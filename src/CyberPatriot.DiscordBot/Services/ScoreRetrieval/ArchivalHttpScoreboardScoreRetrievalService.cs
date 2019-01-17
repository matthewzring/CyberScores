using System;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services.ScoreRetrieval
{
    public class ArchivalHttpScoreboardScoreRetrievalService : HttpScoreboardScoreRetrievalService
    {
        protected class ArchivalHttpScoreboardMetadata : HttpScoreboardScoreRetrievalService.HttpPassthroughScoreRetrieverMetadata
        {
            public ArchivalHttpScoreboardMetadata(HttpScoreboardScoreRetrievalService scoreRetriever) : base(scoreRetriever)
            {
            }

            public override string StaticSummaryLine => $"Archive at {ScoreRetriever.Hostname}";
            public override bool IsDynamic => false;
        }

        public ArchivalHttpScoreboardScoreRetrievalService() : this(null)
        {

        }

        public ArchivalHttpScoreboardScoreRetrievalService(string hostname) : base(hostname)
        {
            Metadata = new ArchivalHttpScoreboardMetadata(this);
        }

        protected override ScoreboardSummaryEntry ParseSummaryEntry(string[] dataEntries)
        {
            // TODO more sophisticated logic to handle scoreboard tweaks over the years
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
            return summary;
        }

        protected override void ParseDetailedSummaryEntry(ScoreboardDetails details, string[] dataEntries)
        {
            var summary = new ScoreboardSummaryEntry();
            details.Summary = summary;
            // ID, Division (labeled location, their bug), Location (labeled division, their bug), tier, scored img, play time, score time, current score, warn
            summary.TeamId = TeamId.Parse(dataEntries[0]);
            summary.Category = _categoryProvider?.GetCategory(summary.TeamId);
            if (Utilities.TryParseEnumSpaceless<Division>(dataEntries[1], out Division division))
            {
                summary.Division = division;
            }
            summary.Location = dataEntries[2];
            if (Enum.TryParse<Tier>(dataEntries[3], true, out Tier tier))
            {
                summary.Tier = tier;
            }
            summary.ImageCount = int.Parse(dataEntries[4].Trim());
            summary.PlayTime = Utilities.ParseHourMinuteTimespan(dataEntries[5]);
            string scoreTimeText = dataEntries[6];
            // to deal with legacy scoreboards
            int scoreTimeIndOffset = 0;
            if (scoreTimeText.Contains(":"))
            {
                details.ScoreTime = Utilities.ParseHourMinuteTimespan(dataEntries[6]);
            }
            else
            {
                details.ScoreTime = summary.PlayTime;
                scoreTimeIndOffset = -1;
            }
            summary.TotalScore = int.Parse(dataEntries[7 + scoreTimeIndOffset].Trim());
            string warnStr = dataEntries[8 + scoreTimeIndOffset];
            summary.Warnings |= warnStr.Contains("T") ? ScoreWarnings.TimeOver : 0;
            summary.Warnings |= warnStr.Contains("M") ? ScoreWarnings.MultiImage : 0;
        }

        protected override Uri BuildDetailsUri(TeamId team) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/{team}.html");

        protected override Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/");
    }
}