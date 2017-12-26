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
            public async Task SetPrefixAsync(string newPrefix)
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
            public IDataPersistenceService Database { get; set; }

            [Command("set")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task SetTimezoneAsync(string newTimezone)
            {
                try
                {
                    if (TimeZoneInfo.FindSystemTimeZoneById(newTimezone) == null)
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
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.TimeZone = newTimezone;
                    await context.WriteAsync();
                }
                await ReplyAsync($"Updated timezone to {TimeZoneNames.TZNames.GetNamesForTimeZone(newTimezone, "en-US").Generic}.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveTimezone()
            {
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.TimeZone = null;
                    await context.WriteAsync();
                }
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
            public async Task ListTeamsAsync(ITextChannel channel = null)
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
            public async Task RemoveTeamAsync(TeamId team, ITextChannel channel = null)
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
            public async Task WatchTeamAsync(TeamId team, ITextChannel channel = null)
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

        [Command("ping")]
        public Task PingAsync() => ReplyAsync("Pong!");

        [Command("kill"), Alias("die"), RequireOwner]
        public Task KillAsync()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        public IScoreRetrievalService ScoreService { get; set; }
        public IDataPersistenceService Database { get; set; }

        [Command("exportscoreboard"), Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireOwner]
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
                            teamDetails);
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