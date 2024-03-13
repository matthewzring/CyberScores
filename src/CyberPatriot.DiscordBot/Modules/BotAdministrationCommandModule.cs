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
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using CyberPatriot.DiscordBot.Services;
using System.Net.Http;
using System.Text;
using CyberPatriot.Services;

namespace CyberPatriot.DiscordBot.Modules
{
    public class BotAdministrationCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreService { get; set; }
        public IDataPersistenceService Database { get; set; }

        [Command("ping"), Summary("Pings the bot. Responds with the internal socket client's estimated latency, if available.")]
        public async Task PingAsync()
        {
            var messageContents = new System.Text.StringBuilder();
            messageContents.AppendLine("**__Pong!__**");
            if (Context.Client is Discord.WebSocket.DiscordSocketClient socketClient)
            {
                messageContents.AppendFormat("Socket Latency: `{0}ms`", socketClient.Latency).AppendLine();
            }
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            // TODO I don't know how the stopwatch behaves if we call methods on it across threads
            // Until we figure that out all calls on the Stopwatch should be on the same thread
            // As I understand it, the ConfigureAwait(true) does NOT guarantee this, it merely gurantees the same SynchronizationContext
            // But that should be better in the event of an issue, and it's the default "sync-like" code behavior, meaning I wouldn't expect an issue
            // TODO figure this out properly 
            IUserMessage myMessage = await ReplyAsync(messageContents.ToString()).ConfigureAwait(true);
            stopwatch.Stop();
            messageContents.AppendFormat("Message RTT: `{0}ms`", stopwatch.ElapsedMilliseconds);
            // we can ConfigureAwait(false) here because we're done with the stopwatch
            await myMessage.ModifyAsync(mp => mp.Content = messageContents.ToString()).ConfigureAwait(false);
        }

