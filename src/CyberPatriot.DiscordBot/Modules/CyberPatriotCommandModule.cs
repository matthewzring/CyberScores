#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;
using System.Linq;
using CyberPatriot.BitmapProvider;
using CyberPatriot.Models.Serialization;
using CyberPatriot.DiscordBot.Models;
using CyberPatriot.Services;

namespace CyberPatriot.DiscordBot.Modules
{
    public class CyberPatriotCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreRetrievalService { get; set; }

        public ScoreboardMessageBuilderService ScoreEmbedBuilder { get; set; }

        public PreferenceProviderService Preferences { get; set; }

        public IGraphProviderService GraphProvider { get; set; }

        public ICompetitionRoundLogicService CompetitionRoundLogicService { get; set; }

        public ILocationResolutionService LocationResolutionService { get; set; }

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
                await ReplyAsync(string.Empty, embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore,
                    completeScoreboard: await ScoreRetrievalService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false),
                    peerFilter: CompetitionRoundLogicService.GetPeerFilter(ScoreRetrievalService.Round, teamScore.Summary),
                    timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false)).Build()).ConfigureAwait(false);
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
                    await targetWriter.WriteLineAsync($"{team.TeamId},{team.Division.ToStringCamelCaseToSpace()},{(!team.Category.HasValue ? string.Empty : team.Category.Value.ToCanonicalName())},{team.Location},{(team.Tier.HasValue ? team.Tier.Value.ToString() : string.Empty)},{team.ImageCount},{team.PlayTime.ToHoursMinutesSecondsString()},{ScoreRetrievalService.Metadata.FormattingOptions.FormatScore(team.TotalScore)},{team.Warnings.ToConciseString()}").ConfigureAwait(false);
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

        [Command("rank"), Alias("getrank"), HideCommandHelp]
        public Task GetTeamWithRankCommandAsync(int rank) => GetTeamWithRankAsync(rank, null, null, null);

        [Command("rank"), Alias("getrank"), HideCommandHelp]
        public Task GetTeamWithRankCommandAsync(int rank, LocationCode location) => GetTeamWithRankAsync(rank, location, null, null);

        [Command("rank"), Alias("getrank"), HideCommandHelp]
        public Task GetTeamWithRankCommandAsync(int rank, DivisionWithCategory division) => GetTeamWithRankAsync(rank, null, division, null);

        [Command("rank"), Alias("getrank"), HideCommandHelp]
        public Task GetTeamWithRankCommandAsync(int rank, LocationCode location, DivisionWithCategory division) => GetTeamWithRankAsync(rank, location, division, null);

        [Command("rank"), Alias("getrank"), HideCommandHelp]
        public Task GetTeamWithRankCommandAsync(int rank, DivisionWithCategory division, Tier tier) => GetTeamWithRankAsync(rank, null, division, tier);

        [Command("rank"), Alias("getrank"), Summary("Gets score information for the team with the given rank.")]
        public Task GetTeamWithRankCommandAsync(
            [Summary("The placement to display. The team at this placement will be displayed. Must be inclusively between 1 and the number of teams.")] int rank,
            [Summary("If provided, the location within which ranking should be calculated."), AlterParameterDisplay(DisplayAsOptional = true)] LocationCode location,
            [Summary("If provided, the division (and optionally category) within which ranking should be calculated."), AlterParameterDisplay(DisplayAsOptional = true)] DivisionWithCategory division,
            [Summary("If provided, the tier within which ranking should be calculated."), AlterParameterDisplay(DisplayAsOptional = true, SubordinateTo = "division")] Tier tier) => GetTeamWithRankAsync(rank, location, division, tier);

        public async Task GetTeamWithRankAsync(int rank, string location, DivisionWithCategory? divisionAndCat, Tier? tier)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (rank < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank));
                }

                var filter = new ScoreboardFilterInfo(divisionAndCat?.Division, tier, divisionAndCat?.Category, location);

                var teams = await ScoreRetrievalService.GetScoreboardAsync(filter).ConfigureAwait(false);
                System.Collections.Generic.IEnumerable<ScoreboardSummaryEntry> teamList = teams.TeamList;
                
                var team = teamList.Skip(rank - 1).First();
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(team.TeamId).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }

                string classSpec = Utilities.JoinNonNullNonEmpty(", ",
                    LocationResolutionService.GetFullNameOrNull(location),
                    filter.Division.HasValue ? (filter.Division.Value.ToStringCamelCaseToSpace() + " Division") : null,
                    filter.Category?.ToCanonicalName(),
                    tier.HasValue ? (tier.Value.ToStringCamelCaseToSpace() + " Tier") : null);

                await ReplyAsync(
                    "**" + Utilities.AppendOrdinalSuffix(rank) + " place " + (classSpec.Length == 0 ? "overall" : "in " + classSpec) + ": " + team.TeamId + "**",
                    embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore,
                        completeScoreboard: await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(null, null)).ConfigureAwait(false),
                        peerFilter: CompetitionRoundLogicService.GetPeerFilter(ScoreRetrievalService.Round, team),
                        timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false)).Build()).ConfigureAwait(false);
            }
        }

        #endregion
        #region Percentile

        [Command("percentile"), HideCommandHelp]
        public Task GetTeamWithPercentileCommandAsync([InclusiveRange(0, 100)] double rank) => GetTeamWithPercentileAsync(rank);

        [Command("percentile"), HideCommandHelp]
        public Task GetTeamWithPercentileCommandAsync([InclusiveRange(0, 100)] double rank, DivisionWithCategory division) => GetTeamWithPercentileAsync(rank, division);

        [Command("percentile"), Summary("Gets score information for the team at the given percentile rank.")]
        public Task GetTeamWithPercentileCommandAsync(
            [Summary("The percentile rank, from 0 to 100, for which information should be displayed."), InclusiveRange(0, 100)] double rank,
            [Summary("If provided, the division (and optionally category) within which the percentile will be calculated."), AlterParameterDisplay(DisplayAsOptional = true)] DivisionWithCategory division,
            [Summary("If provided, the tier within which the percentile will be calculated."), AlterParameterDisplay(DisplayAsOptional = true, SubordinateTo = "division")] Tier tier) => GetTeamWithPercentileAsync(rank, division, tier);

        public async Task GetTeamWithPercentileAsync(double rank, DivisionWithCategory? divAndCat = null, Tier? tier = null)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (rank < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(rank));
                }

                var teams = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(divAndCat?.Division, tier, divAndCat?.Category, null)).ConfigureAwait(false);
                
                // teams list in descending order
                int expectedIndex = ((int)Math.Round(((100 - rank) / 100) * teams.TeamList.Count)).Clamp(0, teams.TeamList.Count);
                ScoreboardDetails teamScore = await ScoreRetrievalService.GetDetailsAsync(teams.TeamList[expectedIndex].TeamId).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining team score.");
                }

                await ReplyAsync(string.Empty,
                    embed: ScoreEmbedBuilder.CreateTeamDetailsEmbed(teamScore,
                        completeScoreboard: await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(null, null)).ConfigureAwait(false),
                        peerFilter: CompetitionRoundLogicService.GetPeerFilter(ScoreRetrievalService.Round, teamScore.Summary), 
                        timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false)).Build()).ConfigureAwait(false);
            }
        }

        #endregion
        #region Scoreboard

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(int pageNumber = 1) => GetLeaderboardImplementationAsync(null, null, null, null, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(LocationCode location, int pageNumber = 1) => GetLeaderboardImplementationAsync(location, null, null, null, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(DivisionWithCategory division, int pageNumber = 1) => GetLeaderboardImplementationAsync(null, division.Category, division.Division, null, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(LocationCode location, DivisionWithCategory division, int pageNumber = 1) => GetLeaderboardImplementationAsync(location, division.Category, division.Division, null, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(Tier tier, int pageNumber = 1) => GetLeaderboardImplementationAsync(null, null, null, tier, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(LocationCode location, Tier tier, int pageNumber = 1) => GetLeaderboardImplementationAsync(location, null, null, tier, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), HideCommandHelp]
        public Task GetLeaderboardAsync(DivisionWithCategory division, Tier tier, int pageNumber = 1) => GetLeaderboardImplementationAsync(null, division.Category, division.Division, tier, pageNumber);

        [Command("scoreboard"), Alias("leaderboard"), Summary("Returns the current CyberPatriot leaderboard.")]
        public Task GetLeaderboardAsync(
            [Summary("The location to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] LocationCode location,
            [Summary("The division (and optionally category) to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] DivisionWithCategory division,
            [Summary("The tier to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] Tier tier,
            int pageNumber = 1) => GetLeaderboardImplementationAsync(location, division.Category, division.Division, tier, pageNumber);

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

                CompleteScoreboardSummary peerScoreboard = await ScoreRetrievalService.GetScoreboardAsync(CompetitionRoundLogicService.GetPeerFilter(ScoreRetrievalService.Round, teamDetails.Summary)).ConfigureAwait(false);

                await ReplyAsync(ScoreEmbedBuilder.CreatePeerLeaderboardEmbed(teamDetails.TeamId, peerScoreboard, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }

        public async Task GetLeaderboardImplementationAsync(string location, ServiceCategory? category, Division? divFilter, Tier? tierFilter, int pageNumber)
        {
            using (Context.Channel.EnterTypingState())
            {
                CompleteScoreboardSummary teamScore = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(divFilter, tierFilter, category, location)).ConfigureAwait(false);
                if (teamScore == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }
                
                await ReplyAsync(ScoreEmbedBuilder.CreateTopLeaderboardEmbed(teamScore, pageNumber: pageNumber, timeZone: await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false))).ConfigureAwait(false);
            }
        }
        #endregion
        #region Image Scoreboard

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, null, null, null, null, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, LocationCode location, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, location, null, null, null, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, DivisionWithCategory division, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, null, division.Category, division.Division, null, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, LocationCode location, DivisionWithCategory division, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, location, division.Category, division.Division, null, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, Tier tier, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, null, null, null, tier, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, LocationCode location, Tier tier, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, location, null, null, tier, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard"), HideCommandHelp]
        public Task GetImageLeaderboardAsync(string imageName, DivisionWithCategory division, Tier tier, int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, null, division.Category, division.Division, tier, pageNumber);

        [Command("imagescoreboard"), Alias("imageleaderboard")]
        [Summary("Gets the leaderboard of scores on the given image, optionally filtered by various parameters. Only supported on offline score providers.")]
        public Task GetImageLeaderboardAsync(
            [Summary("The image for which information should be retrieved. The name must be an exact match.")] string imageName,
            [Summary("The location to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] LocationCode location,
            [Summary("The division (and optionally category) to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] DivisionWithCategory division,
            [Summary("The tier to which scoreboard display should be filtered, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] Tier tier,
            int pageNumber = 1) => GetImageLeaderboardImplementationAsync(imageName, location, division.Category, division.Division, tier, pageNumber);

        public async Task GetImageLeaderboardImplementationAsync(string image, string location, ServiceCategory? category, Division? division, Tier? tier, int pageNumber)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (!ScoreRetrievalService.Metadata.SupportsInexpensiveDetailQueries)
                {
                    throw new InvalidOperationException("Image-specific queries cannot be performed on online score providers. Please use datasource to specify an offline score provider.");
                }

                System.Collections.Generic.IEnumerable<ScoreboardSummaryEntry> teams = (await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(division, tier)).ConfigureAwait(false))?.TeamList;
                if (teams == null)
                {
                    throw new Exception("Error obtaining scoreboard.");
                }

                if (category.HasValue)
                {
                    var catVal = category.Value;
                    teams = teams.Where(t => t.Category == catVal);
                }

                if (location != null)
                {
                    teams = teams.Where(t => t.Location == location);
                }

                string filterDesc = Utilities.JoinNonNullNonEmpty(", ",
                    !division.HasValue ? null : division.Value.ToStringCamelCaseToSpace() + " Division",
                    !tier.HasValue ? null : tier.Value.ToStringCamelCaseToSpace() + " Tier",
                    !category.HasValue ? null : category.Value.ToCanonicalName(),
                    LocationResolutionService.GetFullNameOrNull(location));

                var downloadTasks = teams.Select(t => ScoreRetrievalService.GetDetailsAsync(t.TeamId)).ToArray();

                try
                {
                    await Task.WhenAll(downloadTasks).ConfigureAwait(false);
                }
                catch
                {
                    // oh well?
                }

                await ReplyAsync(
                    message: ScoreEmbedBuilder.CreateImageLeaderboardEmbed(downloadTasks.Where(t => t.IsCompletedSuccessfully).Select(
                        t => new System.Collections.Generic.KeyValuePair<ScoreboardSummaryEntry, ScoreboardImageDetails>(t.Result.Summary,
                            t.Result.Images.SingleOrDefault(i => i.ImageName.Equals(image, StringComparison.InvariantCultureIgnoreCase))))
                        .Where(kvp => kvp.Value != null).OrderByDescending(kvp => kvp.Value.Score).ThenBy(kvp => kvp.Value.PlayTime),
                        filterDescription: filterDesc, pageNumber: pageNumber)).ConfigureAwait(false);
            }
        }
        #endregion
        #region Histogram

        private const string HistogramCommandName = "histogram";
        // Unfortunately since it has to be a compile-time constant we can't refactor out the alias list
        //private static readonly string[] HistogramAliases = { "scoregraph", "scorestats", "statistics" };

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync() => GenerateHistogramAsync(null, null, null, null);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName) => GenerateHistogramAsync(null, null, imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(LocationCode location) => GenerateHistogramAsync(null, null, null, location);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, LocationCode location) => GenerateHistogramAsync(null, null, imageName, location);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(DivisionWithCategory div) => GenerateHistogramAsync(div, null, null, null);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, DivisionWithCategory div) => GenerateHistogramAsync(div, null, imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(LocationCode location, DivisionWithCategory div) => GenerateHistogramAsync(div, null, null, location);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, LocationCode location, DivisionWithCategory div) => GenerateHistogramAsync(div, null, imageName, location);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(Tier tier) => GenerateHistogramAsync(null, tier, null, null);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, Tier tier) => GenerateHistogramAsync(null, tier, imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(LocationCode location, Tier tier) => GenerateHistogramAsync(null, tier, null, location);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, LocationCode location, Tier tier) => GenerateHistogramAsync(null, tier, imageName, location);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(DivisionWithCategory div, Tier tier) => GenerateHistogramAsync(div, tier, null, null);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        [Priority(-1)]
        public Task HistogramCommandAsync(string imageName, DivisionWithCategory div, Tier tier) => GenerateHistogramAsync(div, tier, imageName, null);

        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), HideCommandHelp]
        public Task HistogramCommandAsync(LocationCode location, DivisionWithCategory div, Tier tier) => GenerateHistogramAsync(div, tier, null, location);
        [Command(HistogramCommandName), Alias("scoregraph", "scorestats", "statistics"), Summary("Generates a histogram of scores on the current CyberPatriot leaderboard.")]
        [Priority(-1)]
        public Task HistogramCommandAsync(
            [Summary("The exact name of the image for which scores should be retrieved. If unspecified, total scores will be used. Only supported on offline score parameters. See the `datasource` command."), AlterParameterDisplay(DisplayAsOptional = true)] string imageName,
            [Summary("The location (either two-letter postal code or three-letter country code, in all caps) to filter the analysis to, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] LocationCode location,
            [Summary("The division (and optionally category) to filter the analysis to, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] DivisionWithCategory div,
            [Summary("The tier to filter the analysis to, if provided."), AlterParameterDisplay(DisplayAsOptional = true)] Tier tier
            ) => GenerateHistogramAsync(div, tier, imageName, location);
        
        public async Task GenerateHistogramAsync(DivisionWithCategory? divisionWithCategory, Tier? tier, string imageName, string locCode)
        {
            using (Context.Channel.EnterTypingState())
            {
                if (imageName != null && !ScoreRetrievalService.Metadata.SupportsInexpensiveDetailQueries)
                {
                    throw new InvalidOperationException("Per-image histograms are not supported on online score providers. Use the `datasource` command to select an offline score provider.");
                }

                CompleteScoreboardSummary scoreboard = await ScoreRetrievalService.GetScoreboardAsync(new ScoreboardFilterInfo(divisionWithCategory?.Division, tier)).ConfigureAwait(false);
                decimal[] data = await scoreboard.TeamList
                    .Conditionally(locCode != null, tle => tle.Where(t => t.Location == locCode))
                    .Conditionally(divisionWithCategory?.Category != null, tle => tle.Where(t => t.Category == divisionWithCategory.Value.Category))
                    .TernaryAsync(imageName == null,
                        x => x.Select(datum => (decimal)datum.TotalScore).ToAsyncEnumerable(),
                        x => x.Select(t => ScoreRetrievalService.GetDetailsAsync(t.TeamId))
                              .ToTaskResultEnumerable()
                              .Select(t => t.Images.SingleOrDefault(i => i.ImageName == imageName))
                              .Where(i => i != null)
                              .Select(i => (decimal)i.Score))
                    .ToArrayAsync().ConfigureAwait(false);
                Array.Sort(data);

                Models.User userSettings = await Preferences.Database.FindOneAsync<Models.User>(u => u.Id == Context.User.Id).ConfigureAwait(false);

                ColorPresets.HistogramColorPreset histogramColorScheme = (userSettings?.DiscordTheme ?? "dark") == "light" ? ColorPresets.DiscordLight : ColorPresets.DiscordDark;

                using (var memStr = new System.IO.MemoryStream())
                {
                    await GraphProvider.WriteHistogramPngAsync(data, "Score", "Frequency", datum => datum.ToString("0.0#"), histogramColorScheme, memStr).ConfigureAwait(false);
                    memStr.Position = 0;

                    var histogramEmbed = new EmbedBuilder()
                                         .WithTitle("CyberPatriot Score Analysis")
                                         .WithDescription(Utilities.JoinNonNullNonEmpty(" | ",
                                             imageName.AppendPrependIfNonEmpty("`"),
                                             divisionWithCategory?.Division.ToStringCamelCaseToSpace(),
                                             tier,
                                             divisionWithCategory?.Category?.ToCanonicalName(),
                                             LocationResolutionService.GetFullNameOrNull(locCode))
                                           .CoalesceBlank("All Teams"))
                                         .AddInlineField("Teams", data.Length)
                                         .AddInlineField("Mean", $"{data.Average():0.##}")
                                         .AddInlineField("Standard Deviation", $"{data.StandardDeviation():0.##}")
                                         .AddInlineField("First Quartile", $"{data.Take(data.Length / 2).ToArray().Median():0.##}")
                                         .AddInlineField("Median", $"{data.Median():0.##}")
                                         .AddInlineField("Third Quartile", $"{data.Skip(data.Length / 2).ToArray().Median():0.##}")
                                         .AddInlineField("Min Score", $"{data.Min()}")
                                         .AddInlineField("Max Score", $"{data.Max()}")
                                         .WithTimestamp(scoreboard.SnapshotTimestamp)
                                         .WithFooter(ScoreRetrievalService.Metadata.StaticSummaryLine)
                                         .WithImageUrl("attachment://histogram.png"); // Discord API requirement to use the uploaded histogram

                    await Context.Channel.SendFileAsync(memStr, "histogram.png", embed: histogramEmbed.Build()).ConfigureAwait(false);
                }
            }
        }

        #endregion
    }
}
