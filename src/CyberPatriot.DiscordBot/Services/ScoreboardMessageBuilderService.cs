using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Discord;
using CyberPatriot.DiscordBot;
using CyberPatriot.Models;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public class ScoreboardMessageBuilderService
    {
        public FlagProviderService FlagProvider { get; set; }
        // hack, needed for formatting
        public IScoreRetrievalService ScoreRetriever { get; set; }
        public ICompetitionRoundLogicService CompetitionLogic { get; set; }

        public ScoreboardMessageBuilderService(FlagProviderService flagProvider, IScoreRetrievalService scoreRetriever, ICompetitionRoundLogicService competitionLogic)
        {
            FlagProvider = flagProvider;
            ScoreRetriever = scoreRetriever;
            CompetitionLogic = competitionLogic;
        }

        public class CustomFiltrationInfo
        {
            public Func<ScoreboardSummaryEntry, bool> Predicate { get; set; } = _ => true;
            public string FilterDescription { get; set; }
        }

        public string CreateTopLeaderboardEmbed(CompleteScoreboardSummary scoreboard, CustomFiltrationInfo customFilter = null, TimeZoneInfo timeZone = null, int pageNumber = 1, int pageSize = 15)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            Func<ScoreboardSummaryEntry, bool> predicate = customFilter?.Predicate == null ? _ => true : customFilter.Predicate;

            int pageCount = (int)(Math.Ceiling((((double)scoreboard.TeamList.Count(predicate)) / pageSize)));

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

            if (customFilter?.FilterDescription != null)
            {
                stringBuilder.Append(", ").Append(customFilter.FilterDescription);
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
            scoreboard.TeamList.Where(predicate).Skip(pageNumber * pageSize).Take(pageSize)
                .Select((team, i) => stringBuilder.AppendFormat("#{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + 1 + (pageNumber * pageSize), team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine())
                .Consume();
            stringBuilder.AppendLine("```");
            if (scoreboard.OriginUri != null)
            {
                stringBuilder.AppendLine(scoreboard.OriginUri.ToString());
            }
            return stringBuilder.ToString();
        }

        public string CreatePeerLeaderboardEmbed(CompleteScoreboardSummary scoreboard, ScoreboardDetails teamDetails, TimeZoneInfo timeZone = null, int topTeams = 3, int nearbyTeams = 5)
        {
            var peerTeams = CompetitionLogic.GetPeerTeams(ScoreRetriever.Round, scoreboard, teamDetails.Summary);
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("**CyberPatriot Scoreboard**");
            DateTimeOffset timestamp = timeZone == null ? scoreboard.SnapshotTimestamp : TimeZoneInfo.ConvertTime(scoreboard.SnapshotTimestamp, timeZone);
            stringBuilder.AppendFormat("*Competing against: {0} | As of: ", teamDetails.TeamId);
            stringBuilder.AppendFormat("{0:g}", timestamp);
            stringBuilder.Append(' ').Append(timeZone == null ? "UTC" : TimeZoneNames.TZNames.GetAbbreviationsForTimeZone(timeZone.Id, "en-US").Generic).AppendLine("*");
            stringBuilder.AppendLine("```bash");
            // zero-based rank of the given team
            int pos = peerTeams.IndexOfWhere(team => team.TeamId == teamDetails.TeamId);
            if (pos < nearbyTeams + topTeams + 1)
            {
                peerTeams.Take(nearbyTeams + pos + 1)
                          .Select((team, i) => stringBuilder.AppendFormat("{8}{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + 1, team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier, team.TeamId == teamDetails.TeamId ? ">" : "#").AppendLine())
                          .Consume();
            }
            else
            {
                peerTeams.Take(topTeams)
                          .Select((team, i) => stringBuilder.AppendFormat("#{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + 1, team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine())
                          .Consume();
                stringBuilder.AppendLine("...");
                peerTeams.Skip(pos - nearbyTeams)
                          .Take(nearbyTeams)
                          .Select((team, i) => stringBuilder.AppendFormat("#{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + pos - nearbyTeams + 1, team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine())
                          .Consume();
                stringBuilder.AppendFormat(">{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", pos + 1, teamDetails.TeamId, teamDetails.Summary.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(teamDetails.Summary.TotalScore), teamDetails.Summary.PlayTime, teamDetails.Summary.Warnings.ToConciseString(), teamDetails.Summary.Division.ToConciseString(), teamDetails.Summary.Tier).AppendLine();
                // since pos and i are both zero-based, i + pos + 2 returns correct team rank for teams after given team
                peerTeams.Skip(pos + 1)
                          .Take(nearbyTeams)
                          .Select((team, i) => stringBuilder.AppendFormat("#{0,-5}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + pos + 2, team.TeamId, team.Location, ScoreRetriever.FormattingOptions.FormatScoreForLeaderboard(team.TotalScore), team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine())
                          .Consume();
            }

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
                          .WithTitle("Team " + teamScore.TeamId)
                          .WithDescription(Utilities.JoinNonNullNonEmpty(" | ", CompetitionLogic.GetEffectiveDivisionDescriptor(teamScore.Summary), teamScore.Summary.Tier, teamScore.Summary.Location))
                          .WithFooter(ScoreRetriever.StaticSummaryLine);

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
                        builder.WithColor(0xF2, 0xF2, 0xF2);
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

            string totalPointsAppendage = string.Empty;
            if (teamScore.Images.All(i => i.PointsPossible != -1))
            {
                totalPointsAppendage =
                    "\n" + ScoreRetriever.FormattingOptions.FormatScore(teamScore.Images.Sum(i => i.PointsPossible)) +
                    " points possible";
            }

            builder.AddInlineField("Total Score", $"{ScoreRetriever.FormattingOptions.FormatScore(teamScore.Summary.TotalScore)} points" + totalScoreTimeAppendage + totalPointsAppendage);
            if (peerScoreboard != null)
            {
                // don't pollute upstream
                CompleteScoreboardSummary totalDivisionScoreboard = peerScoreboard.Clone().WithFilter(teamScore.Summary.Division, null);

                IList<ScoreboardSummaryEntry> myDivMyTierTeams = totalDivisionScoreboard.Clone().WithFilter(teamScore.Summary.Division, teamScore.Summary.Tier).TeamList;
                IList<ScoreboardSummaryEntry> peerTeams = CompetitionLogic.GetPeerTeams(ScoreRetriever.Round, totalDivisionScoreboard, teamScore.Summary);
                if (peerTeams.Count > 0)
                {
                    int myIndexInPeerList = peerTeams.IndexOfWhere(entr => entr.TeamId == teamScore.TeamId);

                    double rawPercentile = 1.0 - (((double)peerTeams.Count(peer => peer.TotalScore >= teamScore.Summary.TotalScore)) / peerTeams.Count);
                    int multipliedPercentile = (int)Math.Round(rawPercentile * 1000);
                    int intPart = multipliedPercentile / 10;
                    int floatPart = multipliedPercentile % 10;

                    builder.AddInlineField("Rank", $"{Utilities.AppendOrdinalSuffix(myIndexInPeerList + 1)} place\n{(floatPart == 0 ? Utilities.AppendOrdinalSuffix(intPart) : $"{intPart}.{Utilities.AppendOrdinalSuffix(floatPart)}")} percentile");

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

                    StringBuilder standingFieldBuilder = new StringBuilder();
                    standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(myIndexInPeerList + 1) + " of " + Utilities.Pluralize("peer team", peerTeams.Count));

                    // non-peer rankings use parentheticals - peer rankings are used for the rest of the logic
                    // if peer teams != div+tier teams
                    if (myDivMyTierTeams.Count != peerTeams.Count && !myDivMyTierTeams.Select(t => t.TeamId).OrderBy(t => t).SequenceEqual(peerTeams.Select(t => t.TeamId).OrderBy(t => t)))
                    {
                        // tier ranking, differing from peer ranking
                        standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(myDivMyTierTeams.IndexOfWhere(cand => cand.TeamId == teamScore.TeamId) + 1) + " of " + Utilities.Pluralize("team", myDivMyTierTeams.Count) + " in tier");
                    }
                    if (totalDivisionScoreboard.TeamList.Count > peerTeams.Count)
                    {
                        // division ranking, differing from peer ranking
                        standingFieldBuilder.AppendLine(Utilities.AppendOrdinalSuffix(totalDivisionScoreboard.TeamList.IndexOfWhere(cand => cand.TeamId == teamScore.TeamId) + 1) + " of " + Utilities.Pluralize("team", totalDivisionScoreboard.TeamList.Count) + " in division");
                    }
                    builder.AddInlineField("Standing", standingFieldBuilder.ToString());
                }
            }

            if (teamScore.ImageScoresOverTime != null)
            {
                builder.ImageUrl = CreateChartUrl(teamScore);
            }

            return builder;
        }

        private string CreateChartUrl(ScoreboardDetails teamScore)
        {
            int min = teamScore.ImageScoresOverTime.Values.SelectMany(x => x.Values).Min(x => x ?? 0);
            int max = 100;

            Dictionary<string, string> imageScoreStrings = teamScore.ImageScoresOverTime.ToDictionary(image => image.Key, image => string.Join(',', image.Value.Values.Select(x => x?.ToString() ?? "-9999")));

            var data = string.Join("|", imageScoreStrings.Values);
            var images = string.Join("|", imageScoreStrings.Keys);

            string axisLabels = $"|0:00|{new TimeSpan(teamScore.Summary.PlayTime.Ticks / 6):h\\:mm}|{new TimeSpan(teamScore.Summary.PlayTime.Ticks / 3):h\\:mm}|{new TimeSpan(teamScore.Summary.PlayTime.Ticks / 2):h\\:mm}|{new TimeSpan(teamScore.Summary.PlayTime.Ticks / 3 * 2):h\\:mm}|{new TimeSpan(teamScore.Summary.PlayTime.Ticks / 6 * 5):h\\:mm}|{teamScore.Summary.PlayTime:h\\:mm}";

            string queryString = $"cht=lc&chco=F44336,03A9F4,4CAF50,FFEB3B&chf=bg,s,32363B&chdls=FFFFFF,16&chxs=1,FFFFFF&chs=900x325&chd=t:{WebUtility.UrlEncode(data)}&chxt=x,y&chxl=0:{WebUtility.UrlEncode(axisLabels)}&chdl={WebUtility.UrlEncode(images)}&chxs=1,FFFFFF,12,1,lt,FFFFFF%7C0,FFFFFF,12,0,lt,FFFFFF&chtt=Team+{WebUtility.UrlEncode(teamScore.TeamId.ToString())}&chts=FFFFFF,20&chls=3%7C3%7C3%7C3&chg=16.66666,10&chds={min},{max}&chxr=1,{min},{max}";

            return $"https://chart.googleapis.com/chart?{queryString}"; ;
        }
    }
}