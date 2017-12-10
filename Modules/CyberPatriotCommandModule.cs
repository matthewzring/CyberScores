using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Modules
{
    public class CyberPatriotCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreRetrievalService { get; set; }

        public FlagProviderService FlagProviderService { get; set; }

        [Command("team"), Alias("getteam")]
        public async Task GetTeamAsync(TeamId team)
        {
            ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetails(team);
            var builder = new EmbedBuilder()
                .WithTimestamp(teamScore.Summary.SnapshotTimestamp)
                .WithTitle(teamScore.Summary.Division.ToStringCamelCaseToSpace() + (teamScore.Summary.IsFakeTier() ? string.Empty : (" " + teamScore.Summary.Tier)) + " Team " + team);
            if (teamScore.OriginUri != null)
            {
                builder.Url = teamScore.OriginUri.ToString();
            }
            string flagUrl = FlagProviderService.GetFlagUri(teamScore.Summary.Location);
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
            await ReplyAsync(string.Empty, embed: builder.Build());
        }
    }
}