using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;

namespace CyberPatriot.DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private IServiceProvider _provider;
        private LiteDatabase _database;

        public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands, LiteDatabase database)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _database = database;

            _discord.MessageReceived += MessageReceived;
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _provider = provider;
            _commands.AddTypeReader<CyberPatriot.Models.TeamId>(new TeamIdTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        private async Task MessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            int argPos = -1;
            if (!message.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                if (message.Channel is SocketGuildChannel messageGuildChannel)
                {
                    // check server prefix
                    Models.Guild guildSettings = _database.GetCollection<Models.Guild>().FindOne(g => g.Id == messageGuildChannel.Id);
                    // intentional empty statement
                    if (guildSettings != null && message.HasStringPrefix(guildSettings.Prefix, ref argPos)) ;
                }
                else if (message.Channel is SocketDMChannel)
                {
                    // try it with no prefix
                    argPos = 0;
                }
            }

            if (argPos == -1)
            {
                return;
            }

            var context = new SocketCommandContext(_discord, message);
            var result = await _commands.ExecuteAsync(context, argPos, _provider);

            if (result.Error.HasValue &&
                result.Error.Value == CommandError.UnknownCommand)
                return;

            if (result.Error.HasValue &&
                result.Error.Value != CommandError.UnknownCommand)
                await context.Channel.SendMessageAsync(result.ToString());
        }

    }
}
