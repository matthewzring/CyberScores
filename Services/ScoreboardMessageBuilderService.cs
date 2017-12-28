using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using CyberPatriot;
using CyberPatriot.Models;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public class ScoreboardMessageBuilderService
    {
        public FlagProviderService FlagProvider { get; set; }
        // hack, needed for formatting
        public IScoreRetrievalService ScoreRetriever { get; set; }

        public ScoreboardMessageBuilderService(FlagProviderService flagProvider, IScoreRetrievalService scoreRetriever)
        {
            FlagProvider = flagProvider;
            ScoreRetriever = scoreRetriever;
        }

        public string CreateTopLeaderboardEmbed(CompleteScoreboardSummary scoreboard, TimeZoneInfo timeZone = null, int pageNumber = 1, int pageSize = 15)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            int pageCount = (int)(Math.Ceiling((((double)scoreboard.TeamList.Count) / pageSize)));

            if (--pageNumber < 0 || pageNumber >= pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("**CyberPatriot Scoreboard");
            if (scoreboard.Filter.Division.HasValue)
            {
                stringBuilder.Append(", ").Append(Utilities.ToStringCamelCaseToSpace(scoreboard.Filter.Division.Value));
                if (scoreboard.Filter.Tier != null)
                {
                    stringBuilder.Append(' ').Append(scoreboard.Filter.Tier);
                }
            }
            else if (scoreboard.Filter.Tier != null)
            {
                stringBuilder.Append(", ").Append(scoreboard.Filter.Tier).Append(" Tier");
            }
            if (pageCount > 1)
            {
                stringBuilder.Append(" (Page ").Append(pageNumber + 1).Append(" of ").Append(pageCount).Append(')');
            }
            stringBuilder.AppendLine("**");
            stringBuilder.Append("*As of: ");
            DateTimeOffset timestamp = timeZone == null ? scoreboard.SnapshotTimestamp : TimeZoneInfo.ConvertTime(scoreboard.SnapshotTimestamp, timeZone);
            stringBuilder.AppendFormat("{0:g}", timestamp);
            stringBuilder.Append(' ').Append(timeZone == null ? "UTC" : TimeZoneNames.TZNames.GetAbbreviationsForTimeZone(timeZone.Id, "en-US").Generic).AppendLine("*");
            stringBuilder.AppendLine("```");

            // FIXME time display logic according to FormattingOptions
            scoreboard.TeamList.Skip(pageNumber * pageSize).Take(pageSize)
                .Select((team, i) => stringBuilder.AppendFormat("#{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + 1 + (pageNumber * pageSize), team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine())
                .Consume();
            stringBuilder.AppendLine("```");
            if (scoreboard.OriginUri != null)
            {
                stringBuilder.AppendLine(scoreboard.OriginUri.ToString());
            }
            return stringBuilder.ToString();
        }

        public EmbedBuilder CreateTeamDetailsEmbed(ScoreboardDetails teamScore, CompleteScoreboardSummary peerScoreboard = null)
        {
            if (teamScore == null)
            {
                throw new ArgumentNullException(nameof(teamScore));
            }

            var builder = new EmbedBuilder()
                .WithTimestamp(teamScore.SnapshotTimestamp)
                .WithTitle(teamScore.Summary.Division.ToStringCamelCaseToSpace() + (teamScore.Summary.Tier == null ? string.Empty : (" " + teamScore.Summary.Tier)) + " Team " + teamScore.Summary.TeamId);

            // scoreboard link
            if (teamScore.OriginUri != null)
            {
                builder.Url = teamScore.OriginUri.ToString();
            }

            // location -> flag in thumbnail
            string flagUrl = FlagProvider.GetFlagUri(teamScore.Summary.Location);
            if (flagUrl != null)
            {
                builder.ThumbnailUrl = flagUrl;
            }

            // tier -> color on side
            // colors borrowed from AFA's spreadsheet
            switch (teamScore.Summary.Tier?.Trim().ToLower())
            {
                case "platinum":
                    // tweaked from AFA spreadsheet to be more distinct from silver
                    // AFA original is #DAE3F3
                    builder.WithColor(183, 201, 243);
                    break;
                case "gold":
                    builder.WithColor(0xFF, 0xE6, 0x99);
                    break;
                case "silver":
                    builder.WithColor(0xF2, 0xF2, 0xF2);
                    break;
            }

            // TODO image lookup for location? e.g. thumbnail with flag?
            foreach (var item in teamScore.Images)
            {
                string penaltyAppendage = item.Penalties != 0 ? " - " + Utilities.Pluralize("penalty", item.Penalties) : string.Empty;
                bool overtime = (item.Warnings & ScoreWarnings.TimeOver) == ScoreWarnings.TimeOver;
                bool multiimage = (item.Warnings & ScoreWarnings.MultiImage) == ScoreWarnings.MultiImage;
                string warningAppendage = string.Empty;
                const string multiImageStr = "**M**ulti-image";
                const string overTimeStr = "**T**ime";
                if (overtime || multiimage)
                {
                    warningAppendage = "     Penalties: ";
                }
                if (overtime && multiimage)
                {
                    warningAppendage += multiImageStr + ", " + overTimeStr;
                }
                else if (overtime || multiimage)
                {
                    warningAppendage += multiimage ? multiImageStr : overTimeStr;
                }
                string vulnsString = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetriever.FormattingOptions.VulnerabilityDisplay, item.VulnerabilitiesFound, item.VulnerabilitiesRemaining + item.VulnerabilitiesFound) ? $" ({item.VulnerabilitiesFound}/{item.VulnerabilitiesFound + item.VulnerabilitiesRemaining} vulns{penaltyAppendage})" : string.Empty;
                string playTimeStr = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetriever.FormattingOptions.TimeDisplay, item.PlayTime) ? $" in {item.PlayTime:hh\\:mm}" : string.Empty;
                builder.AddField('`' + item.ImageName + $": {ScoreRetriever.FormattingOptions.FormatScore(item.Score)}pts`", $"{ScoreRetriever.FormattingOptions.FormatScore(item.Score)} points{vulnsString}{playTimeStr}{warningAppendage}");
            }

            string totalScoreTimeAppendage = string.Empty;
            if (ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetriever.FormattingOptions.TimeDisplay, teamScore.Summary.PlayTime))
            {
                totalScoreTimeAppendage = $" in {teamScore.Summary.PlayTime:hh\\:mm}";
            }

            builder.AddInlineField("Total Score", $"{ScoreRetriever.FormattingOptions.FormatScore(teamScore.Summary.TotalScore)} points" + totalScoreTimeAppendage);
            if (peerScoreboard != null)
            {
                // don't pollute upstream
                CompleteScoreboardSummary totalDivisionScoreboard = peerScoreboard.Clone();
                if (totalDivisionScoreboard.Filter.Tier == null)
                {
                    // can't filter if it already has a tier filter
                    totalDivisionScoreboard.WithFilter(teamScore.Summary.Division, null);
                }

                // filter to peer teams
                peerScoreboard.WithFilter(teamScore.Summary.Division, teamScore.Summary.Tier);
                // descending order
                IList<ScoreboardSummaryEntry> peerTeams = peerScoreboard.TeamList;
                if (peerTeams.Count > 0)
                {
                    int myIndexInPeerList = peerTeams.IndexOfWhere(entr => entr.TeamId == teamScore.TeamId);

                    StringBuilder rankEmbedBuilder = new StringBuilder();
                    rankEmbedBuilder.AppendLine(Utilities.AppendOrdinalSuffix(myIndexInPeerList + 1) + " of " + Utilities.Pluralize("peer team", peerTeams.Count));
                    if (totalDivisionScoreboard.Filter.Tier == null && totalDivisionScoreboard.Filter != peerScoreboard.Filter)
                    {
                        rankEmbedBuilder.AppendLine(Utilities.AppendOrdinalSuffix(totalDivisionScoreboard.TeamList.IndexOf(teamScore.Summary) + 1) + " of " + Utilities.Pluralize("team", totalDivisionScoreboard.TeamList.Count) + " in division");
                    }
                    builder.AddInlineField("Rank", rankEmbedBuilder.ToString());

                    double rawPercentile = 1.0 - (((double)peerTeams.Count(peer => peer.TotalScore >= teamScore.Summary.TotalScore)) / peerTeams.Count);
                    builder.AddInlineField("Percentile", Math.Round(rawPercentile * 1000) / 10 + "th percentile");
                    StringBuilder marginBuilder = new StringBuilder();
                    if (myIndexInPeerList > 0)
                    {
                        int marginUnderFirst = peerTeams[0].TotalScore - teamScore.Summary.TotalScore;
                        marginBuilder.AppendLine($"{ScoreRetriever.FormattingOptions.FormatLabeledScoreDifference(marginUnderFirst)} under 1st place");
                    }
                    if (myIndexInPeerList >= 2)
                    {
                        int marginUnderAbove = peerTeams[myIndexInPeerList - 1].TotalScore - teamScore.Summary.TotalScore;
                        marginBuilder.AppendLine($"{ScoreRetriever.FormattingOptions.FormatLabeledScoreDifference(marginUnderAbove)} under {Utilities.AppendOrdinalSuffix(myIndexInPeerList)} place");
                    }
                    if (myIndexInPeerList < peerTeams.Count - 1)
                    {
                        int marginAboveUnder = teamScore.Summary.TotalScore - peerTeams[myIndexInPeerList + 1].TotalScore;
                        marginBuilder.AppendLine($"{ScoreRetriever.FormattingOptions.FormatLabeledScoreDifference(marginAboveUnder)} above {Utilities.AppendOrdinalSuffix(myIndexInPeerList + 2)} place");
                    }
                    // TODO division- and round-specific margins
                    builder.AddInlineField("Margin", marginBuilder.ToString());
                }
            }

            return builder;
        }
    }
}