using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace CyberPatriot.DiscordBot.Services
{
    public class LogService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _discordLogger;
        private readonly ILogger _commandsLogger;
        private readonly ILogger _applicationLogger;

        public LogService(DiscordSocketClient discord, CommandService commands, ILoggerFactory loggerFactory)
        {
            _discord = discord;
            _commands = commands;

            _loggerFactory = ConfigureLogging(loggerFactory);
            _discordLogger = _loggerFactory.CreateLogger("discord");
            _commandsLogger = _loggerFactory.CreateLogger("commands");
            _applicationLogger = _loggerFactory.CreateLogger("application");

            _discord.Log += LogDiscord;
            _commands.Log += LogCommand;

            // TODO use "proper" Microsoft.Extensions.Logging infrastructure, it just seems really overcomplicated for my needs
            _discord.Log += LogErrorToOwnerDM;
        }

        private Task LogErrorToOwnerDM(LogMessage message)
        {
            // lower indicates higher priority
            if (!((message.Severity <= LogSeverity.Warning && message.Exception != null) || message.Severity <= LogSeverity.Error))
            {
                return Task.CompletedTask;
            }

            // per LogCommand (example code): "Don't risk blocking the logging task by awaiting a message send; ratelimits!?"    
            var _ = Task.Run(async () =>
            {
                var appInfo = await _discord.GetApplicationInfoAsync().ConfigureAwait(false);
                var ownerDmChannel = await appInfo?.Owner?.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                if (ownerDmChannel == null)
                {
                    return;
                }

                // backtick, backtick, zero width space, backtick
                // this way it won't break our markdown
                const string threeBackticksEscaped = "``​`";

                await ownerDmChannel.SendMessageAsync(string.Empty,
                embed: new EmbedBuilder()
                    .WithTitle("Application Message: " + message.Severity.ToStringCamelCaseToSpace() + (message.Exception != null ? ": " + message.Exception.GetType().Name : string.Empty))
                    .WithDescription("```" + message.ToString().Replace("```", threeBackticksEscaped) + "```")
                    .WithColor(Color.Red)
                    .Build()).ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private ILoggerFactory ConfigureLogging(ILoggerFactory factory)
        {
            factory.AddConsole();
            return factory;
        }

        private Task LogDiscord(LogMessage message)
        {
            _discordLogger.Log(
                LogLevelFromSeverity(message.Severity),
                0,
                message,
                message.Exception,
                (_1, _2) => message.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        private Task LogCommand(LogMessage message)
        {
            // Don't need to log it in the channel here, already handled elegantly in the CommandHandlingService
            // Return an error message for async commands
            //if (message.Exception is CommandException command)
            //{
            //    // Don't risk blocking the logging task by awaiting a message send; ratelimits!?
            //    var _ = command.Context.Channel.SendMessageAsync($"Error: {command.Message}");
            //}

            _commandsLogger.Log(
                LogLevelFromSeverity(message.Severity),
                0,
                message,
                message.Exception,
                (_1, _2) => message.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        public Task LogApplicationMessageAsync(LogSeverity severity, string message, [System.Runtime.CompilerServices.CallerMemberName] string source = "Application") => LogApplicationMessageAsync(new LogMessage(severity, source, message));

        public Task LogApplicationMessageAsync(LogMessage message)
        {
            _applicationLogger.Log(
                LogLevelFromSeverity(message.Severity),
                0,
                message,
                message.Exception,
                (_1, _2) => message.ToString(prependTimestamp: false));
            return LogErrorToOwnerDM(message);
        }

        private static LogLevel LogLevelFromSeverity(LogSeverity severity)
            => (LogLevel)(Math.Abs((int)severity - 5));

    }
}