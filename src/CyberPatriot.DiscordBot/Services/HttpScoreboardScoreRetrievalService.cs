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
using System.Text.RegularExpressions;

namespace CyberPatriot.DiscordBot.Services
{
    public class HttpScoreboardScoreRetrievalService : IScoreRetrievalService, IDisposable
    {
        public string Hostname { get; protected set; }
        protected HttpClient Client { get; }

        public virtual CompetitionRound Round => _roundInferenceService == null ? 0 : _roundInferenceService.InferRound(DateTimeOffset.UtcNow);

        protected class HttpPassthroughScoreRetrieverMetadata : Models.IScoreRetrieverMetadata
        {
            protected HttpScoreboardScoreRetrievalService ScoreRetriever { get; set; }

            public HttpPassthroughScoreRetrieverMetadata(HttpScoreboardScoreRetrievalService scoreRetriever)
            {
                ScoreRetriever = scoreRetriever;
            }

            public virtual bool IsDynamic => true;

            public virtual string StaticSummaryLine => ScoreRetriever.Hostname;

            public virtual ScoreFormattingOptions FormattingOptions { get; protected set; } = new ScoreFormattingOptions();
        }

        public virtual Models.IScoreRetrieverMetadata Metadata { get; protected set; }


        // a service
        public IRateLimitProvider RateLimiter { get; protected set; } = new NoneRateLimitProvider();
        protected ICompetitionRoundLogicService _roundInferenceService = null;
        protected IExternalCategoryProviderService _categoryProvider = null;

        public HttpScoreboardScoreRetrievalService() : this(null)
        {

        }

        public HttpScoreboardScoreRetrievalService(string hostname)
        {
            Hostname = hostname;
            Client = new HttpClient();
            Metadata = new HttpPassthroughScoreRetrieverMetadata(this);
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

            RateLimiter = provider.GetService<IRateLimitProvider>() ?? new NoneRateLimitProvider();

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
            catch (HttpRequestException e)
            {
                throw new InvalidOperationException("Error getting scoreboard page, perhaps the scoreboard is offline?", e);
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

        private const int DefaultSummaryEntryColumnCount = 8;

        protected virtual ScoreboardSummaryEntry ParseSummaryEntry(string[] dataEntries)
        {
            ScoreboardSummaryEntry summary = new ScoreboardSummaryEntry();
            summary.TeamId = TeamId.Parse(dataEntries[1]);
            summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
            summary.Location = dataEntries[2];
            if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[3], out Division division))
            {
                summary.Division = division;
            }
            if (Enum.TryParse<Tier>(dataEntries[4]?.Trim(), true, out Tier tier))
            {
                summary.Tier = tier;
            }
            summary.ImageCount = int.Parse(dataEntries[5]);
            summary.PlayTime = Utilities.ParseHourMinuteTimespan(dataEntries[6]);
            summary.TotalScore = int.Parse(dataEntries.Last());
            summary.Warnings |= dataEntries[7].Contains("T") ? ScoreWarnings.TimeOver : 0;
            summary.Warnings |= dataEntries[7].Contains("M") ? ScoreWarnings.MultiImage : 0;

            return summary;
        }

