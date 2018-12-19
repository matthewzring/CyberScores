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
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teamId).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, CompetitionRoundLogicService.GetRankingInformation(ScoreRetrievalService.Round, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null)).ConfigureAwait(false), teamScore.Summary)).Build()).ConfigureAwait(false);
            }
        }

        [Command("exportcsv"), Alias("csv", "csvexport"), Summary("Exports the scoreboard summary as a CSV file.")]
        public async Task ExportSummaryCommandAsync()
        {
            using (Context.Channel.EnterTypingState())
            {
                var scoreboardTask = ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);

                var targetWriter = new System.IO.StringWriter();
                await targetWriter.WriteLineAsync("TeamId,Division,Category,Location,Tier,ImageCount,PlayTime,Score,Warnings").ConfigureAwait(false);
                CompleteScoreboardSummary scoreboard = await scoreboardTask.ConfigureAwait(false);
                foreach (var team in scoreboard.TeamList)
                {
                    await targetWriter.WriteLineAsync($"{team.TeamId},{team.Division.ToStringCamelCaseToSpace()},{team.Category ?? string.Empty},{team.Location},{(team.Tier.HasValue ? team.Tier.Value.ToString() : string.Empty)},{team.ImageCount},{team.PlayTime.ToHoursMinutesString()},{ScoreRetrievalService.Metadata.FormattingOptions.FormatScore(team.TotalScore)},{team.Warnings.ToConciseString()}").ConfigureAwait(false);
                }

                TimeZoneInfo tz = await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false);
                string tzAbbr = tz.GetAbbreviations().Generic;
                DateTimeOffset snapshotTimestamp = TimeZoneInfo.ConvertTime(scoreboard.SnapshotTimestamp, tz);
                using (var targetStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(targetWriter.GetStringBuilder().ToString())))
                {
                    await Context.Channel.SendFileAsync(targetStream, "scoreboard.csv", $"Scoreboard summary CSV export\nScore timestamp: {snapshotTimestamp:g} {tzAbbr}\nExported: {TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz):g} {tzAbbr}").ConfigureAwait(false);
                }
            }
        }

        #region Rank

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

                var teams = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier)).ConfigureAwait(false);
                var team = teams.TeamList[rank - 1];
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(team.TeamId).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, CompetitionRoundLogicService.GetRankingInformation(ScoreRetrievalService.Round, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null)).ConfigureAwait(false), teamScore.Summary)).Build()).ConfigureAwait(false);
            }
        }

        #endregion
        #region Percentile

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

                var teams = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier)).ConfigureAwait(false);
                // teams list in descending order
                int expectedIndex = ((int)Math.Round(((100 - rank) / 100) * teams.TeamList.Count)).Clamp(0, teams.TeamList.Count);
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teams.TeamList[expectedIndex].TeamId).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore, CompetitionRoundLogicService.GetRankingInformation(ScoreRetrievalService.Round, await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(teamScore.Summary.Division, null)).ConfigureAwait(false), teamScore.Summary)).Build()).ConfigureAwait(false);
            }
        }

        #endregion
        #region Scoreboard

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard.")]
        public async Task GetLeaderboardAsync(int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given division.")]
        public async Task GetLeaderboardAsync(Division division, int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, null)).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given division and tier.")]
        public async Task GetLeaderboardAsync(Division division, Tier tier, int pageNumber = 1)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier)).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        [Command("scoreboard"), Alias("leaderboard", "peerboard", "peerleaderboard", "peerscoreboard"), Summary("Shows the given team's placement on the current CyberPatriot leaderboard consisting only of its peers."), Priority(5)]
        public async Task GeneratePeerLeaderboardAsync(TeamId team)
        {
            using (Context.Channel.EnterTypingState())
            {
                ScoreboardDetails teamDetails = await ScoreRetrievalService.GetDetailsAsync(team).ConfigureAwait(false);
                if (teamDetails == null)
                {
                    throw new Exception("Error obtaining team score.");
                }

                CompleteScoreboardSummary scoreboard = await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);

                await ReplyAsync(ScoreEmbedBuilder.CreatePeerLeaderboardEmbed(teamDetails.TeamId, scoreboard, CompetitionRoundLogicService.GetPeerTeams(ScoreRetrievalService.Round, scoreboard, teamDetails.Summary), timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        #endregion
        #region Specialized scoreboards
        private bool CategoryEquals(ScoreboardSummaryEntry team, string category)
        {
            if (team.Category == null)
            {
                return category == null;
            }

            string teamCategory = team.Category.Trim().ToLowerInvariant();
            category = category.Trim().ToLowerInvariant();
            if (teamCategory.Equals(category))
            {
                return true;
            }
            if (teamCategory.StartsWith(category))
            {
                // starts to get really soft from this comparison onwards
                return true;
            }
            if (teamCategory.StartsWithWhereElement(c => c != ' ', category))
            {
                return true;
            }

            if (teamCategory.Replace(" ", "").Contains(category.Replace(" ", "")))
            {
                return true;
            }

            string[] teamCategoryWords = teamCategory.Split(' ');
            if (category.Length > 1 && category == string.Join("", teamCategoryWords.Where(w => w != "jrotc").Select(w => w[0])))
            {
                // abbreviation, e.g. CAP or AF
                return true;
            }
        }

        public async Task GetServiceLeaderboardImplementationAsync(string category, Tier? tier, int pageNumber)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(Division.AllService, tier)).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }

                // validate category
                string realCategory = teamScore.TeamList.Where(t => CategoryEquals(t, category)).Select(t => t.Category).Distinct().SingleIfOne();
                if (realCategory == null)
                {
                    throw new ArgumentException("The given category was not found - it was either ambiguous or invalid.", nameof(category));
                }

                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, customFilter: new ScoreboardMessageBuilderService.CustomFiltrationInfo()
                {
                    Predicate = t => t.Category == realCategory,
                    FilterDescription = realCategory
                }, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        public async Task GetLocationLeaderboardImplementationAsync(string location, ScoreboardFilterInfo filterInfo, int pageNumber)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(filterInfo).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }

                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, customFilter: new ScoreboardMessageBuilderService.CustomFiltrationInfo()
                {
                    Predicate = t => t.Location == location,
                    FilterDescription = location // TODO full name of state?
                }, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        [Command("servicescoreboard"), Alias("allservicescoreboard", "serviceleaderboard", "allserviceleaderboard", "categoryscoreboard", "categoryleaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given category of All Service teams.")]
        public Task GetServiceLeaderboardAsync(string category, int pageNumber = 1) => GetServiceLeaderboardImplementationAsync(category, null, pageNumber);

        [Command("servicescoreboard"), Alias("allservicescoreboard", "serviceleaderboard", "allserviceleaderboard", "categoryscoreboard", "categoryleaderboard"), Summary("Returns the current CyberPatriot leaderboard for the given category of All Service teams in the given tier.")]
        public Task GetServiceLeaderboardAsync(string category, Tier tier, int pageNumber = 1) => GetServiceLeaderboardImplementationAsync(category, tier, pageNumber);

        [Command("locationscoreboard"), Alias("statescoreboard", "locationleaderboard", "stateleaderboard"), Summary("Returns the current CyberPatriot leaderboard for teams in the given location.")]
        public Task GetLocationLeaderboardAsync(LocationCode location, int pageNumber = 1) => GetLocationLeaderboardImplementationAsync(location, ScoreboardFilterInfo.NoFilter, pageNumber);
        [Command("locationscoreboard"), Alias("statescoreboard", "locationleaderboard", "stateleaderboard"), Summary("Returns the current CyberPatriot leaderboard for teams in the given location and division.")]
        public Task GetLocationLeaderboardAsync(LocationCode location, Division division, int pageNumber = 1) => GetLocationLeaderboardImplementationAsync(location, new ScoreboardFilterInfo(division, null), pageNumber);
        [Command("locationscoreboard"), Alias("statescoreboard", "locationleaderboard", "stateleaderboard"), Summary("Returns the current CyberPatriot leaderboard for teams in the given location, division, and tier.")]
        public Task GetLocationLeaderboardAsync(LocationCode location, Division division, Tier tier, int pageNumber = 1) => GetLocationLeaderboardImplementationAsync(location, new ScoreboardFilterInfo(division, tier), pageNumber);
        #endregion
        #region Histogram

        private const string HistogramCommandName = "histogram";
        // Unfortunately since it has to be a compile-time constant we can't refactor out the alias list
        //private static readonly string[] HistogramAliases = { "scoregraph", "scorestats", "statistics" };

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of all scores on the current CyberPatriot leaderboard.")]
        public Task HistogramCommandAsync() => GenerateHistogramAsync(ScoreboardFilterInfo.NoFilter, null, null);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of all scores for the given image on the current CyberPatriot leaderboard.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync(string imageName) => GenerateHistogramAsync(ScoreboardFilterInfo.NoFilter, imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of all scores on the current CyberPatriot leaderboard for teams in the given state.")]
        public Task HistogramCommandAsync([Summary("The location (either two-letter postal code or three-letter country code, in all caps) to filter the analysis to.")] LocationCode location) => GenerateHistogramAsync(ScoreboardFilterInfo.NoFilter, null, location);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of all scores for the given image on the current CyberPatriot leaderboard for teams in the given state.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync([OverrideTypeReader(typeof(LocationTypeReader))] string location, string imageName) => GenerateHistogramAsync(ScoreboardFilterInfo.NoFilter, imageName, location);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given division's scores on the current CyberPatriot leaderboard.")]
        public Task HistogramCommandAsync(Division div) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, null), null, null);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given division's scores for the given image on the current CyberPatriot leaderboard.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync(Division div, string imageName) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, null), imageName, null);


        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given division's scores within the given state on the current CyberPatriot leaderboard.")]
        public Task HistogramCommandAsync([Summary("The location (either two-letter postal code or three-letter country code, in all caps) to filter the analysis to.")] LocationCode location, Division div) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, null), null, location);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given division's scores for the given image within the given state on the current CyberPatriot leaderboard.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync([OverrideTypeReader(typeof(LocationTypeReader))] string location, Division div, string imageName) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, null), imageName, location);


        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given tier's scores on the current CyberPatriot leaderboard.")]
        public Task HistogramCommandAsync(Division div, Tier tier) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, tier), null, null);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given tier's scores for the given image on the current CyberPatriot leaderboard.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync(Division div, Tier tier, string imageName) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, tier), imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given tier's scores within the given state on the current CyberPatriot leaderboard.")]
        public Task HistogramCommandAsync([Summary("The location (either two-letter postal code or three-letter country code, in all caps) to filter the analysis to.")] LocationCode location, Division div, Tier tier) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, tier), null, location);
        // [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of the given tier's scores for the given image within the given state on the current CyberPatriot leaderboard.")]
        // [Priority(-1)]
        // public Task HistogramCommandAsync([OverrideTypeReader(typeof(LocationTypeReader))] string location, Division div, Tier tier, string imageName) => GenerateHistogramAsync(new ScoreboardFilterInfo(div, tier), imageName, location);


        public async Task GenerateHistogramAsync(ScoreboardFilterInfo filter, string imageName, string locCode)
        {
            using (Context.Channel.EnterTypingState())
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

                CompleteScoreboardSummary scoreboard = await ScoreRetrievalService.GetScoreboardAsync(filter).ConfigureAwait(false);
                decimal[] data = scoreboard.TeamList
                    .Conditionally(locCode != null, tle => tle.Where(t => t.Location == locCode))
                    .Select(datum => (decimal)datum.TotalScore)
                    .OrderBy(d => d).ToArray();
                using (var memStr = new System.IO.MemoryStream())
                {
                    await GraphProvider.WriteHistogramPngAsync(data, "Score", "Frequency", datum => datum.ToString("0.0#"), BitmapProvider.Color.Parse("#32363B"), BitmapProvider.Color.Parse("#7289DA"), BitmapProvider.Color.White, BitmapProvider.Color.Gray, memStr).ConfigureAwait(false);
                    memStr.Position = 0;

                    // This shouldn't be necessary, Discord's API supports embedding attached images
                    // BUT discord.net does not, see #796
                    var httpClient = new System.Net.Http.HttpClient();
                    var imagePostMessage = new System.Net.Http.MultipartFormDataContent();
                    var innerStream = new System.Net.Http.StreamContent(memStr);
                    innerStream.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("form-data")
                    {
                        Name = "\"file\"",
                        FileName = "\"histogram.png\"",
                        FileNameStar = null
                    };
                    innerStream.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
                    imagePostMessage.Add(innerStream);
                    var reqMesg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://pasteboard.co/upload");
                    reqMesg.Content = imagePostMessage;
                    reqMesg.Headers.Referrer = new Uri("https://pasteboard.co/");
                    Task<System.Net.Http.HttpResponseMessage> uploadUrlResponseTask = httpClient.SendAsync(reqMesg);

                    var histogramEmbed = new EmbedBuilder()
                                         .WithTitle("CyberPatriot Score Analysis")
                                         .WithDescription(Utilities.JoinNonNullNonEmpty(" | ", filter.Division?.ToStringCamelCaseToSpace(), filter.Tier, locCode).CoalesceBlank("All Teams"))
                                         .AddInlineField("Teams", data.Length)
                                         .AddInlineField("Mean", $"{data.Average():0.##}")
                                         .AddInlineField("Standard Deviation", $"{data.StandardDeviation():0.##}")
                                         .AddInlineField("First Quartile", $"{data.Take(data.Length / 2).ToArray().Median():0.##}")
                                         .AddInlineField("Median", $"{data.Median():0.##}")
                                         .AddInlineField("Third Quartile", $"{data.Skip(data.Length / 2).ToArray().Median():0.##}")
                                         .AddInlineField("Min Score", $"{data.Min()}")
                                         .AddInlineField("Max Score", $"{data.Max()}");
                    var uploadResponse = await (await uploadUrlResponseTask.ConfigureAwait(false)).Content.ReadAsStringAsync().ConfigureAwait(false);
                    histogramEmbed = histogramEmbed
                                        .WithImageUrl(
                                            "https://cdn.pbrd.co/images/" + Newtonsoft.Json.JsonConvert.DeserializeAnonymousType(uploadResponse, new { url = "", fileName = "" }).fileName
                                         )
                                         .WithTimestamp(scoreboard.SnapshotTimestamp)
                                         .WithFooter(ScoreRetrievalService.Metadata.StaticSummaryLine);

                    await Context.Channel.SendMessageAsync("", embed: histogramEmbed).ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}