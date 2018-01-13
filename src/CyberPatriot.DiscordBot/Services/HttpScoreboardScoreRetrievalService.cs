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
        public string Hostname { get; protected set; }
        protected HttpClient Client { get; }

        public virtual bool IsDynamic => true;
        public virtual string StaticSummaryLine => Hostname;
        public ScoreFormattingOptions FormattingOptions { get; protected set; } = new ScoreFormattingOptions();
        public CompetitionRound Round => _roundInferenceService == null ? 0 : _roundInferenceService.InferRound(DateTimeOffset.UtcNow);



        // 1 request every 1.5 seconds
        // note the delay may be up to 3s
        public IRateLimitProvider RateLimiter { get; protected set; } = new TimerRateLimitProvider(1500, 3);
        private ICompetitionRoundLogicService _roundInferenceService = null;
        private IExternalCategoryProviderService _categoryProvider = null;

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
            var confProvider = provider.GetRequiredService<IConfiguration>();
            if (Hostname == null)
            {
                Hostname = confProvider["httpConfig:defaultHostname"];
            }
            var httpConfSection = confProvider.GetSection("httpConfig");
            if (httpConfSection != null)
            {
                string uname, pw;
                if ((uname = httpConfSection.GetSection("authentication")["username"]) != null && (pw = httpConfSection.GetSection("authentication")["password"]) != null)
                {
                    Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(uname + ':' + pw)));
                }
                string usragentheader;
                if ((usragentheader = httpConfSection["useragent"]) != null)
                {
                    Client.DefaultRequestHeaders.Add("User-Agent", usragentheader);
                }
            }

            _roundInferenceService = provider.GetService<ICompetitionRoundLogicService>() ?? _roundInferenceService;

            // optionally, attempt to deduce categories
            _categoryProvider = provider.GetService<IExternalCategoryProviderService>();

            return Task.CompletedTask;
        }

        protected virtual Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter)
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
            if (tierFilter.HasValue)
            {

                queryList.Add("tier=" + WebUtility.UrlEncode(tierFilter.Value.ToString()));
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
                scoreboardPage = await Client.GetStringAsync(scoreboardUri).ConfigureAwait(false);
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
                    summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
                    summary.Location = dataEntries[1];
                    if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[2], out Division division))
                    {
                        summary.Division = division;
                    }
                    if (Enum.TryParse<Tier>(dataEntries[3]?.Trim(), true, out Tier tier))
                    {
                        summary.Tier = tier;
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

            await RateLimiter.GetWorkAuthorizationAsync().ConfigureAwait(false);

            Task<string> stringTask = Client.GetStringAsync(detailsUri);
            RateLimiter.AddPrerequisite(stringTask);
            try
            {
                detailsPage = await stringTask.ConfigureAwait(false);
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
            retVal.Summary.Category = _categoryProvider?.GetCategory(retVal.TeamId);
            if (Utilities.TryParseEnumSpaceless<Division>(summaryRow.ChildNodes[1].InnerText, out Division division))
            {
                retVal.Summary.Division = division;
            }
            retVal.Summary.Location = summaryRow.ChildNodes[2].InnerText;
            if (Enum.TryParse<Tier>(summaryRow.ChildNodes[3].InnerText, true, out Tier tier))
            {
                retVal.Summary.Tier = tier;
            }
            retVal.Summary.ImageCount = int.Parse(summaryRow.ChildNodes[4].InnerText.Trim());
            retVal.Summary.PlayTime = Utilities.ParseHourMinuteTimespan(summaryRow.ChildNodes[5].InnerText);
            string scoreTimeText = summaryRow.ChildNodes[6].InnerText;
            // to deal with legacy scoreboards
            int scoreTimeIndOffset = 0;
            if (scoreTimeText.Contains(":"))
            {
                retVal.ScoreTime = Utilities.ParseHourMinuteTimespan(summaryRow.ChildNodes[6].InnerText);
            }
            else
            {
                retVal.ScoreTime = retVal.Summary.PlayTime;
                scoreTimeIndOffset = -1;
            }
            retVal.Summary.TotalScore = int.Parse(summaryRow.ChildNodes[7 + scoreTimeIndOffset].InnerText);
            string warnStr = summaryRow.ChildNodes[8 + scoreTimeIndOffset].InnerText;
            retVal.Summary.Warnings |= warnStr.Contains("T") ? ScoreWarnings.TimeOver : 0;
            retVal.Summary.Warnings |= warnStr.Contains("M") ? ScoreWarnings.MultiImage : 0;

            // summary parsed
            var imagesTable = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[2]").ChildNodes.Where(n => n.Name != "#text").ToArray();
            for (int i = 1; i < imagesTable.Length; i++)
            {
                // skip team IDs to account for legacy scoreboards
                string[] dataEntries = imagesTable[i].ChildNodes.Select(n => n.InnerText.Trim()).SkipWhile(s => TeamId.TryParse(s, out TeamId _)).ToArray();
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
            await RateLimiter.GetWorkAuthorizationAsync().ConfigureAwait(false);
            var docTask = GetHtmlForScoreboardUri(scoreboardUri);
            RateLimiter.AddPrerequisite(docTask);

            var doc = await ParseHtmlDocumentAsync(await docTask).ConfigureAwait(false);
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