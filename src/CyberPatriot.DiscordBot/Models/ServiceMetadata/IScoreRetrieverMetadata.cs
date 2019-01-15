using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public interface IScoreRetrieverMetadata
    {
        bool IsDynamic { get; }
        bool SupportsInexpensiveDetailQueries { get; }
        string StaticSummaryLine { get; }
        ScoreFormattingOptions FormattingOptions { get; }
    }
}