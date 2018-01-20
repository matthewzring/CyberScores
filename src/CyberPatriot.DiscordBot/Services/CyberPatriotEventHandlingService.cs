using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CyberPatriot;
using CyberPatriot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

namespace CyberPatriot.DiscordBot.Services
{
    public class CyberPatriotEventHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private IServiceProvider _provider;
        private IDataPersistenceService _database;
        private IConfiguration _config;
        private ScoreboardMessageBuilderService _messageBuilder;
        private IScoreRetrievalService _scoreRetriever;
        private ICompetitionRoundLogicService _competitionLogic;
        private LogService _logService;
        protected Regex _teamUrlRegex;

        public CyberPatriotEventHandlingService(IServiceProvider provider, DiscordSocketClient discord,
            IDataPersistenceService database, IConfiguration config, ScoreboardMessageBuilderService messageBuilder,
            IScoreRetrievalService scoreRetriever, ICompetitionRoundLogicService competitionLogic, LogService logService)
        {
            _discord = discord;
            _provider = provider;
            _database = database;
            _config = config;
            _messageBuilder = messageBuilder;
            _scoreRetriever = scoreRetriever;
            _competitionLogic = competitionLogic;
            _logService = logService;

            _discord.MessageReceived += MessageReceived;
            _teamUrlRegex = new Regex("https?://" + _config["httpConfig:defaultHostname"].Replace(".", "\\.") +
                                      "/team\\.php\\?team=([0-9]{2}-[0-9]{4})");
        }

        class TimerStateWrapper
        {
            public Dictionary<TeamId, int> PreviousTeamListIndexes = new Dictionary<TeamId, int>();
        }

