#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPatriot.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;

namespace CyberPatriot.Services.ScoreRetrieval
{
    public class HttpScoreboardScoreRetrievalService : IScoreRetrievalService, IDisposable
    {
        public string Hostname { get; protected set; }
        protected HttpClient Client { get; }

        public virtual CompetitionRound Round => _roundInferenceService.InferRound(DateTimeOffset.UtcNow);

        protected class HttpPassthroughScoreRetrieverMetadata : Metadata.IScoreRetrieverMetadata
        {
            protected HttpScoreboardScoreRetrievalService ScoreRetriever { get; set; }

            public HttpPassthroughScoreRetrieverMetadata(HttpScoreboardScoreRetrievalService scoreRetriever)
            {
                ScoreRetriever = scoreRetriever;
            }

            public virtual bool IsDynamic => true;

            public virtual bool SupportsInexpensiveDetailQueries => false;

            public virtual string StaticSummaryLine => ScoreRetriever.Hostname;

            public virtual Metadata.ScoreFormattingOptions FormattingOptions { get; protected set; } = new Metadata.ScoreFormattingOptions();
        }

        Metadata.IScoreRetrieverMetadata IScoreRetrievalService.Metadata => Metadata;

        protected virtual HttpPassthroughScoreRetrieverMetadata Metadata { get; set; }

        // a service
        public IRateLimitProvider RateLimiter { get; protected set; } = new NoneRateLimitProvider();
        protected ICompetitionRoundLogicService _roundInferenceService = null;
        protected IExternalCategoryProviderService _categoryProvider = null;

        internal IConfigurationSection _httpConfiguration;

        public HttpScoreboardScoreRetrievalService() : this(null)
        {
        }

        public HttpScoreboardScoreRetrievalService(string hostname)
        {
            Hostname = hostname;
            Client = new HttpClient();
            Metadata = new HttpPassthroughScoreRetrieverMetadata(this);
        }

        public Task InitializeAsync(IServiceProvider provider, IConfigurationSection httpConfSection)
        {
            _httpConfiguration = httpConfSection;
            if (Hostname == null)
            {
                Hostname = httpConfSection["defaultHostname"];
            }

            int forcedCompetitionRound = 0;

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
                forcedCompetitionRound = httpConfSection.GetValue<int>("forceRound");
            }

            _roundInferenceService = provider.GetService<ICompetitionRoundLogicService>() ?? _roundInferenceService;

            if (forcedCompetitionRound > 0 || _roundInferenceService == null)
            {
                _roundInferenceService = new PreconfiguredRoundPassthroughCompetitionRoundLogicService((CompetitionRound)forcedCompetitionRound, _roundInferenceService);
            }

            // optionally, attempt to deduce categories
            _categoryProvider = provider.GetService<IExternalCategoryProviderService>();

            RateLimiter = provider.GetService<IRateLimitProvider>() ?? new NoneRateLimitProvider();

            return Task.CompletedTask;
        }

