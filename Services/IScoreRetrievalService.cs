using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IScoreRetrievalService
    {
        Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter);
        Task<ScoreboardDetails> GetDetailsAsync(TeamId team);
    }
}