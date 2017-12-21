using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CyberPatriot.DiscordBot;
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
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                guildSettings.Prefix = newPrefix;
                await Database.SaveAsync(guildSettings);
                await ReplyAsync("Updated prefix.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveAsync()
            {
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                guildSettings.Prefix = null;
                await Database.SaveAsync(guildSettings);
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
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };

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

                guildSettings.TimeZone = newTimezone;
                await Database.SaveAsync(guildSettings);
                await ReplyAsync($"Updated timezone to {TimeZoneNames.TZNames.GetNamesForTimeZone(newTimezone, "en-US").Generic}.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveTimezone()
            {
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                guildSettings.TimeZone = null;
                await Database.SaveAsync(guildSettings);
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
                Models.Channel channelSettings =
                    guildSettings?.ChannelSettings?.SingleOrDefault(chan => chan.Id == channel.Id);
                if (channelSettings?.MonitoredTeams == null || channelSettings.MonitoredTeams.Count == 0)
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
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                Models.Channel channelSettings =
                    guildSettings?.ChannelSettings?.SingleOrDefault(chan => chan.Id == channel.Id);
                if (channelSettings == null)
                {
                    if (guildSettings.ChannelSettings == null)
                    {
                        guildSettings.ChannelSettings = new List<Channel>();
                    }
                    guildSettings.ChannelSettings.RemoveAll(chan => chan.Id == Context.Channel.Id);
                    channelSettings = new Models.Channel() { Id = Context.Channel.Id };
                    guildSettings.ChannelSettings.Add(channelSettings);
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
                
                await Database.SaveAsync(guildSettings);
            }
            
            [Command("add"), Alias("watch")]
            [RequireUserPermission(ChannelPermission.ManageChannel)]
            [RequireContext(ContextType.Guild)]
            public async Task WatchTeamAsync(TeamId team, ITextChannel channel = null)
            {
                // guaranteed guild context
                channel = channel ?? (Context.Channel as ITextChannel);
                Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                Models.Channel channelSettings =
                    guildSettings?.ChannelSettings?.SingleOrDefault(chan => chan.Id == channel.Id);
                if (channelSettings == null)
                {
                    if (guildSettings.ChannelSettings == null)
                    {
                        guildSettings.ChannelSettings = new List<Channel>();
                    }
                    guildSettings.ChannelSettings.RemoveAll(chan => chan.Id == channel.Id);
                    channelSettings = new Models.Channel() { Id = channel.Id };
                    guildSettings.ChannelSettings.Add(channelSettings);
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
                
                await Database.SaveAsync(guildSettings);
            }
        }

        [Command("ping")]
        public Task PingAsync() => ReplyAsync("Pong!");
    }
}