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

namespace CyberPatriot.DiscordBot.Modules
{
    public class BotAdministrationCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreService { get; set; }
        public IDataPersistenceService Database { get; set; }
        public PreferenceProviderService Preferences { get; set; }
        public LogService Log { get; set; }

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
                + $"**Code:** [On GitHub](https://github.com/glen3b/CyPatScoreboardBot) - [Add me to your server!](https://discordapp.com/oauth2/authorize?client_id={appinfo.Id}&permissions={CyberPatriotDiscordBot.RequiredPermissions}&scope=bot)\n"
                + "**Disclaimer:** This bot is not affiliated with the Air Force Association nor the CyberPatriot program. All scores displayed, even those marked \"official,\" are non-binding unofficial scores and should be treated as such. Official scores can only be found [on the CyberPatriot website](http://www.uscyberpatriot.org/competition/current-competition/scores). NO GUARANTEES OR WARRANTIES ARE MADE as to the accuracy of any information displayed by this bot. Refer to the GitHub README for more information.")
                .WithFooter("Made by glen3b | Written in C# using Discord.Net", "https://avatars.githubusercontent.com/glen3b")
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Prefix").WithValue((Context.Guild != null ? (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id).ConfigureAwait(false))?.Prefix?.AppendPrepend("`") : null) ?? Context.Client.CurrentUser.Mention))
                .AddField(fb => fb.WithIsInline(true).WithName("Score Provider").WithValue(ScoreService.StaticSummaryLine))
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

        [Command("exportscoreboard", RunMode = RunMode.Async), Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireOwner]
        [Summary("Exports a GZip-compressed JSON scoreboard from the current backend to the current channel.")]
        public async Task DownloadScoreboardAsync()
        {
            await ReplyAsync("Downloading scoreboard...").ConfigureAwait(false);

            using (Context.Channel.EnterTypingState())
            {
                var scoreSummary = await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);

                var teamDetailRetrieveTasks = scoreSummary.TeamList.Select(team => ScoreService.GetDetailsAsync(team.TeamId)).ToList();

                IDictionary<TeamId, ScoreboardDetails> teamDetails = new Dictionary<TeamId, ScoreboardDetails>();

                do
                {
                    try
                    {
                        await Task.WhenAll(teamDetailRetrieveTasks).ConfigureAwait(false);
                    }
                    catch
                    {
                        await Log.LogApplicationMessageAsync(LogSeverity.Error, "Error while retrieving some teams in full score export, excluding them silently!").ConfigureAwait(false);
                        // TODO handle this in a more logical place
                        // assume a rate limit issue, treat this task as lost and cool down
                        // here's a hack if I ever saw one
                        var rateLimiter = (ScoreService.GetFirstFromChain<IScoreRetrievalService>(s => s is HttpScoreboardScoreRetrievalService) as HttpScoreboardScoreRetrievalService)?.RateLimiter;
                        var delayTask = Task.Delay(15000);
                        if (rateLimiter != null)
                        {
                            rateLimiter.AddPrerequisite(delayTask);
                        }

                        await delayTask.ConfigureAwait(false);
                    }

                    // completed and faulted
                    var completedTasks = teamDetailRetrieveTasks.Where(t => t.IsCompleted).ToArray();

                    // add all completed results to the dictionary
                    foreach (var retrieveTask in completedTasks.Where(t => t.IsCompletedSuccessfully))
                    {
                        // successful completion
                        ScoreboardDetails details = retrieveTask.Result;
                        teamDetails[details.TeamId] = details;
                    }

                    // remove all completed and faulted tasks
                    foreach (var task in completedTasks)
                    {
                        teamDetailRetrieveTasks.Remove(task);
                    }
                } while (teamDetailRetrieveTasks.Count > 0);


                MemoryStream rawWriteStream = null;
                try
                {
                    rawWriteStream = new MemoryStream();

                    // need to close to get GZ tail, but this also closes underlying stream...
                    using (var writeStream = new GZipStream(rawWriteStream, CompressionMode.Compress))
                    {
                        await JsonScoreRetrievalService.SerializeAsync(new StreamWriter(writeStream), scoreSummary,
                            teamDetails, ScoreService.Round).ConfigureAwait(false);
                    }
                    rawWriteStream = new MemoryStream(rawWriteStream.ToArray());

                    string fileName = $"scoreboard-{scoreSummary.SnapshotTimestamp.ToUnixTimeSeconds()}.json.gz";
                    if (Directory.Exists("scoreboardarchives"))
                    {
                        using (var fileStream = File.Create(Path.Combine("scoreboardarchives", fileName)))
                        {
                            await rawWriteStream.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                    }
                    rawWriteStream.Position = 0;
                    TimeZoneInfo tz = await Preferences.GetTimeZoneAsync(Context.Guild, Context.User).ConfigureAwait(false);
                    DateTimeOffset timestamp = TimeZoneInfo.ConvertTime(scoreSummary.SnapshotTimestamp, tz);

                    await Context.Channel.SendFileAsync(rawWriteStream, fileName,
                        $"JSON scoreboard snapshot for {timestamp:g} {tz?.GetAbbreviations().Generic ?? "UTC"}").ConfigureAwait(false);
                }
                finally
                {
                    rawWriteStream?.Dispose();
                }
            }
        }
    }
}