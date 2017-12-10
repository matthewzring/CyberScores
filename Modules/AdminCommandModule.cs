using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using CyberPatriot.DiscordBot;

namespace CyberPatriot.DiscordBot.Modules
{

    [Group("admin")]
    public class AdminCommandModule : ModuleBase
    {
        [Group("prefix")]
        public class PrefixModule : ModuleBase
        {
            public LiteDatabase Database { get; set; }

            [Command("set")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task SetPrefixAsync(string newPrefix)
            {
                var guildCollection = Database.GetCollection<Models.Guild>();
                Models.Guild guildSettings = guildCollection.FindOne(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                guildSettings.Prefix = newPrefix;
                guildCollection.Upsert(guildSettings);
                await ReplyAsync("Updated prefix.");
            }

            [Command("remove"), Alias("delete", "unset")]
            [RequireUserPermission(GuildPermission.Administrator)]
            [RequireContext(ContextType.Guild)]
            public async Task RemoveAsync()
            {
                var guildCollection = Database.GetCollection<Models.Guild>();
                Models.Guild guildSettings = guildCollection.FindOne(g => g.Id == Context.Guild.Id) ?? new Models.Guild() { Id = Context.Guild.Id };
                guildSettings.Prefix = null;
                guildCollection.Upsert(guildSettings);
                await ReplyAsync("Removed prefix. Use an @mention to invoke commands.");
            }
        }

        [Command("ping")]
        public Task PingAsync() => ReplyAsync("Pong!");
    }
}