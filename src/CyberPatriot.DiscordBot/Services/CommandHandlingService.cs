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
            _commands.AddTypeReader<CyberPatriot.Models.Tier>(new TierTypeReader());
            // the nasty hack type
            _commands.AddTypeReader<LocationCode>(new LocationTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly()).ConfigureAwait(false);
            await _commands.CreateModuleAsync("help", mb =>
            {
                Dictionary<string, Action<Discord.Commands.Builders.ModuleBuilder>> buildersBySubmoduleName = new Dictionary<string, Action<Discord.Commands.Builders.ModuleBuilder>>();
                Dictionary<string, IList<CommandInfo>> commandsByAlias = new Dictionary<string, IList<CommandInfo>>();
                foreach (var cmd in _commands.Commands.Where(x => !x.Preconditions.Any(y => y is HideCommandHelpAttribute)))
                {
                    foreach (var alias in cmd.Aliases)
                    {
                        string[] aliasComponents = alias.Split(' ');
                        // don't pass the CommandInfo given by the delegate, pass the command we're giving help for

                        for (int i = 0; i < aliasComponents.Length; i++)
                        {
                            string aliasTillNow = string.Join(' ', aliasComponents.Take(i + 1));
                            if (!buildersBySubmoduleName.TryGetValue(aliasTillNow, out Action<Discord.Commands.Builders.ModuleBuilder> submoduleConfigurator))
                            {
                                // first add submodule builder delegate
                                // runs only after all the configurators have been added to the dictionary
                                submoduleConfigurator = new Action<Discord.Commands.Builders.ModuleBuilder>(smb =>
                                {
                                    foreach (var kvp in buildersBySubmoduleName)
                                    {
                                        // check if one of our submodules, and if one of our DIRECT submodules
                                        // all tier layers should be present in the hierarchy
                                        if (kvp.Key.StartsWith(aliasTillNow + " ") && aliasTillNow.Split(' ').Length == kvp.Key.Split(' ').Length - 1)
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

                        if (!commandsByAlias.TryGetValue(alias, out IList<CommandInfo> thisAliasCmdList))
                        {
                            thisAliasCmdList = new List<CommandInfo>();
                            commandsByAlias[alias] = thisAliasCmdList;

                            buildersBySubmoduleName[string.Join(' ', aliasComponents)] += smb =>
                            smb.AddCommand("", (c, p, s, i) => HelpCommandAsync(c, p, s, thisAliasCmdList.ToDictionary(itercmd => itercmd, _ => alias).ToArray()), cb => cb.Summary = "Command help");
                        }

                        thisAliasCmdList.Add(cmd);
                    }
                }
                // only call root modules, root modules will invoke their own submodules
                foreach (var submoduleConfigurator in buildersBySubmoduleName.Where(k => k.Key.Count(c => c == ' ') == 0))
                {
                    mb.AddModule(submoduleConfigurator.Key, submoduleConfigurator.Value);
                }

                // add overall help
                mb.AddCommand(string.Empty, HelpOverallAsync, cb => cb
                    .WithSummary("Overall help. Pass a command specification (e.g. 'help ping') to show help for that command, or a page number (e.g. 'help 1') for paginated overall help.")
                    .WithPriority(int.MinValue / 2)
                    .AddParameter<int>("pageNumber", pb =>
                        pb.WithDefault(1)
                        .WithIsOptional(true)
                        .WithSummary("The page number for paginated help.")));

                // add "help help" manually
                mb.AddModule("help", smb => smb.AddCommand(string.Empty, (c, p, s, i) => HelpCommandAsync(c, p, s, new[] { new KeyValuePair<CommandInfo, string>(_commands.Commands.Single(cand => cand.Aliases[0] == "help"), "help") }), cb => cb.Summary = "Command help"));
            }).ConfigureAwait(false);
        }

        private async Task<ExecuteResult> HelpOverallAsync(ICommandContext context, object[] parameters, IServiceProvider services, CommandInfo invokedHelpCmd)
        {
            const int pageSize = 4;

            // param logic is enforced by the command service
            int pageNumber = (int)parameters[0];

            CommandInfo[] cmds = await _commands.Commands
                .Where(cmd => !cmd.Preconditions.Any(precond => precond is HideCommandHelpAttribute))
                .ToAsyncEnumerable()
                .WhereAsync(async cmd =>
                {
                    ModuleInfo rootModule = cmd.Module;
                    while (rootModule?.Parent != null)
                    {
                        rootModule = rootModule.Parent;
                    }

                    if (rootModule.Name == "help" && cmd.Aliases.SingleIfOne() != "help")
                    {
                        return false;
                    }

                    bool preconditionSuccess = false;
                    try
                    {
                        preconditionSuccess = (await cmd.CheckPreconditionsAsync(context, services)).IsSuccess;
                    }
                    catch { }

                    // all commands OK, except: generated help commands and commands where preconditions are not met
                    // only the overall help command should be displayed in help
                    return preconditionSuccess;
                }).OrderBy(cmd => cmd.Aliases[0]).ToArray().ConfigureAwait(false);
            int pageCount = Utilities.CeilingDivision(cmds.Length, pageSize);

            if (pageNumber < 1 || pageNumber > pageCount)
            {
                return ExecuteResult.FromError(CommandError.Unsuccessful, "The given page number is invalid (too high or too low).");
            }

            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Help",
                Description = "Commands applicable in the current context (e.g. DM, guild channel) with your permission level.",
                Color = Color.DarkBlue
            };

            Models.Guild guildPrefs;
            if (context.Guild != null && (guildPrefs = await _database.FindOneAsync<Models.Guild>(g => g.Id == context.Guild.Id)) != null && guildPrefs.Prefix != null)
            {
                builder.Description += "\n\nThis guild's prefix is: `" + guildPrefs.Prefix + "`";
            }

            foreach (var cmd in cmds.Skip((pageNumber - 1) * pageSize).Take(pageSize))
            {
                BuildHelpAsField(builder, cmd);
            }

            builder.WithFooter(footer => footer.WithText($"Page {pageNumber} of {pageCount}"));

            await context.Channel.SendMessageAsync(string.Empty, embed: builder.Build()).ConfigureAwait(false);
            return ExecuteResult.FromSuccess();
        }

        private struct ExtendedParameterInfo
        {
            public bool Optional;
            public AlterParameterDisplayAttribute AlteredParameterSettings;
            public Discord.Commands.ParameterInfo Info;
            public string Name;
        }

        private void BuildHelpAsField(EmbedBuilder embed, CommandInfo cmd, string commandInvocation = null)
        {
            if (commandInvocation == null)
            {
                commandInvocation = cmd.Aliases[0];
            }
            var paramDescs = new Dictionary<Discord.Commands.ParameterInfo, string>();

            var paramTreeRoot = new Utilities.SimpleNode<ExtendedParameterInfo>();

            foreach (var param in cmd.Parameters)
            {
                var paramSettings = new ExtendedParameterInfo();
                bool optional = param.IsOptional;

                var alteredParamDisplaySettings = param.Preconditions.Select(x => x as AlterParameterDisplayAttribute).FirstOrDefault(x => x != null);
                if (alteredParamDisplaySettings != null && alteredParamDisplaySettings.DisplayAsMandatory ^ alteredParamDisplaySettings.DisplayAsOptional)
                {
                    if (alteredParamDisplaySettings.DisplayAsOptional)
                    {
                        optional = true;
                    }
                    else
                    {
                        optional = false;
                    }
                }

                paramSettings.Optional = optional;
                paramSettings.Info = param;
                paramSettings.AlteredParameterSettings = alteredParamDisplaySettings;
                paramSettings.Name = param.Name;

                // name is null on root item, so this will work in nonsubordinate cases
                paramTreeRoot.FindBreadthFirst(x => x.Value.Name == alteredParamDisplaySettings?.SubordinateTo).Add(paramSettings);
            }
            StringBuilder invocationString = new StringBuilder();
            invocationString.Append('`').Append(commandInvocation);

            void ProcessParamNode(Utilities.SimpleNode<ExtendedParameterInfo> node, bool rootLevel)
            {
                var param = node.Value;
                var paramInfo = param.Info;

                invocationString.Append(' ');
                invocationString.Append(param.Optional ? "[" : (rootLevel ? "<" : ""));

                var paramDescStringBuilder = new StringBuilder("`");

                if (param.Optional)
                {
                    paramDescStringBuilder.Append("[Optional] ");
                }

                paramDescStringBuilder.Append(paramInfo.Type.Name).Append(' ');
                paramDescStringBuilder.Append(param.Name);
                invocationString.Append(param.Name);
                if (paramInfo.DefaultValue != null)
                {
                    paramDescStringBuilder.Append(" = ").Append(paramInfo.DefaultValue);
                    invocationString.Append('=').Append(paramInfo.DefaultValue);
                }
                if (paramInfo.IsRemainder || paramInfo.IsMultiple)
                {
                    paramDescStringBuilder.Append("...");
                    invocationString.Append("...");
                }

                paramDescStringBuilder.Append("` - ");
                paramDescStringBuilder.Append(paramInfo.Summary ?? "*No description provided.*");

                paramDescs[paramInfo] = paramDescStringBuilder.ToString();

                foreach (var childParam in node.Children)
                {
                    ProcessParamNode(childParam, false);
                }

                invocationString.Append(param.Optional ? "]" : (rootLevel ? ">" : ""));
            }

            foreach (var param in paramTreeRoot.Children)
            {
                ProcessParamNode(param, true);
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
                description.Append(paramDescs[param]).AppendLine();
            }

            embed.AddField("__" + invocationString.ToString() + "__", description.ToString());
        }

        private async Task<IResult> HelpCommandAsync(ICommandContext context, object[] parameters, IServiceProvider services, KeyValuePair<CommandInfo, string>[] aliasedCommands)
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
            foreach (var kvp in aliasedCommands)
            {
                BuildHelpAsField(builder, kvp.Key, kvp.Value);
            }

            await context.Channel.SendMessageAsync(string.Empty, embed: builder.Build()).ConfigureAwait(false);
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
                    Models.Guild guildSettings = await _database.FindOneAsync<Models.Guild>(g => g.Id == messageGuildChannel.Guild.Id).ConfigureAwait(false);
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
            var result = await _commands.ExecuteAsync(context, argPos, _provider).ConfigureAwait(false);

            if (result.Error.HasValue &&
                result.Error.Value == CommandError.UnknownCommand)
            {
                return;
            }
            else if (!result.IsSuccess)
            {
                await context.Channel.SendMessageAsync(string.Empty,
                    embed: new EmbedBuilder()
                        .WithColor(Color.Red)
                        .WithTitle("Error Executing Command" + (result.Error.HasValue ? ": " + ((result.Error.Value == CommandError.Exception && result is ExecuteResult ? new Nullable<ExecuteResult>((ExecuteResult)result) : null)?.Exception?.GetType()?.Name ?? result.Error.Value.ToString()).ToStringCamelCaseToSpace() : string.Empty))
                        .WithDescription(result.ErrorReason ?? "An unknown error occurred.")
                        .WithTimestamp(message.CreatedAt)).ConfigureAwait(false);
            }
        }

    }
}
