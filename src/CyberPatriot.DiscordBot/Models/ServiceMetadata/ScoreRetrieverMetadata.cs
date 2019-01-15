using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class ScoreRetrieverMetadata : IScoreRetrieverMetadata
    {
        public bool IsDynamic { get; set; }

        public bool SupportsInexpensiveDetailQueries { get; set; }

        public string StaticSummaryLine { get; set; }

        public ScoreFormattingOptions FormattingOptions { get; set; }
    }
}