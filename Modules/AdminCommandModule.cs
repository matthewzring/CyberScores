using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CyberPatriot.DiscordBot;
using CyberPatriot.DiscordBot.Services;

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

        [Command("ping")]
        public Task PingAsync() => ReplyAsync("Pong!");
    }
}