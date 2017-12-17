using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPatriot.Models;
using HtmlAgilityPack;

namespace CyberPatriot.DiscordBot.Services
{
    public class HttpScoreboardScoreRetrievalService : IScoreRetrievalService
    {
        public string Hostname { get; }
        protected HttpClient Client { get; set; }

        public HttpScoreboardScoreRetrievalService(string hostname)
        {
            Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
            Client = new HttpClient();
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

        protected virtual async Task<HtmlDocument> GetHtmlDocumentForScoreboard(Uri scoreboardUri)
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

            // potentially cpu-bound
            return await Task.Run(() =>
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(scoreboardPage);
                return doc;
            });
        }

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
            try
            {
                detailsPage = await Client.GetStringAsync(detailsUri);
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

            var doc = await GetHtmlDocumentForScoreboard(scoreboardUri);
            var summaries = ProcessSummaries(doc, out DateTimeOffset snapshotTime);

            return new CompleteScoreboardSummary()
            {
                Filter = filter,
                TeamList = summaries.ToIList(),
                SnapshotTimestamp = snapshotTime,
                OriginUri = scoreboardUri
            };
        }
    }
}