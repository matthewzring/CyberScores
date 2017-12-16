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

        [Command("ping")]
        public Task PingAsync() => ReplyAsync("Pong!");
    }
}