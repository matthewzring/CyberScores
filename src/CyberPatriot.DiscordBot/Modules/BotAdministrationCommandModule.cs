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

        [Command("info"), Alias("about"), Summary("Returns information about the bot.")]
        public async Task InfoAsync()
        {
            var appinfo = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
            ulong[] allUsers = await Context.Client.GetGuildsAsync().TaskToAsyncEnumerable<IGuild, IReadOnlyCollection<IGuild>>().SelectMany(g => g.GetUsersAsync().TaskToAsyncEnumerable<IGuildUser, IReadOnlyCollection<IGuildUser>>().Select(u => u.Id)).ToArray().ConfigureAwait(false);
            await ReplyAsync(string.Empty, embed: await new EmbedBuilder()
                .WithAuthor(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrlOrDefault())
                .WithDescription("**Purpose:** A bot for interaction with CyberPatriot scoreboards.\n"
                + $"[**Add me to your server!**](https://discordapp.com/oauth2/authorize?client_id={appinfo.Id}&permissions={CyberPatriotDiscordBot.RequiredPermissions}&scope=bot)\n"
                + $"**Code:** [On GitHub](https://github.com/glen3b/CyPatScoreboardBot)\n"
                + "**Disclaimer:** This bot is not affiliated with the Air Force Association nor the CyberPatriot program. All scores displayed, even those marked \"official,\" are non-binding unofficial scores and should be treated as such. Official scores can only be found [on the CyberPatriot website](http://www.uscyberpatriot.org/competition/current-competition/scores). NO GUARANTEES OR WARRANTIES ARE MADE as to the accuracy of any information displayed by this bot. Refer to the GitHub README for more information.")
                .WithFooter("Made by glen3b | Written in C# using Discord.Net", "https://avatars.githubusercontent.com/glen3b")
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
                .AddField(fb => fb.WithIsInline(true).WithName("Users").WithValue(allUsers.Length + $" total\n{allUsers.Distinct().Count()} unique"))
                .BuildAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Command("kill"), Alias("die"), RequireOwner]
        [Summary("Terminates the bot instance.")]
        public async Task KillAsync()
        {
            await ReplyAsync("Goodbye!").ConfigureAwait(false);
            Environment.Exit(0);
        }

        [Command("setavatar"), Alias("seticon"), RequireOwner]
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

        [Command("getguilds"), Alias("guildlist", "listguilds"), RequireOwner]
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
            replyBuilder.AppendFormat("*{0}*", Utilities.Pluralize("guild", guilds.Count)).AppendLine();
            replyBuilder.AppendLine();

            pageNumber--;

            foreach (var guild in guilds.Skip(pageNumber * guildsPerPage).Take(guildsPerPage))
            {
                var owner = await guild.GetOwnerAsync().ConfigureAwait(false);
                replyBuilder.AppendLine($"__{guild.Name}__ ({guild.Id}):\n- {Utilities.Pluralize("member", (await guild.GetUsersAsync().ConfigureAwait(false)).Count)}\n- {Utilities.Pluralize("text channel", (await guild.GetTextChannelsAsync().ConfigureAwait(false)).Count)}\n- Owned by: {owner.Username}#{owner.DiscriminatorValue} (<\\@{owner.Id}>)");
            }

            await ReplyAsync(replyBuilder.ToString()).ConfigureAwait(false);
        }

        [Group("exportscoreboard")]
        [Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireOwner]
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
                int faultedCount = state.OriginalTaskList.Count(t => t.IsFaulted);

                TimeZoneInfo userTz = await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false);
                TimeSpan elapsed = DateTimeOffset.UtcNow - state.StartTime;

                await ReplyAsync("__Status:__\n" +
                $"Started: {TimeZoneInfo.ConvertTime(state.StartTime, userTz):g} {userTz.GetAbbreviations().Generic} ({elapsed.ToLongString()} ago)\n" +
                $"Team downloads completed: {completedCount} / {state.OriginalTaskList.Length} ({(100.0 * completedCount) / state.OriginalTaskList.Length:F1}%)\n" +
                $"Team downloads errored: {faultedCount}\n" +
                $"About {(elapsed * Math.Min(state.OriginalTaskList.Length / (1.0*(completedCount + faultedCount)), 100000000) - elapsed).ToLongString(showSeconds: false)} remaining").ConfigureAwait(false);
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
                                existingArchive = new JsonScoreRetrievalService(await new StreamReader(unzipStream).ReadToEndAsync().ConfigureAwait(false)).StoredTeamDetails;
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