        protected virtual Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter)
        {
            var builder = new UriBuilder
            {
                Scheme = "http",
                Host = Hostname,
                Path = "/"
            };
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
            var builder = new UriBuilder
            {
                Scheme = "http",
                Host = Hostname,
                Path = "/team.php",
                Query = "team=" + team.ToString()
            };
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
            ScoreboardSummaryEntry summary = new ScoreboardSummaryEntry
            {
                TeamId = TeamId.Parse(dataEntries[1])
            };
            summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
            summary.Location = dataEntries[2];
            if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[3], out Division division))
            {
                summary.Division = division;
            }
            var nextEntry = 4;
            if (dataEntries[nextEntry].ToLower().Trim().Contains("school"))
            {
                nextEntry += 1;
            }
            else if (Enum.TryParse<Tier>(dataEntries[nextEntry]?.Trim(), true, out Tier tier))
            {
                summary.Tier = tier;
                nextEntry += 1;
            }
            summary.ImageCount = int.Parse(dataEntries[nextEntry]);
            summary.PlayTime = Utilities.ParseHourMinuteSecondTimespan(dataEntries[nextEntry + 1]);
            summary.ScoreTime = Utilities.ParseHourMinuteSecondTimespan(dataEntries[nextEntry + 2]);
            summary.TotalScore = dataEntries.Last() != "" ? double.Parse(dataEntries.Last()) : 0;
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("T") ? ScoreWarnings.TimeOver : 0;
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("M") ? ScoreWarnings.MultiImage : 0;
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("W") ? ScoreWarnings.Withdrawn : 0;

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
            // ID, Location, Division, tier, scored image count, play time, score time, warnings, current score
            summary.TeamId = TeamId.Parse(dataEntries[0]);
            // [not in data, matched from categoryProvider] all service category
            summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
            // tier and division
            summary.Location = dataEntries[1];
            if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[2], out Division division))
            {
                summary.Division = division;
            }
            var nextEntry = 3;
            if (dataEntries[nextEntry].ToLower().Trim().Contains("school"))
            {
                nextEntry += 1;
            }
            else if (Enum.TryParse<Tier>(dataEntries[nextEntry]?.Trim(), true, out Tier tier))
            {
                summary.Tier = tier;
                nextEntry += 1;
            }
            // number of images
            summary.ImageCount = int.Parse(dataEntries[nextEntry].Trim());
            // times
            summary.PlayTime = Utilities.ParseHourMinuteSecondTimespan(dataEntries[nextEntry + 1]);
            summary.ScoreTime = Utilities.ParseHourMinuteSecondTimespan(dataEntries[nextEntry + 2]);
            // warnings and total score
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("T") ? ScoreWarnings.TimeOver : 0;
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("M") ? ScoreWarnings.MultiImage : 0;
            summary.Warnings |= dataEntries[nextEntry + 3].Contains("W") ? ScoreWarnings.Withdrawn : 0;
            summary.TotalScore = dataEntries.Last().Trim() != "" ? double.Parse(dataEntries.Last().Trim()) : 0;
        }

        protected virtual IEnumerable<ScoreboardSummaryEntry> ProcessSummaries(HtmlDocument doc, out DateTimeOffset processTimestamp)
        {
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[3]/div/h4")?.InnerText;
            processTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));
            return doc.DocumentNode.SelectSingleNode("/html/body/div[3]/div/div/div/div/div/table").ChildNodes
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
                if (!detailsPage.Contains("<td>" + team + "</td>"))
                {
                    throw new ArgumentException("The given team does not exist.");
                }
            }
            catch (HttpRequestException e)
            {
                throw new InvalidOperationException("Error getting team details page, perhaps the scoreboard is offline?", e);
            }

            ScoreboardDetails retVal = new ScoreboardDetails
            {
                OriginUri = detailsUri
            };

            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(detailsPage);
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            retVal.SnapshotTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));
            var summaryHeaderRow = doc.DocumentNode.SelectSingleNode("/html/body/div[3]/div/div/div[1]/div/div/table/tr[1]");
            var summaryHeaderRowData = summaryHeaderRow.ChildNodes.Select(x => x.InnerText).ToArray();
            var summaryRow = doc.DocumentNode.SelectSingleNode("/html/body/div[3]/div/div/div[1]/div/div/table/tr[2]");
            var summaryRowData = summaryRow.ChildNodes.Select(x => x.InnerText).ToArray();
            ParseDetailedSummaryEntry(retVal, summaryRowData);

            // summary parsed
            var imagesTable = doc.DocumentNode.SelectSingleNode("/html/body/div[3]/div/div/div[2]/div/div/table").ChildNodes.Where(n => n.Name != "#text").ToArray();
            for (int i = 1; i < imagesTable.Length; i++)
            {
                // skip team IDs to account for legacy scoreboards
                string[] dataEntries = imagesTable[i].ChildNodes.Select(n => n.InnerText.Trim()).SkipWhile(s => TeamId.TryParse(s, out TeamId _)).ToArray();
                ScoreboardImageDetails image = new ScoreboardImageDetails
                {
                    PointsPossible = 100,
                    ImageName = dataEntries[0],
                    PlayTime = Utilities.ParseHourMinuteSecondTimespan(dataEntries[1]),
                    VulnerabilitiesFound = dataEntries[2] != "" ? int.Parse(dataEntries[2]) : 0,
                    VulnerabilitiesRemaining = dataEntries[3] != "" ? int.Parse(dataEntries[3]) : 0,
                    Penalties = dataEntries[4] != "" ? int.Parse(dataEntries[4]) : 0,
                    Score = dataEntries.Last() != "" ? double.Parse(dataEntries.Last()) : 0
                };
                image.Warnings |= dataEntries[5].Contains("T") ? ScoreWarnings.TimeOver : 0;
                image.Warnings |= dataEntries[5].Contains("M") ? ScoreWarnings.MultiImage : 0;
                image.Warnings |= dataEntries[5].Contains("W") ? ScoreWarnings.Withdrawn : 0;
                retVal.Images.Add(image);
            }

            // reparse summary table (CCS+Cisco case)
            // pseudoimages: Cisco, administrative adjustment (usually penalty), Web-based challenge
            int ciscoIndex = summaryHeaderRowData.IndexOfWhere(x => x.ToLower().Contains("cisco"));
            int penaltyIndex = summaryHeaderRowData.IndexOfWhere(x => x.ToLower().Contains("adjust"));
            int challengeIndex = summaryHeaderRowData.IndexOfWhere(x => x.ToLower().Contains("chall"));

            ScoreboardImageDetails CreatePseudoImage(string name, double score, double possible)
            {
                var image = new ScoreboardImageDetails
                {
                    PointsPossible = possible,
                    ImageName = name,
                    Score = score,
                    VulnerabilitiesFound = 0,
                    VulnerabilitiesRemaining = 0,
                    Penalties = 0,
                    Warnings = 0,
                    PlayTime = TimeSpan.Zero
                };

                return image;
            }

            if (ciscoIndex != -1 && summaryRowData[ciscoIndex] != "")
            {
                // pseudoimage
                // FIXME shouldn't display vulns and penalties and time

                double ciscoDenom = -1;
                try
                {
                    ciscoDenom = _roundInferenceService.GetCiscoPointsPossible(Round, retVal.Summary.Division, retVal.Summary.Tier);
                }
                catch
                {
                    // probably because round 0; unknown total
                }
                if (summaryRowData[ciscoIndex] == "")
                    retVal.Images.Add(CreatePseudoImage("Cisco (Total)", 0.0, ciscoDenom));
                else
                    retVal.Images.Add(CreatePseudoImage("Cisco (Total)", double.Parse(summaryRowData[ciscoIndex]), ciscoDenom));
            }

            if (penaltyIndex != -1 && summaryRowData[penaltyIndex] != "")
            {
                double penaltyDenom = -1;
                try
                {
                    penaltyDenom = _roundInferenceService.GetAdjustPointsPossible(Round, retVal.Summary.Division, retVal.Summary.Tier);
                }
                catch
                {
                    // probably because round 0; unknown total
                }
                if (summaryRowData[penaltyIndex] == "")
                    retVal.Images.Add(CreatePseudoImage("Administrative Adjustment", 0.0, penaltyDenom));
                else
                    retVal.Images.Add(CreatePseudoImage("Administrative Adjustment", double.Parse(summaryRowData[penaltyIndex]), penaltyDenom));
            }

            if (challengeIndex != -1)
            {
                double challengeDenom = -1;
                try
                {
                    challengeDenom = _roundInferenceService.GetChallengePointsPossible(Round, retVal.Summary.Division, retVal.Summary.Tier);
                }
                catch
                {
                    // probably because round 0; unknown total
                }
                if (summaryRowData[challengeIndex] == "")
                    retVal.Images.Add(CreatePseudoImage("Challenge Score", 0.0, challengeDenom));
                else
                    retVal.Images.Add(CreatePseudoImage("Challenge Score", double.Parse(summaryRowData[challengeIndex]), challengeDenom));
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
                    string[] imageHeaders = headerMatch.Groups[1].Captures.Cast<Capture>().Select(c => c.Value).ToArray();
                    SortedDictionary<DateTimeOffset, int?>[] dictArr = new SortedDictionary<DateTimeOffset, int?>[imageHeaders.Length];
                    for (int i = 0; i < dictArr.Length; i++)
                    {
                        dictArr[i] = new SortedDictionary<DateTimeOffset, int?>();
                        retVal.ImageScoresOverTime[imageHeaders[i]] = dictArr[i];
                    }
                    foreach (var m in teamScoreGraphEntry.Matches(detailsPage).Cast<Match>().Where(g => g?.Success ?? false))
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
                            continue;
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
                    }
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
            var doc = await ParseHtmlDocumentAsync(await docTask).ConfigureAwait(true);
            var summaries = ProcessSummaries(doc, out DateTimeOffset snapshotTime).Where(x => filter.Matches(x));

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