        [Command("info"), Alias("about", "invite"), Summary("Returns information about the bot, including an invite link.")]
        public async Task InfoAsync()
        {
            var appinfo = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            await ReplyAsync(string.Empty, embed: await new EmbedBuilder()
                .WithAuthor(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrlOrDefault())
                .WithDescription("Hello! I am **CyberScores**, a bot for interaction with the CyberPatriot scoreboard.\n"
                + $"Type `@CyberScores help` for a list of my commands.\n\n"
                + $"<:globe_with_meridians:1031956460248256523> [Github](https://github.com/matthewzring/CyberScores)\n"
                + $"<:link:1031853124887007252> [Invite](https://cyberscores.cypat.gg/invite)\n"
                + $"<:discord:1031849735226667008> [Support Server](https://discord.gg/cyberpatriot)")
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Prefix").WithValue((Context.Guild != null ? (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id).ConfigureAwait(false))?.Prefix?.AppendPrepend("`") : null) ?? Context.Client.CurrentUser.Mention))
                .AddField(fb => fb.WithIsInline(true).WithName("Score Provider").WithValue(ScoreService.Metadata.StaticSummaryLine))
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Teams").WithValue((await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false)).TeamList.Count))
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Guilds").WithValue((await Context.Client.GetGuildsAsync().ConfigureAwait(false)).Count))
                .AddField(fb => fb.WithIsInline(true).WithName("Uptime").WithValue(string.Join("\n",
                    (DateTimeOffset.UtcNow - CyberPatriotDiscordBot.StartupTime).ToLongString()
                    .Split(' ')
                    .Select((v, i) => new { Value = v, Index = i })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(y => y.Value)).Select(x => string.Join(" ", x)))))
                .BuildAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Command("kill"), Alias("die", "shutdown"), RequireTeamOwner]
        [Summary("Terminates the bot instance.")]
        public async Task KillAsync()
        {
            await ReplyAsync("Goodbye!").ConfigureAwait(false);
            Environment.Exit(0);
        }

        [Command("setavatar"), Alias("seticon"), RequireTeamOwner]
        [Summary("Sets the avatar for the bot.")]
        public async Task SetIconAsync(string iconUrl)
        {
            var iconDownloader = new System.Net.Http.HttpClient();
            string tempFileTargetName = Path.GetTempFileName();
            using (var iconStream = await iconDownloader.GetStreamAsync(iconUrl).ConfigureAwait(false))
            using (var tempFileTarget = File.OpenWrite(tempFileTargetName))
            {
                await iconStream.CopyToAsync(tempFileTarget).ConfigureAwait(false);
            }
            await Context.Client.CurrentUser.ModifyAsync(props => props.Avatar = new Image(tempFileTargetName)).ConfigureAwait(false);
            File.Delete(tempFileTargetName);
            await ReplyAsync("Avatar updated!").ConfigureAwait(false);
        }

        [Command("getguilds"), Alias("guildlist", "listguilds"), RequireTeamOwner]
        [Summary("Lists the guilds this bot is a member of. Paginated.")]
        public async Task ListGuilds(int pageNumber = 1)
        {
            IReadOnlyCollection<IGuild> guilds = await Context.Client.GetGuildsAsync().ConfigureAwait(false);

            const int guildsPerPage = 10;
            int highestPage = (int)Math.Ceiling((1.0 * guilds.Count) / guildsPerPage);
            if (pageNumber <= 0 || pageNumber > highestPage)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            }

            var replyBuilder = new System.Text.StringBuilder();
            replyBuilder.AppendLine($"**Guild List (Page {pageNumber} of {highestPage}):**");
            replyBuilder.AppendFormat("*{0}, member counts are likely inaccurate*", Utilities.Pluralize("guild", guilds.Count)).AppendLine();
            replyBuilder.AppendLine();

            pageNumber--;

            foreach (var guild in guilds.Skip(pageNumber * guildsPerPage).Take(guildsPerPage))
            {
                var owner = await guild.GetOwnerAsync().ConfigureAwait(false);

                int memberCt = -1;
                try
                {
                    memberCt = (await guild.GetUsersAsync().ConfigureAwait(false)).Count;
                } catch { }

                replyBuilder.AppendLine($"__{guild.Name}__ ({guild.Id}):\n- {Utilities.Pluralize("member", memberCt)}\n- {Utilities.Pluralize("text channel", (await guild.GetTextChannelsAsync().ConfigureAwait(false)).Count)}\n- Owned by: {(owner != null ? owner.Username + '#' + owner.DiscriminatorValue : "<unknown>")} (<{(Context.Channel is IDMChannel ? "" : "\\")}@{guild.OwnerId}>)");
            }

            await ReplyAsync(replyBuilder.ToString()).ConfigureAwait(false);
        }

        [Group("exportscoreboard")]
        [Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireTeamOwner]
        public class ExportScoreboardModule : ModuleBase
        {
            public ScoreboardDownloadService ScoreDownloader { get; set; }
            public PreferenceProviderService Preferences { get; set; }
            public IScoreRetrievalService ScoreService { get; set; }

            [Command("status")]
            [Summary("Gets the status of an ongoing scoreboard download.")]
            public async Task GetStatus()
            {
                var state = ScoreDownloader.GetState();
                // list might be being modified, but array won't be
                // statuses might change during execution but Task itself is threadsafe

                if (state.OriginalTaskList == null)
                {
                    await ReplyAsync("__Status:__\nInitializing...").ConfigureAwait(false);
                    return;
                }

                // second might be higher because tasks continue executing during this
                int completedCount = state.OriginalTaskList.Count(t => t.IsCompleted);
                TeamId[] faulted = state.OriginalTaskList.Select((t, i) => new { Id = state.OriginalDownloadList[i], Task = t }).Where(x => x.Task.IsFaulted).Select(x => x.Id).ToArray();

                TimeZoneInfo userTz = await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false);
                TimeSpan elapsed = DateTimeOffset.UtcNow - state.StartTime;

                var faultedTeamsStatus = new StringBuilder();
                if (faulted.Length > 0)
                {
                    faultedTeamsStatus.AppendLine();
                    faultedTeamsStatus.AppendLine();
                    faultedTeamsStatus.AppendLine("__Teams whose downloads failed:__");
                    const int maxFaultedToDisplay = 20;
                    int cap = Math.Min(faulted.Length, maxFaultedToDisplay);
                    for (int i = 0; i < cap; i++)
                    {
                        faultedTeamsStatus.AppendLine(faulted[i].ToString());
                    }

                    if (faulted.Length > maxFaultedToDisplay)
                    {
                        faultedTeamsStatus.AppendLine($"(and {faulted.Length - maxFaultedToDisplay} others)");
                    }
                }

                await ReplyAsync("__Status:__\n" +
                $"Started: {TimeZoneInfo.ConvertTime(state.StartTime, userTz):g} {userTz.GetAbbreviations().Generic} ({elapsed.ToLongString()} ago)\n" +
                $"Team downloads completed: {completedCount} / {state.OriginalTaskList.Length} ({(100.0 * completedCount) / state.OriginalTaskList.Length:F1}%)\n" +
                $"Team downloads errored: {faulted.Length}\n" +
                $"About {(elapsed * Math.Min(state.OriginalTaskList.Length / (1.0 * (completedCount + faulted.Length)), 100000000) - elapsed).ToLongString(showSeconds: false)} remaining" + faultedTeamsStatus.ToString()).ConfigureAwait(false);
            }

            [Command("redownload")]
            [Summary("Redownloads the data for a given team.")]
            public async Task Redownload(TeamId team)
            {
                var state = ScoreDownloader.GetState();

                await state.ListLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    state.DetailsTaskList.Add(ScoreService.GetDetailsAsync(team));
                }
                finally
                {
                    state.ListLock.Release();
                }

                await ReplyAsync($"Queued re-download for {team}.").ConfigureAwait(false);
            }

            // FIXME RunMode.Async means NO ERROR HANDLING
            // the user will NOT see errors
            [Command(RunMode = RunMode.Async)]
            [Summary("Exports a GZip-compressed JSON scoreboard from the current backend to the current channel.")]
            [Priority(-1)]
            public async Task DownloadScoreboardAsync([Summary("A URL pointing to an existing GZip-compressed JSON backup.")] string existingDataUrl = null)
            {
                await ReplyAsync("Downloading scoreboard...").ConfigureAwait(false);

                using (Context.Channel.EnterTypingState())
                {
                    IReadOnlyDictionary<TeamId, ScoreboardDetails> existingArchive = null;
                    var retState = new ScoreboardDownloadService.ReturnedStateInfoWrapper();

                    try
                    {
                        string attachmentUrl = Context.Message?.Attachments?.FirstOrDefault()?.Url ?? existingDataUrl;
                        if (attachmentUrl != null)
                        {
                            using (var downloader = new HttpClient())
                            using (var unzipStream = new GZipStream(await downloader.GetStreamAsync(attachmentUrl).ConfigureAwait(false), CompressionMode.Decompress))
                            {
                                var temp = new CyberPatriot.Services.ScoreRetrieval.JsonScoreRetrievalService();
                                temp.Deserialize(await new StreamReader(unzipStream).ReadToEndAsync().ConfigureAwait(false));
                                existingArchive = temp.StoredTeamDetails;
                            }
                        }
                    }
                    catch { }

                    using (MemoryStream ms = await ScoreDownloader.DownloadFullScoreboardGzippedAsync(retState: retState, previousScoreStatus: existingArchive).ConfigureAwait(false))
                    {
                        string fileName = $"scoreboard-{retState.Summary.SnapshotTimestamp.ToUnixTimeSeconds()}.json.gz";
                        if (Directory.Exists("scoreboardarchives"))
                        {
                            using (var fileStream = File.Create(Path.Combine("scoreboardarchives", fileName)))
                            {
                                await ms.CopyToAsync(fileStream).ConfigureAwait(false);
                            }
                        }
                        ms.Position = 0;
                        TimeZoneInfo tz = await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false);
                        DateTimeOffset timestamp = TimeZoneInfo.ConvertTime(retState.Summary.SnapshotTimestamp, tz);

                        double downloadPercentSuccess = retState.DownloadTasks.Length == 0 ? 100 : (100.0 * retState.DownloadTasks.Count(t => t.IsCompletedSuccessfully)) / retState.DownloadTasks.Length;

                        await Context.Channel.SendFileAsync(ms, fileName,
                            $"JSON scoreboard snapshot for {timestamp:g} {tz.GetAbbreviations().Generic}\n" +
                            $"{Utilities.Pluralize("team", retState.TeamDetailCount)} total:\n" +
                            $"{Utilities.Pluralize("team", retState.DownloadTasks.Length)} downloaded from \"{ScoreService.Metadata.StaticSummaryLine}\"\n" +
                            $"{downloadPercentSuccess:F1}% of downloads successful").ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
