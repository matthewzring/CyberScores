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

        protected virtual async Task<IEnumerable<ScoreboardSummary>> ProcessSummaries(Uri scoreboardUri)
        {
            throw new NotImplementedException();
        }

        public async Task<ScoreboardDetails> GetDetails(TeamId team)
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
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException("Error getting team details page, perhaps the scoreboard is offline?");
            }

            ScoreboardDetails retVal = new ScoreboardDetails();
            retVal.Summary = new ScoreboardSummary();
            retVal.OriginUri = detailsUri;
            
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(detailsPage);
            var timestampHeader = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/h2[2]")?.InnerText;
            retVal.Summary.SnapshotTimestamp = timestampHeader == null ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(timestampHeader.Replace("Generated At: ", string.Empty).Replace("UTC", "+0:00"));
            var summaryRow = doc.DocumentNode.SelectSingleNode("/html/body/div[2]/div/table[1]/tr[2]");
            // ID, Division (labeled location, their bug), Location (labeled division, their bug), tier, scored img, play time, score time, current score, warn
            retVal.Summary.TeamId = TeamId.Parse(summaryRow.ChildNodes[0].InnerText);
            if (Utilities.TryParseEnumSpaceless<Division>(summaryRow.ChildNodes[1].InnerText, out Division division))
            {
                retVal.Summary.Division = division;
            }
            retVal.Summary.Location = summaryRow.ChildNodes[2].InnerText;
            retVal.Summary.Tier = summaryRow.ChildNodes[3].InnerText;
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

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard()
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(null, null)));
        }

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter)
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(divisionFilter, null)));
        }

        public IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter, string tierFilter)
        {
            return new AsyncSyncEnumerableWrapper<ScoreboardSummary>(ProcessSummaries(BuildScoreboardUri(divisionFilter, tierFilter)));
        }
    }
}