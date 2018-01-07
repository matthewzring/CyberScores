using System;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class ArchivalHttpScoreboardScoreRetrievalService : HttpScoreboardScoreRetrievalService
    {
        public override string StaticSummaryLine => $"Archive at {Hostname}";
        public override bool IsDynamic => false;

        protected override Uri BuildDetailsUri(TeamId team) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/{team}.html");

        protected override Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/");
    }
}