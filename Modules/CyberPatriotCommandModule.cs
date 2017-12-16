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

        public ScoreboardEmbedBuilderService ScoreEmbedBuilder { get; set; }

        [Command("team"), Alias("getteam")]
        public async Task GetTeamAsync(TeamId team)
        {
            ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetails(team);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining team score.");
            }
            await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateEmbed(teamScore).Build());
        }
    }
}