using System.Collections.Generic;

namespace CyberPatriot.Services.Metadata
{
    public interface IScoreRetrieverMetadata
    {
        bool IsDynamic { get; }
        bool SupportsInexpensiveDetailQueries { get; }
        string StaticSummaryLine { get; }
        ScoreFormattingOptions FormattingOptions { get; }
    }
}