        /// <summary>
        /// Parses a detailed summary entry into a scoreboard details object.
        /// </summary>
        /// <param name="dataEntries">The data.</param>
        protected virtual void ParseDetailedSummaryEntry(ScoreboardDetails details, string[] dataEntries)
        {
            var summary = new ScoreboardSummaryEntry();
            details.Summary = summary;
            // ID, Division (labeled location, their bug), Location (labeled division, their bug), tier, scored image count, play time, score time, warnings, current score
            summary.TeamId = TeamId.Parse(dataEntries[0]);
            // [not in data, matched from categoryProvider] all service category
            summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
            // tier and division
            if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[2], out Division division))
            {
                summary.Division = division;
            }
            summary.Location = dataEntries[1];
            if (Enum.TryParse<Tier>(dataEntries[3], true, out Tier tier))
            {
                summary.Tier = tier;
            }
            // number of images
            summary.ImageCount = int.Parse(dataEntries[4].Trim());
            // times
            summary.PlayTime = Utilities.ParseHourMinuteTimespan(dataEntries[5]);
            details.ScoreTime = Utilities.ParseHourMinuteTimespan(dataEntries[6]);
            // warnings and total score
            string warnStr = dataEntries[7];
            summary.Warnings |= warnStr.Contains("T") ? ScoreWarnings.TimeOver : 0;
            summary.Warnings |= warnStr.Contains("M") ? ScoreWarnings.MultiImage : 0;
            summary.TotalScore = int.Parse(dataEntries.Last().Trim());
        }

        protected virtual IEnumerable<ScoreboardSummaryEntry> ProcessSummaries(HtmlDocument doc, out DateTimeOffset processTimestamp)
        {
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            processTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));

            return doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table").ChildNodes
                .Where(n => n.Name != "#text")
                .Skip(1) // header
                .Select(n => n.ChildNodes.Select(c => c.InnerText.Trim()).ToArray())
                .Select(ParseSummaryEntry);
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
            catch (HttpRequestException e)
            {
                throw new InvalidOperationException("Error getting team details page, perhaps the scoreboard is offline?", e);
            }

            ScoreboardDetails retVal = new ScoreboardDetails();
            retVal.OriginUri = detailsUri;

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(detailsPage);
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            retVal.SnapshotTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));
            var summaryHeaderRow = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[1]/tr[1]");
            var summaryHeaderRowData = summaryHeaderRow.ChildNodes.Select(x => x.InnerText).ToArray();
            var summaryRow = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[1]/tr[2]");
            var summaryRowData = summaryRow.ChildNodes.Select(x => x.InnerText).ToArray();
            ParseDetailedSummaryEntry(retVal, summaryRowData);

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

            // reparse summary table (CCS+Cisco case)
            // pseudoimages: Cisco, penalty
            int ciscoIndex = summaryHeaderRowData.IndexOfWhere(x => x.ToLower().Contains("cisco"));
            int penaltyIndex = summaryHeaderRowData.IndexOfWhere(x => x.ToLower().Contains("penalty"));

            ScoreboardImageDetails CreatePseudoImage(string name, int score, int possible)
            {
                var image = new ScoreboardImageDetails();
                image.PointsPossible = possible;
                image.ImageName = name;
                image.Score = score;

                image.VulnerabilitiesFound = 0;
                image.VulnerabilitiesRemaining = 0;
                image.Penalties = 0;
                image.Warnings = 0;
                image.PlayTime = TimeSpan.Zero;

                return image;
            }

            if (ciscoIndex != -1)
            {
                // pseudoimage
                // FIXME shouldn't display vulns and penalties and time

                int ciscoDenom = -1;
                try
                {
                    ciscoDenom = _roundInferenceService.GetCiscoPointsPossible(Round, retVal.Summary.Division, retVal.Summary.Tier);
                }
                catch
                {
                    // probably because round 0; unknown total
                }

                retVal.Images.Add(CreatePseudoImage("Cisco (Total)", int.Parse(summaryRowData[ciscoIndex]), ciscoDenom));
            }

            if (penaltyIndex != -1)
            {
                retVal.Images.Add(CreatePseudoImage("Administrative Penalties", int.Parse(summaryRowData[penaltyIndex]), 0));
            }

            // score graph
            try
            {
                var teamScoreGraphHeader = new Regex(@"\['Time'(?:, '(\w+)')* *\]");
                var teamScoreGraphEntry = new Regex(@"\['(\d{2}/\d{2} \d{2}:\d{2})'(?:, (-?\d+|null))*\]");
                Match headerMatch = teamScoreGraphHeader.Match(detailsPage);
                if (headerMatch?.Success ?? false)
                {
                    retVal.ImageScoresOverTime = new Dictionary<string, SortedDictionary<DateTimeOffset, int?>>();
                    string[] imageHeaders = headerMatch.Groups[1].Captures.Select(c => c.Value).ToArray();
                    SortedDictionary<DateTimeOffset, int?>[] dictArr = new SortedDictionary<DateTimeOffset, int?>[imageHeaders.Length];
                    for (int i = 0; i < dictArr.Length; i++)
                    {
                        dictArr[i] = new SortedDictionary<DateTimeOffset, int?>();
                        retVal.ImageScoresOverTime[imageHeaders[i]] = dictArr[i];
                    }
                    teamScoreGraphEntry.Matches(detailsPage).Where(g => g?.Success ?? false).Select<Match, object>((m, ind) =>
                        {
                            DateTimeOffset dto = default(DateTimeOffset);
                            try
                            {
                                // MM/dd hh:mm
                                string dateStr = m.Groups[1].Value;
                                string[] dateStrComponents = dateStr.Split(' ');
                                string[] dateComponents = dateStrComponents[0].Split('/');
                                string[] timeComponents = dateStrComponents[1].Split(':');
                                dto = new DateTimeOffset(DateTimeOffset.UtcNow.Year, int.Parse(dateComponents[0]), int.Parse(dateComponents[1]), int.Parse(timeComponents[0]), int.Parse(timeComponents[1]), 0, TimeSpan.Zero);
                            }
                            catch
                            {
                                return null;
                            }

                            var captures = m.Groups[2].Captures;

                            for (int i = 0; i < captures.Count; i++)
                            {
                                int? scoreVal = null;
                                if (int.TryParse(captures[i].Value, out int thingValTemp))
                                {
                                    scoreVal = thingValTemp;
                                }
                                dictArr[i][dto] = scoreVal;
                            }

                            return null;
                        }).Consume();
                }
            }
            catch
            {
                // TODO log
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