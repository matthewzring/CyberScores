using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPatriot.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Threading;

namespace CyberPatriot.DiscordBot.Services
{
    public class HttpScoreboardScoreRetrievalService : IScoreRetrievalService, IDisposable
    {
        public string Hostname { get; private set; }
        protected HttpClient Client { get; }

        public bool IsDynamic => true;
        public string StaticSummaryLine => Hostname;
        public ScoreFormattingOptions FormattingOptions { get; } = new ScoreFormattingOptions();
        public CompetitionRound Round => _roundInferenceService == null ? 0 : _roundInferenceService.InferRound(DateTimeOffset.UtcNow);



        // 1 request every 1.5 seconds
        // note the delay may be up to 3s
        protected IRateLimitProvider RateLimiter { get; set; } = new TimerRateLimitProvider(1500);
        private ICompetitionRoundLogicService _roundInferenceService = null;

        #region Rate Limiting Implementation
        protected interface IRateLimitProvider
        {
            Task GetWorkAuthorizationAsync();
            void AddPrerequisite(Task prereq);
        }

        /// <summary>
        /// A rate limit provider that performs no limiting.
        /// </summary>
        protected class NoneRateLimitProvider : IRateLimitProvider
        {
            public Task GetWorkAuthorizationAsync() => Task.CompletedTask;
            public void AddPrerequisite(Task t) { }
        }

        /// <summary>
        /// A rate limit provider backed by a Timer. May have extended delays by up to a factor of two.
        /// </summary>
        protected class TimerRateLimitProvider : IRateLimitProvider, IDisposable
        {
            // based on https://stackoverflow.com/questions/34792699/async-version-of-monitor-pulse-wait
            internal sealed class Awaiter
            {
                private readonly ConcurrentQueue<TaskCompletionSource<byte>> _waiting = new ConcurrentQueue<TaskCompletionSource<byte>>();
                private readonly object _syncContext = new object();
                private volatile ConcurrentBag<Task> _pulsePrerequs = new ConcurrentBag<Task>();

                public void Pulse()
                {
                    // this is called from a threadpool thread, we don't mind if we block it
                    // but if it executes for a while we don't want it to spin up 1000 threads, therefore
                    // only one thread can be trying to dequeue an awaiter at once
                    // if another thread comes along while this is still in the lock, it'll silently "fail" and exit
                    if (Monitor.TryEnter(_syncContext))
                    {
                        try
                        {
                            // FIXME this feels dreadfully hacky
                            // should not need to recreate the bag every pulse
                            var prereqBag = new ConcurrentBag<Task>();
                            prereqBag = Interlocked.Exchange(ref _pulsePrerequs, prereqBag);

                            // this is the old prereq bag, nobody's modifying it (we've swapped it out)
                            // wait for all the prereqs, then clear the bag (we don't want a reference to them lying around)
                            // this is the blocking call that necessitates the lock
                            Task.WhenAll(prereqBag).Wait();
                            prereqBag.Clear();
                            prereqBag = null;

                            // finally complete a waiting task
                            TaskCompletionSource<byte> tcs;
                            if (_waiting.TryDequeue(out tcs))
                            {
                                tcs.TrySetResult(1);
                            }
                        }
                        finally
                        {
                            Monitor.Exit(_syncContext);
                        }
                    }
                }

                public Task Wait()
                {
                    // no two awaiters can wait on the same task
                    /*
                    TaskCompletionSource<byte> tcs;
                    if (_waiting.TryPeek(out tcs))
                    {
                        return tcs.Task;
                    }
                    */

                    var tcs = new TaskCompletionSource<byte>();
                    _waiting.Enqueue(tcs);
                    return tcs.Task;
                }

                public void RegisterPrerequisite(Task t)
                {
                    // if we're in the middle of a tick it's ok if the prereq doesn't get awaited this tick
                    // the lock isn't critical over here
                    // it's just important it gets awaited before the next Pulse
                    _pulsePrerequs.Add(t);
                }
            }


            private Awaiter TaskAwaiter { get; } = new Awaiter();
            private Timer Timer { get; }

            public TimerRateLimitProvider(TimeSpan interval)
            {
                Timer = new Timer(TriggerAwaiter, null, TimeSpan.Zero, interval);
            }

