using System;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class ArchivalHttpScoreboardScoreRetrievalService : HttpScoreboardScoreRetrievalService
    {
        protected class ArchivalHttpScoreboardMetadata : HttpScoreboardScoreRetrievalService.HttpPassthroughScoreRetrieverMetadata
        {
            public ArchivalHttpScoreboardMetadata(HttpScoreboardScoreRetrievalService scoreRetriever) : base(scoreRetriever)
            {
            }

            public override string StaticSummaryLine => $"Archive at {ScoreRetriever.Hostname}";
            public override bool IsDynamic => false;
        }

        public ArchivalHttpScoreboardScoreRetrievalService() : this(null)
        {

        }

        public ArchivalHttpScoreboardScoreRetrievalService(string hostname) : base(hostname)
        {
            Metadata = new ArchivalHttpScoreboardMetadata(this);
        }


        protected override Uri BuildDetailsUri(TeamId team) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/{team}.html");

        protected override Uri BuildScoreboardUri(Division? divisionFilter, Tier? tierFilter) => new Uri($"https://{Hostname}/cpix/r4_html_scoreboard/");
    }
}