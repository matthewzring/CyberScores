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
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.DiscordBot.Models;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Modules;

[Group("admin")]
public class GuildAdministrationCommandModule : ModuleBase
{
    public IDataPersistenceService Database { get; set; }
    public LogService Log { get; set; }

    [Group("prefix")]
    [RequireUserPermission(GuildPermission.ManageGuild, Group = "RolePermission")]
    [RequireTeamOwner(Group = "RolePermission")]
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
                Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id).ConfigureAwait(false);
                guildSettings.Prefix = newPrefix;
                await context.WriteAsync().ConfigureAwait(false);
            }
            await ReplyAsync($"Prefix updated to `{newPrefix}` for this guild.").ConfigureAwait(false);
        }

        [Command("remove"), Alias("delete", "unset")]
        [Summary("Removes the prefix for this guild, reverting to the @mention default.")]
        public async Task RemoveAsync()
        {
            using (var context = Database.OpenContext<Guild>(true))
            {
                Models.Guild guildSettings = await Guild.OpenWriteGuildSettingsAsync(context, Context.Guild.Id).ConfigureAwait(false);
                guildSettings.Prefix = null;
                await context.WriteAsync().ConfigureAwait(false);
            }
            await ReplyAsync("Removed prefix for this guild. Use an @mention to invoke commands.").ConfigureAwait(false);
        }
    }

    [Group("timezone"), Alias("tz")]
    [RequireUserPermission(GuildPermission.ManageGuild, Group = "RolePermission")]
    [RequireTeamOwner(Group = "RolePermission")]
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
                await ReplyAsync($"That timezone is not recognized. Please make sure you are passing a valid {tzType} timezone identifier.").ConfigureAwait(false);
                return;
            }
            await PreferenceService.SetTimeZoneAsync(Context.Guild, newTz).ConfigureAwait(false);
            await ReplyAsync($"Updated timezone to {TimeZoneNames.TZNames.GetNamesForTimeZone(newTimezone, "en-US").Generic}.").ConfigureAwait(false);
        }

        [Command("remove"), Alias("delete", "unset")]
        [Summary("Removes the designated timezone for this guild, reverting the default to UTC.")]
        public async Task RemoveTimezone()
        {
            await PreferenceService.SetTimeZoneAsync(Context.Guild, null).ConfigureAwait(false);
            await ReplyAsync("Removed timezone. Displayed times will now be in UTC.").ConfigureAwait(false);
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
            Models.Guild guildSettings = await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id).ConfigureAwait(false);
            if (guildSettings == null || !guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings) || channelSettings?.MonitoredTeams == null || channelSettings.MonitoredTeams.Count == 0)
            {
                await ReplyAsync($"{channel.Mention} is not monitoring any teams.").ConfigureAwait(false);
            }
            else
            {
                var retVal = new StringBuilder();
                retVal.AppendLine($"{channel.Mention} is monitoring {Utilities.Pluralize("team", channelSettings.MonitoredTeams.Count)}");
                foreach (var teamId in channelSettings.MonitoredTeams)
                {
                    retVal.AppendLine(teamId.ToString());
                }
                await ReplyAsync(retVal.ToString()).ConfigureAwait(false);
            }
        }

        [Command("remove"), Alias("delete", "unwatch")]
        [RequireUserPermission(ChannelPermission.ManageChannels, Group = "RolePermission")]
        [RequireTeamOwner(Group = "RolePermission")]
        [Summary("Unwatches a team from placement change notifications in this channel.")]
        public async Task RemoveTeamAsync([Summary("The team to unwatch.")] TeamId team)
        {
            // guaranteed guild context
            var channel = Context.Channel as ITextChannel;
            using (var dbContext = Database.OpenContext<Models.Guild>(true))
            {
                var guildSettings = await Guild.OpenWriteGuildSettingsAsync(dbContext, Context.Guild.Id).ConfigureAwait(false);
                if (!guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings))
                {
                    channelSettings = new Channel() { Id = channel.Id };
                    guildSettings.ChannelSettings[channel.Id] = channelSettings;
                }
                if (channelSettings.MonitoredTeams == null || !channelSettings.MonitoredTeams.Contains(team))
                {
                    await ReplyAsync("Could not unwatch that team; it was not being watched.").ConfigureAwait(false);
                }
                else
                {
                    channelSettings.MonitoredTeams.Remove(team);
                    await ReplyAsync($"Unwatching team {team} in {channel.Mention}").ConfigureAwait(false);
                }

                await dbContext.WriteAsync().ConfigureAwait(false);
            }
        }

        [Command("add"), Alias("watch")]
        [RequireUserPermission(ChannelPermission.ManageChannels, Group = "RolePermission")]
        [RequireTeamOwner(Group = "RolePermission")]
        [Summary("Add a team for placement change monitoring. When this team changes placement (either rises or falls) on the scoreboard, an announcement will be made in this channel.")]
        public async Task WatchTeamAsync([Summary("The team to monitor.")] TeamId team)
        {
            // guaranteed guild context
            var channel = Context.Channel as ITextChannel;
            using (var dbContext = Database.OpenContext<Models.Guild>(true))
            {
                var guildSettings = await Guild.OpenWriteGuildSettingsAsync(dbContext, Context.Guild.Id).ConfigureAwait(false);
                if (!guildSettings.ChannelSettings.TryGetValue(channel.Id, out Models.Channel channelSettings))
                {
                    channelSettings = new Channel() { Id = channel.Id };
                    guildSettings.ChannelSettings[channel.Id] = channelSettings;
                }
                if (channelSettings.MonitoredTeams != null && channelSettings.MonitoredTeams.Contains(team))
                {
                    await ReplyAsync("Could not watch that team; it is already being watched.").ConfigureAwait(false);
                }
                else
                {
                    if (channelSettings.MonitoredTeams == null)
                    {
                        channelSettings.MonitoredTeams = new List<TeamId>();
                    }
                    channelSettings.MonitoredTeams.Add(team);
                    await ReplyAsync($"Watching team {team} in {channel.Mention}").ConfigureAwait(false);
                }

                await dbContext.WriteAsync().ConfigureAwait(false);
            }
        }
    }

    [Command("clearprefs"), Alias("deleteprefs", "deletedata", "clearpreferences")]
    [RequireUserPermission(GuildPermission.ManageGuild, Group = "RolePermission")]
    [RequireTeamOwner(Group = "RolePermission")]
    [RequireContext(ContextType.Guild)]
    [Summary("Clears storage of all per-guild information.")]
    public async Task RemoveAsync()
    {
        int numDeleted = -1;
        using (var context = Database.OpenContext<Guild>(true))
        {
            numDeleted = await context.DeleteAsync(g => g.Id == Context.Guild.Id).ConfigureAwait(false);
            await context.WriteAsync().ConfigureAwait(false);
        }

        if (numDeleted < 0 || numDeleted > 1)
            await Log.LogApplicationMessageAsync(LogSeverity.Warning, $"Deleted {numDeleted} entries when clearing data for guild ID {Context.Guild.Id}", source: nameof(GuildAdministrationCommandModule));

        await ReplyAsync("Deleted all stored guild preferences. It's as if you were never there.\nNote you may have to set a new prefix.").ConfigureAwait(false);
    }
}
