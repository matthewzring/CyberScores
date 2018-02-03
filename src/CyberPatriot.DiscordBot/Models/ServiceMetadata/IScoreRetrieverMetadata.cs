using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public interface IScoreRetrieverMetadata
    {
        bool IsDynamic { get; }
        string StaticSummaryLine { get; }
        ScoreFormattingOptions FormattingOptions { get; }
    }
}