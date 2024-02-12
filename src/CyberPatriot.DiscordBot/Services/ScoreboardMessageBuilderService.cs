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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Discord;
using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;
using CyberPatriot.Services;
using CyberPatriot.Services.Metadata;

namespace CyberPatriot.DiscordBot.Services
{
    public class ScoreboardMessageBuilderService
    {
        // hack, needed for formatting
        // the score retriever reference should NOT be used outside of metadata retrieval
        // we need to keep the reference because the metadata instance (e.g. fallback provider) might change
        // TODO proper separate service?
        [Obsolete("The score retriever should not be directly referenced by the formatting class.")]
        private IScoreRetrievalService _scoreRetriever;
#pragma warning disable 0618 // metadata reference may change
        public CyberPatriot.Services.Metadata.IScoreRetrieverMetadata ScoreRetrieverMetadata => _scoreRetriever.Metadata;
#pragma warning restore 0618

        public ICompetitionRoundLogicService CompetitionLogic { get; set; }
        public ILocationResolutionService LocationResolution { get; set; }

        public ScoreboardMessageBuilderService(IScoreRetrievalService scoreRetriever, ICompetitionRoundLogicService competitionLogic, ILocationResolutionService locationResolution)
        {
            CompetitionLogic = competitionLogic;
            LocationResolution = locationResolution;

#pragma warning disable 0618 // initial assignment, see comments near the property
            _scoreRetriever = scoreRetriever;
#pragma warning restore 0618
        }

        protected string AbbreviateDivision(ScoreboardSummaryEntry team)
        {
            if (!team.Category.HasValue || team.Division != Division.AllService)
            {
                return team.Division.ToConciseString();
            }

            return "AS:" + CyberPatriot.Models.Serialization.ServiceCategoryExtensions.Abbreviate(team.Category.Value);
        }

        private string GetTeamLeaderboardEntry(ScoreboardSummaryEntry team, int friendlyIndex, bool useAbbreviatedDivision = false, string prefix = "#", bool showAdvancement = true)
        {
            string divisionFormatString = useAbbreviatedDivision ? "{0,6}" : "  {0,-10}";

            return $"{prefix}{friendlyIndex,-5}{team.TeamId,-7}{team.Location,4}" + string.Format(divisionFormatString, AbbreviateDivision(team)) + $"{team.Tier,10}{ScoreRetrieverMetadata.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore),16}{((showAdvancement && team.Advancement.HasValue) ? team.Advancement.Value.ToConciseString() : (ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.TimeDisplay, team.PlayTime) ? team.PlayTime.ToHoursMinutesSecondsString() : "")),10}{team.Warnings.ToConciseString(),4}";
        }

        private string GetImageLeaderboardEntry(ScoreboardSummaryEntry team, ScoreboardImageDetails image, int friendlyIndex, bool useAbbreviatedDivision = false, string prefix = "#")
        {
            string divisionFormatString = useAbbreviatedDivision ? "{0,6}" : "  {0,-10}";
            string vulnPenString = new string(' ', 10);
            if (ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.VulnerabilityDisplay, image.VulnerabilitiesFound, image.VulnerabilitiesRemaining))
            {
                vulnPenString = $"{image.VulnerabilitiesFound,5}v {image.Penalties,2}p";
            }

            return $"{prefix}{friendlyIndex,-5}{team.TeamId,-7}{team.Location,4}" +
                string.Format(divisionFormatString, AbbreviateDivision(team)) + $"{team.Tier,10}" +
                $"{ScoreRetrieverMetadata.FormattingOptions.FormatScoreForLeaderboard(image.Score),13}" +
                vulnPenString +
                $"{(ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.TimeDisplay, image.PlayTime) ? image.PlayTime.ToHoursMinutesSecondsString() : ""),7}" +
                $"{image.Warnings.ToConciseString(),4}";
        }