            public TimerRateLimitProvider(int millis) : this(TimeSpan.FromMilliseconds(millis))
            {
            }

            private void TriggerAwaiter(object state = null) => TaskAwaiter.Pulse();

            public Task GetWorkAuthorizationAsync() => TaskAwaiter.Wait();

            public void AddPrerequisite(Task prerequisite) => TaskAwaiter.RegisterPrerequisite(prerequisite);

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Timer.Dispose();
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~TimerRateLimitProvider() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion
        }
        #endregion

        public HttpScoreboardScoreRetrievalService() : this(null)
        {

        }

        public HttpScoreboardScoreRetrievalService(string hostname)
        {
            Hostname = hostname;
            Client = new HttpClient();
        }

        public Task InitializeAsync(IServiceProvider provider)
        {
            if (Hostname == null)
            {
                Hostname = provider.GetRequiredService<IConfiguration>()["defaultScoreboardHostname"];
            }

            _roundInferenceService = provider.GetService<ICompetitionRoundLogicService>() ?? _roundInferenceService;

            return Task.CompletedTask;
        }

        protected virtual Uri BuildScoreboardUri(Division? divisionFilter, string tierFilter)
        {

            var builder = new UriBuilder();
            builder.Scheme = "http";
            builder.Host = Hostname;
            builder.Path = "/";
            // there doesn't appear to be a terribly clean way to do this
            List<string> queryList = new List<string>(2);
            if (divisionFilter.HasValue)
            {
                queryList.Add("division=" + WebUtility.UrlEncode(divisionFilter.Value.ToStringCamelCaseToSpace()));
            }
            if (tierFilter != null)
            {

                queryList.Add("tier=" + WebUtility.UrlEncode(tierFilter));
            }

            builder.Query = string.Join("&", queryList);
            return builder.Uri;
        }

        protected virtual Uri BuildDetailsUri(TeamId team)
        {
            var builder = new UriBuilder();
            builder.Scheme = "http";
            builder.Host = Hostname;
            builder.Path = "/team.php";
            builder.Query = "team=" + team.ToString();
            return builder.Uri;
        }

        protected virtual async Task<string> GetHtmlForScoreboardUri(Uri scoreboardUri)
        {
            string scoreboardPage;
            try
            {
                scoreboardPage = await Client.GetStringAsync(scoreboardUri);
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("Error getting scoreboard page, perhaps the scoreboard is offline?");
            }

            return scoreboardPage;
        }

        protected Task<HtmlDocument> ParseHtmlDocumentAsync(string htmlContents) => // potentially cpu-bound
            Task.Run(() =>
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(htmlContents);
                return doc;
            });

