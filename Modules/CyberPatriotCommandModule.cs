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

        public ScoreboardMessageBuilderService ScoreEmbedBuilder { get; set; }

        [Command("team"), Alias("getteam")]
        public async Task GetTeamAsync(TeamId team)
        {
            ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(team);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining team score.");
            }
            await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore).Build());
        }

        [Command("scoreboard"), Alias("leaderboard", "top")]
        public async Task GetLeaderboardAsync()
        {
            CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(Division.Open, "High School");
            if (teamScore == null)
            {
                throw new Exception("Error obtaining scoreboard.");
            }
            await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore));
        }
    }
}