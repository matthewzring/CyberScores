using System;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class ArchivalHttpScoreboardScoreRetrievalService : HttpScoreboardScoreRetrievalService
    {
        protected override Uri BuildDetailsUri(TeamId team) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/{team}.html");

        protected override Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/");
    }
}