        protected virtual IEnumerable<ScoreboardSummaryEntry> ProcessSummaries(HtmlDocument doc, out DateTimeOffset processTimestamp)
        {
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            processTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));

            var teamsTable = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table").ChildNodes.Where(n => n.Name != "#text").ToArray();

            IEnumerable<ScoreboardSummaryEntry> SummaryProcess()
            {

                for (int i = 1; i < teamsTable.Length; i++)
                {
                    string[] dataEntries = teamsTable[i].ChildNodes.Select(n => n.InnerText.Trim()).ToArray();
                    ScoreboardSummaryEntry summary = new ScoreboardSummaryEntry();
                    summary.TeamId = TeamId.Parse(dataEntries[0]);
                    summary.Location = dataEntries[1];
                    if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[2], out Division division))
                    {
                        summary.Division = division;
                    }
                    if (!Utilities.IsFakeTier(dataEntries[3]))
                    {
                        summary.Tier = dataEntries[3];
                    }
                    summary.ImageCount = int.Parse(dataEntries[4]);
                    summary.PlayTime = Utilities.ParseHourMinuteTimespan(dataEntries[5]);
                    summary.TotalScore = int.Parse(dataEntries[6]);
                    summary.Warnings |= dataEntries[7].Contains("T") ? ScoreWarnings.TimeOver : 0;
                    summary.Warnings |= dataEntries[7].Contains("M") ? ScoreWarnings.MultiImage : 0;
                    yield return summary;
                }
            }

            return SummaryProcess();
        }

        public async Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            if (team == null)
            {
                throw new ArgumentNullException(nameof(team));
            }

            string detailsPage;
            Uri detailsUri = BuildDetailsUri(team);

            await RateLimiter.GetWorkAuthorizationAsync();

            Task<string> stringTask = Client.GetStringAsync(detailsUri);
            RateLimiter.AddPrerequisite(stringTask);
            try
            {
                detailsPage = await stringTask;
                // hacky, cause they don't return a proper error page for nonexistant teams
                if (!detailsPage.Contains(@"<div id='chart_div' class='chart'>"))
                {
                    throw new ArgumentException("The given team does not exist.");
                }
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("Error getting team details page, perhaps the scoreboard is offline?");
            }

            ScoreboardDetails retVal = new ScoreboardDetails();
            retVal.Summary = new ScoreboardSummaryEntry();
            retVal.OriginUri = detailsUri;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(detailsPage);
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            retVal.SnapshotTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));
            var summaryRow = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[1]/tr[2]");
            // ID, Division (labeled location, their bug), Location (labeled division, their bug), tier, scored img, play time, score time, current score, warn
            retVal.Summary.TeamId = TeamId.Parse(summaryRow.ChildNodes[0].InnerText);
            if (Utilities.TryParseEnumSpaceless<Division>(summaryRow.ChildNodes[1].InnerText, out Division division))
            {
                retVal.Summary.Division = division;
            }
            retVal.Summary.Location = summaryRow.ChildNodes[2].InnerText;
            retVal.Summary.Tier = summaryRow.ChildNodes[3].InnerText;
            if (Utilities.IsFakeTier(retVal.Summary.Tier))
            {
                retVal.Summary.Tier = null;
            }
            retVal.Summary.ImageCount = int.Parse(summaryRow.ChildNodes[4].InnerText.Trim());
            retVal.Summary.PlayTime = Utilities.ParseHourMinuteTimespan(summaryRow.ChildNodes[5].InnerText);
            retVal.ScoreTime = Utilities.ParseHourMinuteTimespan(summaryRow.ChildNodes[6].InnerText);
            retVal.Summary.TotalScore = int.Parse(summaryRow.ChildNodes[7].InnerText);
            string warnStr = summaryRow.ChildNodes[8].InnerText;
            retVal.Summary.Warnings |= warnStr.Contains("T") ? ScoreWarnings.TimeOver : 0;
            retVal.Summary.Warnings |= warnStr.Contains("M") ? ScoreWarnings.MultiImage : 0;

            // summary parsed
            var imagesTable = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[2]").ChildNodes.Where(n => n.Name != "#text").ToArray();
            for (int i = 1; i < imagesTable.Length; i++)
            {
                string[] dataEntries = imagesTable[i].ChildNodes.Select(n => n.InnerText.Trim()).ToArray();
                ScoreboardImageDetails image = new ScoreboardImageDetails();
                image.PointsPossible = 100;
                image.ImageName = dataEntries[0];
                image.PlayTime = Utilities.ParseHourMinuteTimespan(dataEntries[1]);
                image.VulnerabilitiesFound = int.Parse(dataEntries[2]);
                image.VulnerabilitiesRemaining = int.Parse(dataEntries[3]);
                image.Penalties = int.Parse(dataEntries[4]);
                image.Score = int.Parse(dataEntries[5]);
                image.Warnings |= dataEntries[6].Contains("T") ? ScoreWarnings.TimeOver : 0;
                image.Warnings |= dataEntries[6].Contains("M") ? ScoreWarnings.MultiImage : 0;
                retVal.Images.Add(image);
            }
            return retVal;
        }
        public virtual async Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            Uri scoreboardUri = BuildScoreboardUri(filter.Division, filter.Tier);
            await RateLimiter.GetWorkAuthorizationAsync();
            var docTask = GetHtmlForScoreboardUri(scoreboardUri);
            RateLimiter.AddPrerequisite(docTask);

            var doc = await ParseHtmlDocumentAsync(await docTask);
            var summaries = ProcessSummaries(doc, out DateTimeOffset snapshotTime);

            return new CompleteScoreboardSummary()
            {
                Filter = filter,
                TeamList = summaries.ToIList(),
                SnapshotTimestamp = snapshotTime,
                OriginUri = scoreboardUri
            };
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Client.Dispose();
                    (RateLimiter as IDisposable)?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~HttpScoreboardScoreRetrievalService() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}