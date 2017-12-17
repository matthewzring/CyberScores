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

        public IDataPersistenceService Database { get; set; }

        [Command("team"), Alias("getteam")]
        public async Task GetTeamAsync(TeamId team)
        {
            ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(team);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining team score.");
            }
            await ReplyAsync(string.Empty, embed: (await ScoreEmbedBuilder.CreateTeamDetailsEmbedAsync(teamScore, await ScoreRetrievalService.GetScoreboardAsync(teamScore.Summary.Division, teamScore.Summary.Tier))).Build());
        }

        [Command("scoreboard"), Alias("leaderboard", "top")]
        public async Task GetLeaderboardAsync(int pageNumber = 1)
        {
            CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(null, null);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining scoreboard.");
            }
            string tzId = (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id)).TimeZone;
            TimeZoneInfo tz = null;
            if (tzId != null)
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            }
            await ReplyAsync(await ScoreEmbedBuilder.CreateTopLeaderboardEmbedAsync(teamScore, pageNumber: pageNumber, timeZone: tz));
        }
    }
}