using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Discord;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class ScoreboardMessageBuilderService
    {
        public FlagProviderService FlagProvider { get; set; }

        public ScoreboardMessageBuilderService(FlagProviderService flagProvider)
        {
            FlagProvider = flagProvider;
        }

        public string CreateTopLeaderboardEmbed(CompleteScoreboardSummary scoreboard, TimeZoneInfo timeZone = null, int pageNumber = 1, int pageSize = 15)
        {
            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
            }

            // can't easily check upper bound
            if (--pageNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            var stringBuilder = new StringBuilder();
            stringBuilder.Append("**CyberPatriot Scoreboard");
            if (scoreboard.DivisionFilter.HasValue)
            {
                stringBuilder.Append(", ").Append(Utilities.ToStringCamelCaseToSpace(scoreboard.DivisionFilter.Value));
                if (scoreboard.TierFilter != null)
                {
                    stringBuilder.Append(' ').Append(scoreboard.TierFilter);
                }
            }
            else if (scoreboard.TierFilter != null)
            {
                stringBuilder.Append(", ").Append(scoreboard.TierFilter).Append(" Tier");
            }
            if (pageNumber > 0)
            {
                stringBuilder.Append(" (Page ").Append(pageNumber + 1).Append(')');
            }
            stringBuilder.AppendLine("**");
            stringBuilder.Append("*As of: ");
            DateTimeOffset timestamp = timeZone == null ? scoreboard.SnapshotTimestamp : TimeZoneInfo.ConvertTime(scoreboard.SnapshotTimestamp, timeZone);
            stringBuilder.AppendFormat("{0:g}", timestamp);
            stringBuilder.Append(' ').Append(timeZone?.DisplayName ?? "UTC").AppendLine("*");
            stringBuilder.AppendLine("```");

            scoreboard.TeamList.Skip(pageNumber * pageSize).Take(pageSize)
            .ForEach((team, i) =>
            {
                stringBuilder.AppendFormat("#{0,-4}{1}{2,4}{6,6}{7,10}{3,16}{4,7:hh\\:mm}{5,4}", i + 1 + (pageNumber * pageSize), team.TeamId, team.Location, team.TotalScore, team.PlayTime, team.Warnings.ToConciseString(), team.Division.ToConciseString(), team.Tier).AppendLine();
            });
            stringBuilder.AppendLine("```");
            if (scoreboard.OriginUri != null)
            {
                stringBuilder.AppendLine(scoreboard.OriginUri.ToString());
            }
            return stringBuilder.ToString();
        }

        public EmbedBuilder CreateTeamDetailsEmbed(ScoreboardDetails teamScore)
        {
            if (teamScore == null)
            {
                throw new ArgumentNullException(nameof(teamScore));
            }

            var builder = new EmbedBuilder()
                .WithTimestamp(teamScore.SnapshotTimestamp)
                .WithTitle(teamScore.Summary.Division.ToStringCamelCaseToSpace() + (teamScore.Summary.Tier == null ? string.Empty : (" " + teamScore.Summary.Tier)) + " Team " + teamScore.Summary.TeamId);
            if (teamScore.OriginUri != null)
            {
                builder.Url = teamScore.OriginUri.ToString();
            }
            string flagUrl = FlagProvider.GetFlagUri(teamScore.Summary.Location);
            if (flagUrl != null)
            {
                builder.ThumbnailUrl = flagUrl;
            }
            // TODO image lookup for location? e.g. thumbnail with flag?
            foreach (var item in teamScore.Images)
            {
                string penaltyAppendage = item.Penalties > 0 ? " - " + item.Penalties + " penalties" : string.Empty;
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
                builder.AddField('`' + item.ImageName + $": {item.Score}pts`", $"{item.Score} points ({item.VulnerabilitiesFound}/{item.VulnerabilitiesFound + item.VulnerabilitiesRemaining} vulns{penaltyAppendage}) in {item.PlayTime:hh\\:mm}{warningAppendage}");
            }

            return builder;
        }
    }
}