        public Task InitializeAsync(IServiceProvider provider)
        {
            var cts = new CancellationTokenSource();
            _discord.Ready += () =>
            {
                // FIXME exception handling in here
#pragma warning disable 4014
                Utilities.PeriodicTask.Run(TeamPlacementChangeNotificationTimer, TimeSpan.FromMinutes(5), new TimerStateWrapper(), cts.Token);
                Utilities.PeriodicTask.Run(UpdateGameBasedOnBackend, TimeSpan.FromSeconds(60), cts.Token);

                // set the game initially
                UpdateGameBasedOnBackend();
#pragma warning restore 4014

                return Task.CompletedTask;
            };
            _discord.Disconnected += err =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            };
            _discord.JoinedGuild += async sg =>
            {
                IMessageChannel ch = (sg.DefaultChannel as IMessageChannel ?? await sg.Owner.GetOrCreateDMChannelAsync().ConfigureAwait(false));
                await ch
                    .SendMessageAsync(
                        $"Hello! I am the __unofficial__ CyberPatriot scoreboard Discord bot{(ch is IDMChannel ? ", and I've just been added to your server " + sg.Name?.AppendPrepend("\"") : string.Empty)}. To get started, " +
                        $"give me a prefix:\n\"{_discord.CurrentUser.Mention} admin prefix set <your prefix here>\"\n" +
                        $"You can say \"{_discord.CurrentUser.Mention} about\" to get more info about me, or run \"{_discord.CurrentUser.Mention} help\" for a list of commands.\n\n" +
                        $"Please keep in mind this bot is 100% *unofficial*, and is not in any way affiliated with the Air Force Association or the CyberPatriot program. All scores " +
                        $"reported by the bot, even those marked \"official\", are a best-effort representation of the corresponding AFA-published details - accuracy is not guaranteed. " +
                        $"Refer to the about command for more information.").ConfigureAwait(false);
            };
            return Task.CompletedTask;
        }

        async Task UpdateGameBasedOnBackend()
        {
            try
            {
                string staticSummary = _scoreRetriever.StaticSummaryLine;
                if (staticSummary != null)
                {
                    var summaryLineBuilder = new StringBuilder(staticSummary);
                    if (_scoreRetriever.IsDynamic)
                    {
                        summaryLineBuilder.Append(" - ");
                        var topTeam = (await _scoreRetriever.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false)).TeamList.FirstOrDefault();
                        if (topTeam == null)
                        {
                            summaryLineBuilder.Append("No teams!");
                        }
                        else
                        {
                            summaryLineBuilder.AppendFormat("Top: {0}, {1}pts", topTeam.TeamId, _scoreRetriever.FormattingOptions.FormatScore(topTeam.TotalScore));
                        }
                    }

                    await _discord.SetGameAsync(summaryLineBuilder.ToString()).ConfigureAwait(false);
                }
                else
                {
                    await _discord.SetGameAsync(null).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await _logService.LogApplicationMessageAsync(ex);
            }
        }

        async Task TeamPlacementChangeNotificationTimer(TimerStateWrapper state)
        {
            try
            {
                using (var databaseContext = _database.OpenContext<Models.Guild>(false))
                using (var guildSettingEnumerator = databaseContext.FindAllAsync().GetEnumerator())
                {
                    CompleteScoreboardSummary masterScoreboard = null;
                    Dictionary<TeamId, int> teamIdsToPeerIndexes = new Dictionary<TeamId, int>();
                    while (await guildSettingEnumerator.MoveNext().ConfigureAwait(false))
                    {
                        Models.Guild guildSettings = guildSettingEnumerator.Current;

                        if (guildSettings?.ChannelSettings == null || guildSettings.ChannelSettings.Count == 0)
                        {
                            return;
                        }

                        IGuild guild = _discord.GetGuild(guildSettings.Id);
                        foreach (var chanSettings in guildSettings.ChannelSettings.Values)
                        {
                            if (chanSettings?.MonitoredTeams == null || chanSettings.MonitoredTeams.Count == 0)
                            {
                                continue;
                            }

                            IGuildChannel rawChan = await guild.GetChannelAsync(chanSettings.Id).ConfigureAwait(false);
                            if (!(rawChan is ITextChannel chan))
                            {
                                continue;
                            }

                            masterScoreboard = await _scoreRetriever.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);

                            foreach (TeamId monitored in chanSettings.MonitoredTeams)
                            {
                                int masterScoreboardIndex =
                                    masterScoreboard.TeamList.IndexOfWhere(scoreEntry => scoreEntry.TeamId == monitored);
                                if (masterScoreboardIndex == -1)
                                {
                                    continue;
                                }

                                ScoreboardSummaryEntry monitoredEntry = masterScoreboard.TeamList[masterScoreboardIndex];
                                int peerIndex = _competitionLogic.GetPeerTeams(_scoreRetriever.Round, masterScoreboard, monitoredEntry).IndexOf(monitoredEntry);
                                teamIdsToPeerIndexes[monitored] = peerIndex;

                                // we've obtained all information, now compare to past data
                                if (state.PreviousTeamListIndexes != null &&
                                    state.PreviousTeamListIndexes.TryGetValue(monitored, out int prevPeerIndex))
                                {
                                    int indexDifference = peerIndex - prevPeerIndex;
                                    if (indexDifference != 0)
                                    {
                                        StringBuilder announceMessage = new StringBuilder();
                                        announceMessage.Append("**");
                                        announceMessage.Append(monitored);
                                        announceMessage.Append("**");
                                        if (indexDifference > 0)
                                        {
                                            announceMessage.Append(" rose ");
                                        }
                                        else
                                        {
                                            announceMessage.Append(" fell ");
                                            indexDifference *= -1;
                                        }

                                        announceMessage.Append(Utilities.Pluralize("place", indexDifference));
                                        announceMessage.Append(" to **");
                                        announceMessage.Append(Utilities.AppendOrdinalSuffix(peerIndex + 1));
                                        announceMessage.Append(" place**.");
                                        await chan.SendMessageAsync(
                                        announceMessage.ToString(),
                                        embed: _messageBuilder
                                               .CreateTeamDetailsEmbed(
                                               await _scoreRetriever.GetDetailsAsync(monitored).ConfigureAwait(false),
                                               masterScoreboard)
                                               .Build());
                                    }
                                }
                            }
                        }
                    }

                    state.PreviousTeamListIndexes = teamIdsToPeerIndexes;
                }
            }
            catch (Exception ex)
            {
                await _logService.LogApplicationMessageAsync(ex);
            }
        }

        private async Task MessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            // show embed for team links
            Match scoreboardMatch = _teamUrlRegex.Match(message.Content);
            if (scoreboardMatch != null && scoreboardMatch.Success)
            {
                await message.Channel.SendMessageAsync(string.Empty,
                    embed: _messageBuilder
                        .CreateTeamDetailsEmbed(
                            await _scoreRetriever.GetDetailsAsync(TeamId.Parse(scoreboardMatch.Groups[1].Value)).ConfigureAwait(false))
                        .Build()).ConfigureAwait(false);
            }
        }
    }
}