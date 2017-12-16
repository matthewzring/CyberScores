using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IScoreRetrievalService
    {
        // I don't feel great about a Task wrapping an IAsyncEnumerable
        // we do it this way so that SnapshotTimestamp can be set accurately,
        // that is to say, based on HTML document contents, which we need to await for first
        Task<CompleteScoreboardSummary> GetScoreboardAsync(Division? divisionFilter, string tierFilter);
        Task<ScoreboardDetails> GetDetailsAsync(TeamId team);
    }
}