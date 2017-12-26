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

        public PreferenceProviderService Preferences { get; set; }

        [Command("team"), Alias("getteam"), Summary("Gets score information for a given team.")]
        public async Task GetTeamAsync(TeamId teamId)
        {
            ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teamId);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining team score.");
            }
            await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null))).Build());
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard.")]
        public async Task GetLeaderboardAsync(int pageNumber = 1)
        {
            CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
            if (teamScore == null)
            {
                throw new Exception("Error obtaining scoreboard.");
            }
            await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User)));
        }
    }
}