        public string CreateImageLeaderboardEmbed(IEnumerable<KeyValuePair<ScoreboardSummaryEntry, ScoreboardImageDetails>> completeImageData, string filterDescription, int teamCount = -1, int pageNumber = 1, int pageSize = 15)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            if (teamCount == -1)
            {
                completeImageData = completeImageData.ToIList();
                teamCount = completeImageData.Count();
            }

            int pageCount = (int)Math.Ceiling(((double)teamCount) / pageSize);

            pageNumber--;

            if (pageNumber < 0 || pageNumber >= pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            IList<KeyValuePair<ScoreboardSummaryEntry, ScoreboardImageDetails>> thisPageImageData = completeImageData.Skip(pageNumber * pageSize).Take(pageSize).ToIList();

            StringBuilder resultBuilder = new StringBuilder();
            resultBuilder.Append("**CyberPatriot Image Scoreboard");
            if (!string.IsNullOrWhiteSpace(filterDescription))
            {
                resultBuilder.Append(", ").Append(filterDescription);
            }
            if (pageCount > 1)
            {
                resultBuilder.Append($" (Page {pageNumber + 1} of {pageCount})");
            }
            resultBuilder.AppendLine("**");

            ScoreboardImageDetails canonicalImage = thisPageImageData[0].Value;
            resultBuilder.AppendFormat("**`{0}`", canonicalImage.ImageName);
            int vulnCt = canonicalImage.VulnerabilitiesRemaining + canonicalImage.VulnerabilitiesFound;
            double pts = canonicalImage.PointsPossible;
            bool displayVulns = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.VulnerabilityDisplay, vulnCt);
            bool displayPts = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.VulnerabilityDisplay, pts);

            if (displayVulns || displayPts)
            {
                resultBuilder.Append(": ");
                if (displayVulns)
                {
                    resultBuilder.Append(Utilities.Pluralize("vulnerability", vulnCt));

                    if (displayPts)
                    {
                        resultBuilder.Append(", ");
                    }
                }
                if (displayPts)
                {
                    resultBuilder.Append(ScoreRetrieverMetadata.FormattingOptions.FormatScore(pts)).Append(" points possible");
                }
            }

            resultBuilder.AppendLine("**");
            resultBuilder.AppendLine("```");

            bool conciseDivision = !thisPageImageData.Any(x => x.Key.Category != null);

            for (int i = 0; i < thisPageImageData.Count; i++)
            {
                var teamScore = thisPageImageData[i];
                int friendlyIndex = i + 1 + (pageNumber * pageSize);
                resultBuilder.AppendLine(GetImageLeaderboardEntry(teamScore.Key, teamScore.Value, friendlyIndex, useAbbreviatedDivision: conciseDivision));
            }
            resultBuilder.AppendLine("```");

            return resultBuilder.ToString();
        }

        public string CreateTopLeaderboardEmbed(CompleteScoreboardSummary scoreboard, TimeZoneInfo timeZone = null, int pageNumber = 1, int pageSize = 15)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            int pageCount = (int)(Math.Ceiling((((double)scoreboard.TeamList.Count()) / pageSize)));

            if (--pageNumber < 0 || pageNumber >= pageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("**CyberPatriot Scoreboard");
            string filterDesc = Utilities.JoinNonNullNonEmpty(", ",
                scoreboard.Filter.Division.HasValue ? Utilities.ToStringCamelCaseToSpace(scoreboard.Filter.Division.Value) + " Division" : null,
                scoreboard.Filter.Category?.ToCanonicalName(),
                scoreboard.Filter.Tier,
                LocationResolution.GetFullNameOrNull(scoreboard.Filter.Location)
                );

            if (filterDesc.Length > 0)
            {
                stringBuilder.Append(", ");
                stringBuilder.Append(filterDesc);
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

            bool conciseDivision = (scoreboard.Filter.Division.HasValue && scoreboard.Filter.Division.Value != Division.AllService) || !scoreboard.TeamList.Any(x => x.Category.HasValue);

            scoreboard.TeamList.Skip(pageNumber * pageSize).Take(pageSize)
                .Select((team, i) => stringBuilder.AppendLine(GetTeamLeaderboardEntry(team, i + 1 + (pageNumber * pageSize), useAbbreviatedDivision: conciseDivision)))
                .Last().AppendLine("```");
            if (scoreboard.OriginUri != null)
            {
                stringBuilder.AppendLine(scoreboard.OriginUri.ToString());
            }
            return stringBuilder.ToString();
        }

        public string CreatePeerLeaderboardEmbed(TeamId teamId, CompleteScoreboardSummary peerScoreboard, TimeZoneInfo timeZone = null, int topTeams = 3, int nearbyTeams = 5)
        {
            // var peerTeams = CompetitionLogic.GetPeerTeams(ScoreRetriever.Round, scoreboard, teamDetails.Summary);
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("**CyberPatriot Scoreboard**");
            DateTimeOffset timestamp = timeZone == null ? peerScoreboard.SnapshotTimestamp : TimeZoneInfo.ConvertTime(peerScoreboard.SnapshotTimestamp, timeZone);
            stringBuilder.AppendFormat("*Competing against: {0} | As of: ", teamId);
            stringBuilder.AppendFormat("{0:g}", timestamp);
            stringBuilder.Append(' ').Append(timeZone == null ? "UTC" : TimeZoneNames.TZNames.GetAbbreviationsForTimeZone(timeZone.Id, "en-US").Generic).AppendLine("*");

            var peerTeams = peerScoreboard.TeamList;

            stringBuilder.AppendFormat("*{0} competes in: {1} Division", teamId, peerScoreboard.Filter.Division.ToStringCamelCaseToSpace());
            if (peerScoreboard.Filter.Category.HasValue)
            {
                stringBuilder.AppendFormat(", {0}", peerScoreboard.Filter.Category.Value.ToCanonicalName());
            }
            if (peerScoreboard.Filter.Tier.HasValue)
            {
                stringBuilder.AppendFormat(", {0} Tier", peerScoreboard.Filter.Tier.Value);
            }
            if (peerScoreboard.Filter.Location != null)
            {
                stringBuilder.AppendFormat(", {0}", peerScoreboard.Filter.Location);
            }
            stringBuilder.AppendLine("*");

            bool conciseDivision = !peerTeams.Any(x => x.Category != null);

            stringBuilder.AppendLine("```bash");
            // zero-based rank of the given team
            int pos = peerTeams.IndexOfWhere(team => team.TeamId == teamId);
            if (pos < nearbyTeams + topTeams + 1)
            {
                peerTeams.Take(nearbyTeams + pos + 1)
                          .Select((team, i) => stringBuilder.AppendLine(GetTeamLeaderboardEntry(team, i + 1, useAbbreviatedDivision: conciseDivision, prefix: team.TeamId == teamId ? ">" : "#")))
                          .Consume();
            }
            else
            {
                peerTeams.Take(topTeams)
                          .Select((team, i) => stringBuilder.AppendLine(GetTeamLeaderboardEntry(team, i + 1, useAbbreviatedDivision: conciseDivision)))
                          .Consume();
                stringBuilder.AppendLine("...");
                peerTeams.Skip(pos - nearbyTeams)
                          .Take(nearbyTeams)
                          .Select((team, i) => stringBuilder.AppendLine(GetTeamLeaderboardEntry(team, i + pos - nearbyTeams + 1, useAbbreviatedDivision: conciseDivision)))
                          .Consume();
                ScoreboardSummaryEntry thisTeamDetails = peerTeams.Single(t => t.TeamId == teamId);
                stringBuilder.AppendLine(GetTeamLeaderboardEntry(thisTeamDetails, pos + 1, useAbbreviatedDivision: conciseDivision, prefix: ">"));
                // since pos and i are both zero-based, i + pos + 2 returns correct team rank for teams after given team
                peerTeams.Skip(pos + 1)
                          .Take(nearbyTeams)
                          .Select((team, i) => stringBuilder.AppendLine(GetTeamLeaderboardEntry(team, i + pos + 2, useAbbreviatedDivision: conciseDivision)))
                          .Consume();
            }

            stringBuilder.AppendLine("```");
            if (peerScoreboard.OriginUri != null)
            {
                stringBuilder.AppendLine(peerScoreboard.OriginUri.ToString());
            }
            return stringBuilder.ToString();
        }

        public EmbedBuilder CreateTeamDetailsEmbed(ScoreboardDetails teamScore, CompleteScoreboardSummary completeScoreboard = null, ScoreboardFilterInfo peerFilter = default(ScoreboardFilterInfo), TimeZoneInfo timeZone = null)
        {
            if (teamScore == null)
            {
                throw new ArgumentNullException(nameof(teamScore));
            }

            var builder = new EmbedBuilder()
                          .WithTimestamp(teamScore.SnapshotTimestamp)
                          .WithTitle("Team " + teamScore.TeamId)
                          .WithDescription(Utilities.JoinNonNullNonEmpty(" | ", CompetitionLogic.GetEffectiveDivisionDescriptor(teamScore.Summary), teamScore.Summary.Tier, LocationResolution.GetFullName(teamScore.Summary.Location)))
                          .WithFooter(ScoreRetrieverMetadata.StaticSummaryLine);

            if (!string.IsNullOrWhiteSpace(teamScore.Comment))
            {
                builder.Description += "\n";
                builder.Description += teamScore.Comment;
            }

            // scoreboard link
            if (teamScore.OriginUri != null)
            {
                builder.Url = teamScore.OriginUri.ToString();
            }

            // location -> flag in thumbnail
            Uri flagUrl = LocationResolution?.GetFlagUriOrNull(teamScore.Summary.Location);
            if (flagUrl != null)
            {
                builder.ThumbnailUrl = flagUrl.ToString();
            }

            // tier -> color on side
            // colors borrowed from AFA's spreadsheet
            if (teamScore.Summary.Tier.HasValue)
            {
                switch (teamScore.Summary.Tier.Value)
                {
                    case Tier.Platinum:
                        // tweaked from AFA spreadsheet to be more distinct from silver
                        // AFA original is #DAE3F3
                        builder.WithColor(183, 201, 243);
                        break;
                    case Tier.Gold:
                        builder.WithColor(0xFF, 0xE6, 0x99);
                        break;
                    case Tier.Silver:
                        // tweaked from AFA spreadsheet to be more distinct from platinum, and to look less white
                        // AFA original is #F2F2F2
                        builder.WithColor(0x90, 0x90, 0x90);
                        break;
                }
            }

            // TODO image lookup for location? e.g. thumbnail with flag?
            foreach (var item in teamScore.Images)
            {
                string penaltyAppendage = item.Penalties != 0 ? " - " + Utilities.Pluralize("penalty", item.Penalties) : string.Empty;
                bool overtime = (item.Warnings & ScoreWarnings.TimeOver) == ScoreWarnings.TimeOver;
                bool multiimage = (item.Warnings & ScoreWarnings.MultiImage) == ScoreWarnings.MultiImage;
                string warningAppendage = string.Empty;
                const string multiImageStr = "**M**ultiple Instances";
                const string overTimeStr = "**T**ime Exceeded";
                if (overtime || multiimage)
                {
                    warningAppendage = "\nWarnings: ";
                }
                if (overtime && multiimage)
                {
                    warningAppendage += multiImageStr + ", " + overTimeStr;
                }
                else if (overtime || multiimage)
                {
                    warningAppendage += multiimage ? multiImageStr : overTimeStr;
                }
                string vulnsString = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.VulnerabilityDisplay, item.VulnerabilitiesFound, item.VulnerabilitiesRemaining + item.VulnerabilitiesFound) ? $" ({item.VulnerabilitiesFound}/{item.VulnerabilitiesFound + item.VulnerabilitiesRemaining} vulns{penaltyAppendage})" : string.Empty;
                string playTimeStr = ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.TimeDisplay, item.PlayTime) ? $" in {item.PlayTime.ToHoursMinutesSecondsString()}" : string.Empty;
                builder.AddField('`' + item.ImageName + $": {ScoreRetrieverMetadata.FormattingOptions.FormatScore(item.Score)}pts`", $"{ScoreRetrieverMetadata.FormattingOptions.FormatScore(item.Score)} points{vulnsString}{playTimeStr}{warningAppendage}");
            }

            string totalScoreTimeAppendage = string.Empty;
            if (ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.TimeDisplay, teamScore.Summary.PlayTime))
            {
                totalScoreTimeAppendage = $" in {teamScore.Summary.PlayTime.ToHoursMinutesSecondsString()}";
            }

            string totalPointsAppendage = string.Empty;
            if (teamScore.Images.All(i => i.PointsPossible != -1))
            {
                totalPointsAppendage =
                    "\n" + ScoreRetrieverMetadata.FormattingOptions.FormatScore(teamScore.Images.Sum(i => i.PointsPossible)) +
                    " points possible";
            }

            builder.AddInlineField("Total Score", $"{ScoreRetrieverMetadata.FormattingOptions.FormatScore(teamScore.Summary.TotalScore)} points" + totalScoreTimeAppendage + totalPointsAppendage);

            if (teamScore.Summary.Warnings != 0)
            {
                string warningsOverview = null;
                if ((teamScore.Summary.Warnings & ScoreWarnings.MultiImage) == ScoreWarnings.MultiImage)
                {
                    warningsOverview = "Multiple Instances";
                }

                if ((teamScore.Summary.Warnings & ScoreWarnings.TimeOver) == ScoreWarnings.TimeOver)
                {
                    if (warningsOverview == null)
                    {
                        warningsOverview = "";
                    }
                    else
                    {
                        warningsOverview += "\n";
                    }

                    warningsOverview += "Time Limit Exceeded";
                }

                if ((teamScore.Summary.Warnings & ScoreWarnings.Withdrawn) == ScoreWarnings.Withdrawn)
                {
                    if (warningsOverview == null)
                    {
                        warningsOverview = "";
                    }
                    else
                    {
                        warningsOverview += "\n";
                    }

                    warningsOverview += "Score Withdrawn";
                }

                builder.AddInlineField("Warnings", warningsOverview);
            }

            var timingFieldBuilder = new StringBuilder();

            if (ScoreFormattingOptions.EvaluateNumericDisplay(ScoreRetrieverMetadata.FormattingOptions.TimeDisplay, teamScore.ScoreTime))
            {
                if (timingFieldBuilder.Length > 0)
                {
                    timingFieldBuilder.AppendLine();
                }
                timingFieldBuilder.AppendFormat("Score achieved in {0}", teamScore.ScoreTime.ToHoursMinutesSecondsString());
            }

            DateTimeOffset? maxImageTime = null;
            if (teamScore.ImageScoresOverTime != null)
            {
                foreach (var dto in teamScore.ImageScoresOverTime.Select(x => x.Value.Keys.Last()))
                {
                    if (!maxImageTime.HasValue || dto > maxImageTime.Value)
                    {
                        maxImageTime = dto;
                    }
                }
            }

            if (maxImageTime.HasValue)
            {
                if (timingFieldBuilder.Length > 0)
                {
                    timingFieldBuilder.AppendLine();
                }
                timingFieldBuilder.Append("Score last updated:");
                timingFieldBuilder.AppendLine().Append("\u00A0\u00A0"); //NBSP x2

                if (DateTimeOffset.UtcNow - maxImageTime.Value < TimeSpan.FromDays(1))
                {
                    timingFieldBuilder.Append((DateTimeOffset.UtcNow - maxImageTime.Value).ToLongString(showSeconds: false)).Append(" ago");
                }
                else
                {
                    DateTimeOffset timestamp = timeZone == null ? maxImageTime.Value : TimeZoneInfo.ConvertTime(maxImageTime.Value, timeZone);
                    timingFieldBuilder.AppendFormat("{0:g} ", timestamp);
                    timingFieldBuilder.Append(TimeZoneNames.TZNames.GetAbbreviationsForTimeZone(timeZone.Id, "en-US").Generic.Replace("UTC Time", "UTC"));
                }
            }

            if (timingFieldBuilder.Length > 0)
            {
                builder.AddInlineField("Timing", timingFieldBuilder.ToString());
            }

            if (completeScoreboard != null)
            {
                var peerList = completeScoreboard.Clone().WithFilter(peerFilter).TeamList;
                int myIndexInPeerList = peerList.IndexOfWhere(x => x.TeamId == teamScore.TeamId);

                double rawPercentile = 1.0 - ((myIndexInPeerList + 1.0) / peerList.Count);
                int multipliedPercentile = (int)Math.Round(rawPercentile * 1000);
                int intPart = multipliedPercentile / 10;
                int floatPart = multipliedPercentile % 10;

                builder.AddInlineField("Rank", $"{Utilities.AppendOrdinalSuffix(myIndexInPeerList + 1)} place\n{(floatPart == 0 ? Utilities.AppendOrdinalSuffix(intPart) : $"{intPart}.{Utilities.AppendOrdinalSuffix(floatPart)}")} percentile");

                StringBuilder marginBuilder = new StringBuilder();
                if (myIndexInPeerList > 0)
                {
                    double marginUnderFirst = peerList[0].TotalScore - teamScore.Summary.TotalScore;
                    marginBuilder.AppendLine($"{ScoreRetrieverMetadata.FormattingOptions.FormatLabeledScoreDifference(marginUnderFirst)} under 1st place");
                }
                if (myIndexInPeerList >= 2)
                {
                    double marginUnderAbove = peerList[myIndexInPeerList - 1].TotalScore - teamScore.Summary.TotalScore;
                    marginBuilder.AppendLine($"{ScoreRetrieverMetadata.FormattingOptions.FormatLabeledScoreDifference(marginUnderAbove)} under {Utilities.AppendOrdinalSuffix(myIndexInPeerList)} place");
                }
                if (myIndexInPeerList < peerList.Count - 1)
                {
                    double marginAboveUnder = teamScore.Summary.TotalScore - peerList[myIndexInPeerList + 1].TotalScore;
                    marginBuilder.AppendLine($"{ScoreRetrieverMetadata.FormattingOptions.FormatLabeledScoreDifference(marginAboveUnder)} above {Utilities.AppendOrdinalSuffix(myIndexInPeerList + 2)} place");
                }

                // TODO division- and round-specific margins
                builder.AddInlineField("Margin", marginBuilder.ToString());

                StringBuilder standingFieldBuilder = new StringBuilder();

                IList<ScoreboardSummaryEntry> subPeer = null;
                string subPeerLabel = null;

                if (!peerFilter.Category.HasValue && teamScore.Summary.Category.HasValue)
                {
                    var myCategory = teamScore.Summary.Category.Value;
                    subPeer = peerList.Where(x => x.Category == myCategory).ToIList();
                    subPeerLabel = " in category";
                }
                else if (peerFilter.Location == null && teamScore.Summary.Location != null)
                {
                    var myLocation = teamScore.Summary.Location;
                    subPeer = peerList.Where(x => x.Location == myLocation).ToIList();
                    subPeerLabel = " in state";
                }

                if (subPeerLabel != null)
                {
                    standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(subPeer.IndexOfWhere(x => x.TeamId == teamScore.TeamId) + 1) + " of " + Utilities.Pluralize("peer team", subPeer.Count) + subPeerLabel);
                }

                standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(myIndexInPeerList + 1) + " of " + Utilities.Pluralize("peer team", peerList.Count));

                // if peer teams != div+tier teams
                if ((peerFilter.Category.HasValue || peerFilter.Location != null) && peerFilter.Tier.HasValue)
                {
                    // tier ranking, differing from peer ranking
                    var tierTeams = completeScoreboard.Clone().WithFilter(new ScoreboardFilterInfo(peerFilter.Division, peerFilter.Tier)).TeamList;
                    standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(tierTeams.IndexOfWhere(x => x.TeamId == teamScore.TeamId) + 1) + " of " + Utilities.Pluralize("team", tierTeams.Count) + " in tier");
                }
                if (peerFilter.Category.HasValue || peerFilter.Location != null || peerFilter.Tier.HasValue)
                {
                    // division ranking, differing from peer ranking
                    var divTeams = completeScoreboard.Clone().WithFilter(new ScoreboardFilterInfo(peerFilter.Division, null)).TeamList;
                    standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(divTeams.IndexOfWhere(x => x.TeamId == teamScore.TeamId) + 1) + " of " + Utilities.Pluralize("team", divTeams.Count) + " in division");
                }
                builder.AddInlineField("Standing", standingFieldBuilder.ToString());
            }

            if (teamScore.ImageScoresOverTime != null)
            {
                builder.ImageUrl = CreateChartUrl(teamScore);
            }

            if (teamScore.Summary.Advancement.HasValue)
            {
                builder.AddInlineField("Advancement", teamScore.Summary.Advancement.Value.ToStringCamelCaseToSpace());
            }

            return builder;
        }

        private string CreateChartUrl(ScoreboardDetails teamScore)
        {
            int min = teamScore.ImageScoresOverTime.Values.SelectMany(x => x.Values).Select(x => x == null || x > 0 ? 0 : x.Value).Min();
            int max = 100;

            Dictionary<string, string> imageScoreStrings = teamScore.ImageScoresOverTime.ToDictionary(image => image.Key, image => string.Join(',', image.Value.Values.Select(x => x?.ToString() ?? "-9999")));

            var data = string.Join("|", imageScoreStrings.Values);
            var images = string.Join("|", imageScoreStrings.Keys);

            // last scoring tick timestamp - first scoring tick timestamp
            TimeSpan graphPlayTime = teamScore.ImageScoresOverTime.Values.Select(x => x.Keys.Last()).Max() - teamScore.ImageScoresOverTime.Values.Select(x => x.Keys.First()).Min();
            long graphPlayTimeTicks = graphPlayTime.Ticks;

            string axisLabels = $"|0:00|{new TimeSpan(graphPlayTimeTicks / 6).ToHoursMinutesSecondsString(1)}|{new TimeSpan(graphPlayTimeTicks / 3).ToHoursMinutesSecondsString(1)}|{new TimeSpan(graphPlayTimeTicks / 2).ToHoursMinutesSecondsString(1)}|{new TimeSpan(graphPlayTimeTicks / 3 * 2).ToHoursMinutesSecondsString(1)}|{new TimeSpan(graphPlayTimeTicks / 6 * 5).ToHoursMinutesSecondsString(1)}|{graphPlayTime.ToHoursMinutesSecondsString(1)}";

            string queryString = $"cht=lc&chco=F44336,03A9F4,4CAF50,FFEB3B&chf=bg,s,2F3136&chdls=FFFFFF,16&chxs=1,FFFFFF&chs=900x325&chd=t:{WebUtility.UrlEncode(data)}&chxt=x,y&chxl=0:{WebUtility.UrlEncode(axisLabels)}&chdl={WebUtility.UrlEncode(images)}&chxs=1,FFFFFF,12,1,lt,FFFFFF%7C0,FFFFFF,12,0,lt,FFFFFF&chtt=Team+{WebUtility.UrlEncode(teamScore.TeamId.ToString())}&chts=FFFFFF,20&chls=3%7C3%7C3%7C3&chg=16.66666,10&chds={min},{max}&chxr=1,{min},{max}";

            return $"https://chart.googleapis.com/chart?{queryString}";
        }
    }
}
