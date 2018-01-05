using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;
using System.Linq;
using CyberPatriot.BitmapProvider;

namespace CyberPatriot.DiscordBot.Modules
{
    public class CyberPatriotCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreRetrievalService { get; set; }

        public ScoreboardMessageBuilderService ScoreEmbedBuilder { get; set; }

        public PreferenceProviderService Preferences { get; set; }

        public IGraphProviderService GraphProvider { get; set; }

        public ICompetitionRoundLogicService CompetitionRoundLogicService { get; set; }

        [Command("team"), Alias("getteam"), Summary("Gets score information for a given team.")]
        public async Task GetTeamAsync(TeamId teamId)
        {
            using (Context.Channel.EnterTypingState())
            {
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teamId);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null))).Build());
            }
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard.")]
        public async Task GetLeaderboardAsync(int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User)));
            }
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given division.")]
        public async Task GetLeaderboardAsync(Division division, int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, null));
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User)));
            }
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given division and tier.")]
        public async Task GetLeaderboardAsync(Division division, string tier, int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (!string.IsNullOrWhiteSpace(tier))
                {
                    // transform some common mistakes with the tier
                    // first letter capital
                    char[] tierCharArray = tier.ToCharArray();
                    tierCharArray[0] = char.ToUpper(tierCharArray[0]);
                    tier = new string(tierCharArray);
                }
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier));
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User)));
            }
        }

        [Command("scoreboard"), Alias("leaderboard", "peerboard", "peerleaderboard", "peerscoreboard"), Summary("Shows the given team's placement on the current CyberPatriot leaderboard consisting only of its peers."), Priority(5)]
        public async Task GeneratePeerLeaderboardAsync(TeamId team)
        {
            using (Context.Channel.EnterTypingState())
            {
                ScoreboardDetails teamDetails = await ScoreRetrievalService.GetDetailsAsync(team);
                if (teamDetails == null)
                {
                    throw new Exception("Error obtaining team score.");
                }

                CompleteScoreboardSummary scoreboard = await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);

                await ReplyAsync(ScoreEmbedBuilder.CreatePeerLeaderboardEmbed(scoreboard, teamDetails, await Preferences.GetTimeZoneAsync(Context.Guild, Context.User)));
            }
        }

        [Command("histogram"), Alias("scoregraph", "scorestats"), Summary("Generates a histogram of the scores on the current CyberPatriot leaderboard.")]
        public async Task GenerateHistogramAsync()
        {
            decimal[] data = (await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter)).TeamList
                // nasty hack
                .Select(datum => decimal.TryParse(ScoreRetrievalService.FormattingOptions.FormatScore(datum.TotalScore), out decimal d) ? d : datum.TotalScore)
                .OrderBy(d => d).ToArray();
            using (var memStr = new System.IO.MemoryStream())
            {
                await GraphProvider.WriteHistogramPngAsync(data, "Score", "Frequency", BitmapProvider.Color.White, BitmapProvider.Color.Blue, BitmapProvider.Color.Black, memStr);
                memStr.Position = 0;
                await Context.Channel.SendFileAsync(memStr, "histogram.png", $"**CyberPatriot Score Analysis**\n"
                    + $"**Teams:** {data.Length}\n**Mean:** {data.Average()}\n**Median:** {data[data.Length / 2]}");
            }
        }
    }
}