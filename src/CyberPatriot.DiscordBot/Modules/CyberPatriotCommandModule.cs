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

        [Command("rank"), Alias("getrank"), Summary("Gets score information for the team with the given rank.")]
        public Task GetTeamWithRankCommandAsync(int rank) => GetTeamWithRankAsync(rank);

        [Command("rank"), Alias("getrank"), Summary("Gets score information for the team with the given rank in the given division.")]
        public Task GetTeamWithRankCommandAsync(int rank, Division division) => GetTeamWithRankAsync(rank, division);

        [Command("rank"), Alias("getrank"), Summary("Gets score information for the team with the given rank in the given division and tier.")]
        public Task GetTeamWithRankCommandAsync(int rank, Division division, Tier tier) => GetTeamWithRankAsync(rank, division, tier);

        public async Task GetTeamWithRankAsync(int rank, Division? division = null, Tier? tier = null)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (rank < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank));
                }

                var teams = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier));
                var team = teams.TeamList[rank - 1];
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(team.TeamId);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null))).Build());
            }
        }

        [Command("percentile"), Summary("Gets score information for the team at the given percentile rank.")]
        public Task GetTeamWithPercentileCommandAsync([InclusiveRange(0, 100)] double rank) => GetTeamWithPercentileAsync(rank);

        [Command("percentile"), Summary("Gets score information for the team at the given percentile rank in the given division.")]
        public Task GetTeamWithPercentileCommandAsync([InclusiveRange(0, 100)] double rank, Division division) => GetTeamWithPercentileAsync(rank, division);

        [Command("percentile"), Summary("Gets score information for the team at the given percentile rank in the given division and tier.")]
        public Task GetTeamWithPercentileCommandAsync([InclusiveRange(0, 100)] double rank, Division division, Tier tier) => GetTeamWithPercentileAsync(rank, division, tier);

        public async Task GetTeamWithPercentileAsync(double rank, Division? division = null, Tier? tier = null)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (rank < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank));
                }

                var teams = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier));
                // teams list in descending order
                int expectedIndex = ((int)Math.Round(((100 - rank) / 100) * teams.TeamList.Count)).Clamp(0, teams.TeamList.Count);
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teams.TeamList[expectedIndex].TeamId);
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
        public async Task GetLeaderboardAsync(Division division, Tier tier, int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
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

        #region Histogram

        [Command("histogram"), Alias("scoregraph", "scorestats"), Summary("Generates a histogram of all scores on the current CyberPatriot leaderboard. Scores are processed as overall scores unless an image name is specified, in which case only scores on that image are analyzed.")]
        public Task HistogramCommandAsync(string imageName = null) => GenerateHistogramAsync(ScoreboardFilterInfo.NoFilter, imageName);

        [Command("histogram"), Alias("scoregraph", "scorestats"), Summary("Generates a histogram of the given division's scores on the current CyberPatriot leaderboard. Scores are processed as overall scores unless an image name is specified, in which case only scores on that image are analyzed.")]
        public Task HistogramCommandAsync(Division div, string imageName = null) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, null), imageName);

        [Command("histogram"), Alias("scoregraph", "scorestats"), Summary("Generates a histogram of the given tier's scores on the current CyberPatriot leaderboard. Scores are processed as overall scores unless an image name is specified, in which case only scores on that image are analyzed.")]
        public Task HistogramCommandAsync(Division div, Tier tier, string imageName = null) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, tier), imageName);

        public async Task GenerateHistogramAsync(ScoreboardFilterInfo filter, string imageName)
        {
            var descBuilder = new System.Text.StringBuilder();
            if (filter.Division.HasValue)
            {
                descBuilder.Append(' ').Append(filter.Division.Value.ToStringCamelCaseToSpace());
            }
            if (filter.Tier.HasValue)
            {
                descBuilder.Append(' ').Append(filter.Tier.Value);
            }
            if (imageName != null)
            {
                throw new NotSupportedException("Per-image histograms are not yet supported.");

                // unreachable code - not implemented on the data-aggregation/filter side, but this code Should Work:tm: for constructing the title
#pragma warning disable 0162
                if (descBuilder.Length > 0)
                {
                    descBuilder.Append(": ");
                }
                descBuilder.Append(imageName);
#pragma warning restore 0162
            }

            decimal[] data = (await ScoreRetrievalService.GetScoreboardAsync(filter)).TeamList
                // nasty hack
                .Select(datum => decimal.TryParse(ScoreRetrievalService.FormattingOptions.FormatScore(datum.TotalScore), out decimal d) ? d : datum.TotalScore)
                .OrderBy(d => d).ToArray();
            using (var memStr = new System.IO.MemoryStream())
            {
                await GraphProvider.WriteHistogramPngAsync(data, "Score", "Frequency", (lower, upper) => $"{lower:0.0#} - {upper:0.0#}", BitmapProvider.Color.White, BitmapProvider.Color.Blue, BitmapProvider.Color.Black, memStr);
                memStr.Position = 0;
                await Context.Channel.SendFileAsync(memStr, "histogram.png", $"__**CyberPatriot Score Analysis" + descBuilder.ToString().Trim().AppendPrependIfNonEmpty(": ", "") + "**__\n"
                    + $"**Teams:** {data.Length}\n**Mean:** {data.Average():0.##}\n**Median:** {data.Median():0.##}");
            }
        }

        #endregion
    }
}