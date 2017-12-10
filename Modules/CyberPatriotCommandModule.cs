using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Modules
{
    public class CyberPatriotCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreRetrievalService { get; set; }

        [Command("team"), Alias("getteam")]
        public async Task GetTeamAsync(TeamId team)
        {
            await ReplyAsync($"Season: {team.SeasonId}, Number: {team.TeamNumber}");
        }
    }
}