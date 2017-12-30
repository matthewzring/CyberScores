using System;
using System.Collections.Generic;
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
    public class GuildAdministrationCommandModule : ModuleBase
    {
        [Group("prefix")]
        [RequireUserPermission(GuildPermission.Administrator, Group = "RolePermission")]
        [RequireOwner(Group = "RolePermission")]
        [RequireContext(ContextType.Guild)]
        public class PrefixModule : ModuleBase
        {
            public IDataPersistenceService Database { get; set; }

            [Command("set")]
            [Summary("Sets the prefix for this guild.")]
            public async Task SetPrefixAsync([Summary("The new prefix for this guild.")] string newPrefix)
            {
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.Prefix = newPrefix;
                    await context.WriteAsync();
                }
                await ReplyAsync($"Prefix updated to `{newPrefix}` for this guild.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [Summary("Removes the prefix for this guild, reverting to the @mention default.")]
            public async Task RemoveAsync()
            {
                using (var context = Database.OpenContext<Guild>(true))
                {
                    Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id);
                    guildSettings.Prefix = null;
                    await context.WriteAsync();
                }
                await ReplyAsync("Removed prefix for this guild. Use an @mention to invoke commands.");
            }
        }

        [Group("timezone"), Alias("tz")]
        [RequireUserPermission(GuildPermission.Administrator, Group = "RolePermission")]
        [RequireOwner(Group = "RolePermission")]
        [RequireContext(ContextType.Guild)]
        public class TimezoneModule : ModuleBase
        {
            public PreferenceProviderService PreferenceService { get; set; }

            [Command("set")]
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
            [Summary("Removes the designated timezone for this guild, reverting the default to UTC.")]
            public async Task RemoveTimezone()
            {
                await PreferenceService.SetTimeZoneAsync(Context.Guild, null);
                await ReplyAsync("Removed timezone. Displayed times will now be in UTC.");
            }
        }

        [Group("teammonitor")]
        [RequireContext(ContextType.Guild)]
        public class TeamMonitorModule : ModuleBase
        {
            public IDataPersistenceService Database { get; set; }

            [Command("list")]
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
            [RequireUserPermission(ChannelPermission.ManageChannel, Group = "RolePermission")]
            [RequireOwner(Group = "RolePermission")]
            [Summary("Unwatches a team from placement change notifications in this channel.")]
            public async Task RemoveTeamAsync([Summary("The team to unwatch.")] TeamId team)
            {
                // guaranteed guild context
                var channel = Context.Channel as ITextChannel;
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
            [RequireUserPermission(ChannelPermission.ManageChannel, Group = "RolePermission")]
            [RequireOwner(Group = "RolePermission")]
            [Summary("Add a team for placement change monitoring. When this team changes placement (either rises or falls) on the scoreboard, an announcement will be made in this channel.")]
            public async Task WatchTeamAsync([Summary("The team to monitor.")] TeamId team)
            {
                // guaranteed guild context
                var channel = Context.Channel as ITextChannel;
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
    }
}