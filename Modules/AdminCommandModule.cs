using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.DiscordBot.Models;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Modules
{

    [Group("admin")]
    public class AdminCommandModule : ModuleBase
    {
        [Group("prefix")]
        public class PrefixModule : ModuleBase
        {
            public IDataPersistenceService Database { get; set; }

            [Command("set")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            [Summary("Sets the prefix for this guild.")]
            public async Task SetPrefixAsync([Summary("The new prefix for this guild.")] string newPrefix)
            {
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.Prefix = newPrefix;
                    await context.WriteAsync();
                }
                await ReplyAsync("Updated prefix.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            [Summary("Removes the prefix for this guild, reverting to the @mention default.")]
            public async Task RemoveAsync()
            {
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.Prefix = null;
                    await context.WriteAsync();
                }
                await ReplyAsync("Removed prefix. Use an @mention to invoke commands.");
            }
        }

        [Group("timezone"), Alias("tz")]
        public class TimezoneModule : ModuleBase
        {
            public PreferenceProviderService PreferenceService { get; set; }

            [Command("set")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            [Summary("Sets the default timezone for this guild.")]
            public async Task SetTimezoneAsync([Summary("The timezone in which times will be displayed by default.")] string newTimezone)
            {
                TimeZoneInfo newTz;
                try
                {
                    if ((newTz = TimeZoneInfo.FindSystemTimeZoneById(newTimezone)) == null)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    // FIXME inconsistent timezone IDs between platforms -_-
                    string tzType = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" : "IANA";
                    await ReplyAsync($"That timezone is not recognized. Please make sure you are passing a valid *{tzType}* timezone identifier.");
                    return;
                }
                await PreferenceService.SetTimeZoneAsync(Context.Guild, newTz);
                await ReplyAsync($"Updated timezone to {TimeZoneNames.TZNames.GetNamesForTimeZone(newTimezone, "en-US").Generic}.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            [Summary("Removes the designated timezone for this guild, reverting the default to UTC.")]
            public async Task RemoveTimezone()
            {
                await PreferenceService.SetTimeZoneAsync(Context.Guild, null);
                await ReplyAsync("Removed timezone. Displayed times will now be in UTC.");
            }
        }

        [Group("teammonitor")]
        public class TeamMonitorModule : ModuleBase
        {
            public IDataPersistenceService Database { get; set; }

            [Command("list")]
            [RequireUserPermission(ChannelPermission.ManageChannel)]
            [RequireContext(ContextType.Guild)]
            [Summary("Lists all teams which are monitored in a given channel.")]
            public async Task ListTeamsAsync([Summary("The channel to list teams for. Defaults to the current channel.")] ITextChannel channel = null)
            {
                // guaranteed guild context
                channel = channel ?? (Context.Channel as ITextChannel);
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id);
                if (guildSettings == null || !guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings) || channelSettings?.MonitoredTeams == null || channelSettings.MonitoredTeams.Count == 0)
                {
                    await ReplyAsync($"{channel.Mention} is not monitoring any teams.");
                }
                else
                {
                    var retVal = new StringBuilder();
                    retVal.AppendLine($"{channel.Mention} is monitoring {Utilities.Pluralize("team", channelSettings.MonitoredTeams.Count)}");
                    foreach (var teamId in channelSettings.MonitoredTeams)
                    {
                        retVal.AppendLine(teamId.ToString());
                    }
                    await ReplyAsync(retVal.ToString());
                }
            }

            [Command("remove"), Alias("delete", "unwatch")]
            [RequireUserPermission(ChannelPermission.ManageChannel)]
            [RequireContext(ContextType.Guild)]
            [Summary("Unwatches a team from placement change notifications.")]
            public async Task RemoveTeamAsync([Summary("The team to unwatch.")] TeamId team, [Summary("The channel in which the team will cease to be monitored. Defaults to the current channel.")] ITextChannel channel = null)
            {
                // guaranteed guild context
                channel = channel ?? (Context.Channel as ITextChannel);
                using (var dbContext = Database.OpenContext<Models.Guild>(true))
                {
                    var guildSettings = await Guild.OpenWriteGuildSettingsAsync(dbContext, Context.Guild.Id);
                    if (!guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings))
                    {
                        channelSettings = new Channel() { Id = channel.Id };
                        guildSettings.ChannelSettings[channel.Id] = channelSettings;
                    }
                    if (channelSettings.MonitoredTeams == null || !channelSettings.MonitoredTeams.Contains(team))
                    {
                        await ReplyAsync("Could not unwatch that team; it was not being watched.");
                    }
                    else
                    {
                        channelSettings.MonitoredTeams.Remove(team);
                        await ReplyAsync($"Unwatching team {team} in {channel.Mention}");
                    }

                    await dbContext.WriteAsync();
                }
            }

            [Command("add"), Alias("watch")]
            [RequireUserPermission(ChannelPermission.ManageChannel)]
            [RequireContext(ContextType.Guild)]
            [Summary("Add a team for placement change monitoring. When this team changes placement (either rises or falls) on the scoreboard, an announcement will be made in the given channel.")]
            public async Task WatchTeamAsync([Summary("The team to monitor.")] TeamId team, [Summary("The channel in which the team will be monitored. Defaults to the current channel.")] ITextChannel channel = null)
            {
                // guaranteed guild context
                channel = channel ?? (Context.Channel as ITextChannel);
                using (var dbContext = Database.OpenContext<Models.Guild>(true))
                {
                    var guildSettings = await Guild.OpenWriteGuildSettingsAsync(dbContext, Context.Guild.Id);
                    if (!guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings))
                    {
                        channelSettings = new Channel() { Id = channel.Id };
                        guildSettings.ChannelSettings[channel.Id] = channelSettings;
                    }
                    if (channelSettings.MonitoredTeams != null && channelSettings.MonitoredTeams.Contains(team))
                    {
                        await ReplyAsync("Could not watch that team; it is already being watched.");
                    }
                    else
                    {
                        if (channelSettings.MonitoredTeams == null)
                        {
                            channelSettings.MonitoredTeams = new List<TeamId>();
                        }
                        channelSettings.MonitoredTeams.Add(team);
                        await ReplyAsync($"Watching team {team} in {channel.Mention}");
                    }

                    await dbContext.WriteAsync();
                }
            }
        }

        [Command("ping"), Summary("Pings the bot. Responds with the internal socket client's estimated latency, if available.")]
        public Task PingAsync() => ReplyAsync("Pong!" + (Context.Client is Discord.WebSocket.DiscordSocketClient socketClient ? " " + socketClient.Latency + "ms" : string.Empty));

        [Command("kill"), Alias("die"), RequireOwner]
        [Summary("Terminates the bot instance.")]
        public Task KillAsync()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        [Command("setavatar"), Alias("seticon"), RequireOwner]
        [Summary("Sets the avatar for the bot.")]
        public async Task SetIconAsync(string iconUrl)
        {
            var iconDownloader = new System.Net.Http.HttpClient();
            string tempFileTargetName = Path.GetTempFileName();
            using (var iconStream = await iconDownloader.GetStreamAsync(iconUrl))
            using (var tempFileTarget = File.OpenWrite(tempFileTargetName))
            {
                await iconStream.CopyToAsync(tempFileTarget);
            }
            await Context.Client.CurrentUser.ModifyAsync(props => props.Avatar = new Image(tempFileTargetName));
            File.Delete(tempFileTargetName);
            await ReplyAsync("Avatar updated!");
        }

        public IScoreRetrievalService ScoreService { get; set; }
        public IDataPersistenceService Database { get; set; }
        public PreferenceProviderService Preferences { get; set; }

        [Command("shell"), RequireOwner, Summary("Execute a shell command."), RequireContext(ContextType.DM)]
        public async Task ExecuteShellAsync([Summary("The file to execute. Required by .NET.")] string procName, [Remainder, Summary("The command line arguments to execute.")] string commandLine = "")
        {
            // await (await (await Context.Client.GetApplicationInfoAsync()).Owner?.GetOrCreateDMChannelAsync()).SendMessageAsync($"{Context.User.Mention} is executing `{procName}`");

            using (Context.Channel.EnterTypingState())
            using (
                var newProc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    UseShellExecute = false,
                    Arguments = commandLine,
                    CreateNoWindow = true,
                    FileName = procName,
                    WorkingDirectory = Environment.CurrentDirectory,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }))
            using (var target = new MemoryStream())
            {
                DateTimeOffset start = DateTimeOffset.UtcNow;
                var targetWriter = new StreamWriter(target);

                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var completionReadTask = Task.Run(async () =>
                {
                    while (!newProc.StandardOutput.EndOfStream || !newProc.StandardError.EndOfStream)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            throw new OperationCanceledException();
                        }
                        if (!newProc.StandardOutput.EndOfStream)
                        {
                            string stdoutLine = await newProc.StandardOutput.ReadLineAsync();
                            await targetWriter.WriteLineAsync(stdoutLine);
                        }

                        if (!newProc.StandardError.EndOfStream)
                        {
                            string stderrLine = await newProc.StandardOutput.ReadLineAsync();
                            await targetWriter.WriteLineAsync(stderrLine);
                        }
                    }
                    await targetWriter.FlushAsync();
                    target.Position = 0;
                    return await new StreamReader(target).ReadToEndAsync();
                }, cts.Token);
                string result;
                try
                {
                    result = "```" + await completionReadTask + "```";
                    if (completionReadTask.IsCanceled)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    result = "Error, the process may have timed out.";
                }
                await ReplyAsync(result);
            }
        }


        [Command("exportscoreboard"), Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireOwner]
        [Summary("Exports a GZip-compressed JSON scoreboard from the current backend to the current channel.")]
        public async Task DownloadScoreboardAsync()
        {
            await ReplyAsync("Downloading scoreboard...");

            using (Context.Channel.EnterTypingState())
            {
                var scoreSummary = await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
                IDictionary<TeamId, ScoreboardDetails> teamDetails =
                (await Task.WhenAll(scoreSummary.TeamList.Select(team => ScoreService.GetDetailsAsync(team.TeamId)))
                ).ToDictionary(entry => entry.TeamId);
                MemoryStream rawWriteStream = null;
                try
                {
                    rawWriteStream = new MemoryStream();

                    // need to close to get GZ tail, but this also closes underlying stream...
                    using (var writeStream = new GZipStream(rawWriteStream, CompressionMode.Compress))
                    {
                        await JsonScoreRetrievalService.SerializeAsync(new StreamWriter(writeStream), scoreSummary,
                            teamDetails, ScoreService.Round);
                    }
                    rawWriteStream = new MemoryStream(rawWriteStream.ToArray());

                    string fileName = $"scoreboard-{scoreSummary.SnapshotTimestamp.ToUnixTimeSeconds()}.json.gz";
                    if (Directory.Exists("scoreboardarchives"))
                    {
                        using (var fileStream = File.Create(Path.Combine("scoreboardarchives", fileName)))
                        {
                            await rawWriteStream.CopyToAsync(fileStream);
                        }
                    }
                    rawWriteStream.Position = 0;
                    string tzId = (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id))?.TimeZone;
                    TimeZoneInfo tz = null;
                    if (tzId != null)
                    {
                        tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                    }
                    DateTimeOffset timestamp = tz == null
                        ? scoreSummary.SnapshotTimestamp
                        : TimeZoneInfo.ConvertTime(scoreSummary.SnapshotTimestamp, tz);

                    await Context.Channel.SendFileAsync(rawWriteStream, fileName,
                        $"JSON scoreboard snapshot for {timestamp:g} {tz?.GetAbbreviations().Generic ?? "UTC"}");
                }
                finally
                {
                    rawWriteStream?.Dispose();
                }
            }
        }
    }
}