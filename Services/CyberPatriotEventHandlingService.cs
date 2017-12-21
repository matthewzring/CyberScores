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
        protected Regex _teamUrlRegex;

        public CyberPatriotEventHandlingService(IServiceProvider provider, DiscordSocketClient discord,
            IDataPersistenceService database, IConfiguration config, ScoreboardMessageBuilderService messageBuilder,
            IScoreRetrievalService scoreRetriever)
        {
            _discord = discord;
            _provider = provider;
            _database = database;
            _config = config;
            _messageBuilder = messageBuilder;
            _scoreRetriever = scoreRetriever;

            _discord.MessageReceived += MessageReceived;
            _teamUrlRegex = new Regex("https?://" + _config["defaultScoreboardHostname"].Replace(".", "\\.") +
                                      "/team\\.php\\?team=([0-9]{2}-[0-9]{4})");
        }

        class TimerStateWrapper
        {
            public Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary> PreviousTeamLists;
            public Dictionary<TeamId, int> PreviousTeamListIndexes = new Dictionary<TeamId, int>();
        }

        public Task InitializeAsync(IServiceProvider provider)
        {
            var cts = new CancellationTokenSource();
            _discord.Ready += () =>
            {
                // FIXME exception handling in here
#pragma warning disable 4014
                Utilities.PeriodicTask.Run(TimerTick, TimeSpan.FromMinutes(5), new TimerStateWrapper(), cts.Token);
#pragma warning restore 4014

                return Task.CompletedTask;
            };
            _discord.Disconnected += err =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            };
            return Task.CompletedTask;
        }

        async Task TimerTick(TimerStateWrapper state)
        {
            using (var guildSettingEnumerator = _database.FindAllAsync<Models.Guild>().GetEnumerator())
            {
                Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary> scoreboards =
                    new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();
                Dictionary<TeamId, int> teamIdsToPeerIndexes = new Dictionary<TeamId, int>();
                while (await guildSettingEnumerator.MoveNext())
                {
                    Models.Guild guildSettings = guildSettingEnumerator.Current;

                    if (guildSettings?.ChannelSettings == null || guildSettings.ChannelSettings.Count == 0)
                    {
                        return;
                    }

                    SocketGuild guild = _discord.GetGuild(guildSettings.Id);
                    foreach (var chanSettings in guildSettings.ChannelSettings)
                    {
                        if (chanSettings?.MonitoredTeams == null || chanSettings.MonitoredTeams.Count == 0)
                        {
                            continue;
                        }

                        SocketGuildChannel rawChan = guild.GetChannel(chanSettings.Id);
                        if (!(rawChan is SocketTextChannel chan))
                        {
                            continue;
                        }

                        if (!scoreboards.TryGetValue(ScoreboardFilterInfo.NoFilter,
                            out CompleteScoreboardSummary masterScoreboard))
                        {
                            masterScoreboard = await _scoreRetriever.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
                            scoreboards[ScoreboardFilterInfo.NoFilter] = masterScoreboard;
                        }

                        foreach (TeamId monitored in chanSettings.MonitoredTeams)
                        {
                            int masterScoreboardIndex =
                                masterScoreboard.TeamList.IndexOfWhere(scoreEntry => scoreEntry.TeamId == monitored);
                            if (masterScoreboardIndex == -1)
                            {
                                continue;
                            }
                            ScoreboardSummaryEntry monitoredEntry = masterScoreboard.TeamList[masterScoreboardIndex];
                            var peerFilter = new ScoreboardFilterInfo(monitoredEntry.Division, monitoredEntry.Tier);
                            if (!scoreboards.TryGetValue(peerFilter, out CompleteScoreboardSummary peerScoreboard))
                            {
                                peerScoreboard = masterScoreboard.Clone().WithFilter(peerFilter);
                                scoreboards[peerFilter] = peerScoreboard;
                            }
                            int peerIndex = peerScoreboard.TeamList.IndexOf(monitoredEntry);
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
                                    await chan.SendMessageAsync(announceMessage.ToString(), embed: _messageBuilder
                                        .CreateTeamDetailsEmbed(
                                            await _scoreRetriever.GetDetailsAsync(monitored), masterScoreboard)
                                        .Build());
                                }
                            }
                        }
                    }
                }
                state.PreviousTeamListIndexes = teamIdsToPeerIndexes;
                state.PreviousTeamLists = scoreboards;
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
                            await _scoreRetriever.GetDetailsAsync(TeamId.Parse(scoreboardMatch.Groups[1].Value)))
                        .Build());
            }
        }
    }
}