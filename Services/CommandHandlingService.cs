using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace CyberPatriot.DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private IServiceProvider _provider;
        private IDataPersistenceService _database;

        public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands, IDataPersistenceService database)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _database = database;

            _discord.MessageReceived += MessageReceived;
        }

        private readonly System.Text.RegularExpressions.Regex multipleSpaces = new System.Text.RegularExpressions.Regex(" +");

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _provider = provider;
            _commands.AddTypeReader<CyberPatriot.Models.TeamId>(new TeamIdTypeReader());
            _commands.AddTypeReader<CyberPatriot.Models.Division>(new DivisionTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly());
            await _commands.CreateModuleAsync("help", mb =>
            {
                Dictionary<string, Action<Discord.Commands.Builders.ModuleBuilder>> buildersBySubmoduleName = new Dictionary<string, Action<Discord.Commands.Builders.ModuleBuilder>>();
                foreach (var cmd in _commands.Commands)
                {
                    foreach (var alias in cmd.Aliases)
                    {
                        string[] aliasComponents = alias.Split(' ');
                        // don't pass the CommandInfo given by the delegate, pass the command we're giving help for
                        if (aliasComponents.Length == 1)
                        {
                            mb.AddCommand(alias, (c, p, s, i) => HelpCommandAsync(c, p, s, cmd, alias), cb => cb.Summary = "Command help");
                        }
                        else
                        {
                            for (int i = 0; i < aliasComponents.Length - 1; i++)
                            {
                                string aliasTillNow = string.Join(' ', aliasComponents.Take(i + 1));
                                if (!buildersBySubmoduleName.TryGetValue(aliasTillNow, out Action<Discord.Commands.Builders.ModuleBuilder> submoduleConfigurator))
                                {
                                    // first add submodule builder delegate
                                    submoduleConfigurator = new Action<Discord.Commands.Builders.ModuleBuilder>(smb =>
                                    {
                                        foreach (var kvp in buildersBySubmoduleName)
                                        {
                                            // check if one of our submodules, and if one of our DIRECT submodules
                                            // all tier layers should be present in the hierarchy
                                            if (kvp.Key.StartsWith(aliasTillNow + " ") && kvp.Key.Substring(aliasTillNow.Length + 1).Count(c => c == ' ') - 1 == aliasTillNow.Count(c => c == ' '))
                                            {
                                                // this is one of our DIRECT submodules
                                                // it could have its own submodules
                                                smb.AddModule(kvp.Key.Substring(aliasTillNow.Length + 1).Split(' ').First(), kvp.Value);
                                            }
                                        }
                                    });
                                    buildersBySubmoduleName.Add(aliasTillNow, submoduleConfigurator);
                                }
                            }

                            buildersBySubmoduleName[string.Join(' ', aliasComponents.Take(aliasComponents.Length - 1))] += smb =>
                                smb.AddCommand(aliasComponents.Last(), (c, p, s, i) => HelpCommandAsync(c, p, s, cmd, alias), cb => cb.Summary = "Command help");
                        }
                    }
                }
                // only call root modules, root modules will invoke their own submodules
                foreach (var submoduleConfigurator in buildersBySubmoduleName.Where(k => k.Key.Count(c => c == ' ') == 0))
                {
                    mb.AddModule(submoduleConfigurator.Key, submoduleConfigurator.Value);
                }

                // add overall help
                mb.AddCommand(string.Empty, HelpOverallAsync, cb => cb.WithSummary("Overall help. Pass a command specification (e.g. 'help admin ping') to show help for that command."));
            });
        }

        private async Task<IResult> HelpOverallAsync(ICommandContext context, object[] parameters, IServiceProvider services, CommandInfo invokedHelpCmd)
        {
            // FIXME pagination currently fails, and adding the parameter breaks the rest of the help commands
            const int pageSize = 50;
            int pageNumber = 1;

            if (parameters.Length > 1)
            {
                return ParseResult.FromError(CommandError.BadArgCount, "Help for commands takes no more than one argument.");
            }
            else if (parameters.Length == 1)
            {
                if (!int.TryParse(parameters[0] as string, out pageNumber))
                {
                    return TypeReaderResult.FromError(CommandError.ParseFailed, "Could not parse pageNumber as an integer.");
                }
            }

            CommandInfo[] cmds = _commands.Commands
                .Where(cmd =>
                {
                    ModuleInfo rootModule = cmd.Module;
                    while (rootModule?.Parent != null)
                    {
                        rootModule = rootModule.Parent;
                    }

                    // all commands OK, except generated help commands
                    // only the overall help command should be displayed in help
                    return rootModule.Name != "help" || cmd.Aliases.SingleIfOne() == "help";
                }).OrderBy(cmd => cmd.Aliases[0]).ToArray();
            int pageCount = Utilities.CeilingDivision(cmds.Length, pageSize);

            if (pageNumber < 1 || pageNumber > pageCount)
            {
                return ExecuteResult.FromError(CommandError.Unsuccessful, "The given page number is invalid (too high or too low).");
            }

            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Help",
                Color = Color.DarkBlue
            };

            foreach (var cmd in cmds.Skip((pageNumber - 1) * pageSize).Take(pageSize))
            {
                BuildHelpAsField(builder, cmd);
            }

            builder.WithFooter(footer => footer.WithText($"Page {pageNumber} of {pageCount}"));

            await context.Channel.SendMessageAsync(string.Empty, embed: builder.Build());
            return ExecuteResult.FromSuccess();
        }

        private void BuildHelpAsField(EmbedBuilder embed, CommandInfo cmd, string commandInvocation = null)
        {
            if (commandInvocation == null)
            {
                commandInvocation = cmd.Aliases[0];
            }
            StringBuilder invocationString = new StringBuilder();
            invocationString.Append('`').Append(commandInvocation);
            var paramDescs = new Dictionary<Discord.Commands.ParameterInfo, string>();
            foreach (var param in cmd.Parameters)
            {
                invocationString.Append(' ');
                invocationString.Append(param.IsOptional ? '[' : '<');

                var paramStringBuilder = new StringBuilder();

                paramStringBuilder.Append(param.Name);
                if (param.DefaultValue != null) paramStringBuilder.Append(" = ").Append(param.DefaultValue);
                if (param.IsRemainder || param.IsMultiple) paramStringBuilder.Append("...");

                paramDescs[param] = paramStringBuilder.ToString();
                invocationString.Append(paramStringBuilder.ToString());
                invocationString.Append(param.IsOptional ? ']' : '>');

            }
            invocationString.Append('`').AppendLine();
            StringBuilder description = new StringBuilder();
            if (cmd.Summary != null || cmd.Remarks != null)
            {
                description.AppendLine(cmd.Summary == null ^ cmd.Remarks == null ? (cmd.Summary ?? cmd.Remarks) : cmd.Summary + "\n\n" + cmd.Remarks);
            }
            else
            {
                description.AppendLine("*No description provided.*");
            }

            description.AppendLine();

            if (cmd.Aliases.Count > 1)
            {
                description.Append("Also known as: ").AppendLine(string.Join(", ", cmd.Aliases.Except(commandInvocation))).AppendLine();
            }

            description.AppendLine(cmd.Parameters.Count > 0 ? "**Parameters:**" : "*Parameterless command.*");

            foreach (var param in cmd.Parameters)
            {
                string descText = paramDescs[param];
                description.AppendFormat("`{3}{2} {0}` - {1}", descText, param.Summary ?? "*No description provided.*", param.Type.Name, param.IsOptional ? "[Optional] " : string.Empty).AppendLine();
            }

            embed.AddField("__" + invocationString.ToString() + "__", description.ToString());
        }

        private async Task<IResult> HelpCommandAsync(ICommandContext context, object[] parameters, IServiceProvider services, CommandInfo cmd, string alias)
        {
            if (parameters?.Length != 0)
            {
                return ParseResult.FromError(CommandError.BadArgCount, "Help for commands takes no arguments.");
            }

            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Help",
                Color = Color.DarkBlue
            };
            BuildHelpAsField(builder, cmd, alias);

            await context.Channel.SendMessageAsync(string.Empty, embed: builder.Build());
            return ExecuteResult.FromSuccess();
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
                    Models.Guild guildSettings = await _database.FindOneAsync<Models.Guild>(g => g.Id == messageGuildChannel.Guild.Id);
                    // intentional empty statement
                    if (guildSettings?.Prefix != null)
                    {
                        if (!message.HasStringPrefix(guildSettings.Prefix, ref argPos))
                        {
                            argPos = -1;
                        }
                    }
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
            {
                await context.Channel.SendMessageAsync(string.Empty,
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error Executing Command: " + result.Error.Value.ToStringCamelCaseToSpace())
                        .WithDescription(result.ErrorReason)
                        .WithTimestamp(message.CreatedAt));
            }
        }

    }
}
