using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;

namespace CyberPatriot.Services
{
    public interface IScoreRetrievalService
    {
        Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter);
        Task<ScoreboardDetails> GetDetailsAsync(TeamId team);
        Task InitializeAsync(IServiceProvider provider, IConfigurationSection config);
        CompetitionRound Round { get; }
        Metadata.IScoreRetrieverMetadata Metadata { get; }
    }
}