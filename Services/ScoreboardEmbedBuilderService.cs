using System;
using Discord;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class ScoreboardEmbedBuilderService
    {
        public FlagProviderService FlagProvider { get; set; }

        public ScoreboardEmbedBuilderService(FlagProviderService flagProvider)
        {
            FlagProvider = flagProvider;
        }

        public EmbedBuilder CreateEmbed(ScoreboardDetails teamScore)
        {
            if (teamScore == null)
            {
                throw new ArgumentNullException(nameof(teamScore));
            }

            var builder = new EmbedBuilder()
                .WithTimestamp(teamScore.Summary.SnapshotTimestamp)
                .WithTitle(teamScore.Summary.Division.ToStringCamelCaseToSpace() + (teamScore.Summary.IsFakeTier() ? string.Empty : (" " + teamScore.Summary.Tier)) + " Team " + teamScore.Summary.TeamId);
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