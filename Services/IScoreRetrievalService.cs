using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IScoreRetrievalService
    {
        IAsyncEnumerable<ScoreboardSummary> GetScoreboard();
        IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter);
        IAsyncEnumerable<ScoreboardSummary> GetScoreboard(Division divisionFilter, string tierFilter);
        Task<ScoreboardDetails> GetDetails(TeamID team);
    }
}