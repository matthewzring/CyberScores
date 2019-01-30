using System;
using System.Collections.Generic;
using System.Text;
using CyberPatriot.Models;

namespace CyberPatriot.Services
{
    /// <summary>
    /// A stub class which provides preconfigured values for competition information and passes through the remaining queries to another competition round logic provider.
    /// </summary>
    public class PreconfiguredRoundPassthroughCompetitionRoundLogicService : ICompetitionRoundLogicService
    {
        protected CompetitionRound preconfiguredRound;
        protected ICompetitionRoundLogicService underlyingService;

        public PreconfiguredRoundPassthroughCompetitionRoundLogicService(CompetitionRound round, ICompetitionRoundLogicService underlying)
        {
            preconfiguredRound = round;
            underlyingService = underlying;
        }
        
        double ICompetitionRoundLogicService.GetCiscoPointsPossible(CompetitionRound round, Division division, Tier? tier) => underlyingService.GetCiscoPointsPossible(round, division, tier);

        string ICompetitionRoundLogicService.GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team) => underlyingService.GetEffectiveDivisionDescriptor(team);

        ScoreboardFilterInfo ICompetitionRoundLogicService.GetPeerFilter(CompetitionRound round, ScoreboardSummaryEntry teamInfo) => underlyingService.GetPeerFilter(round, teamInfo);
        
        public CompetitionRound InferRound(DateTimeOffset date) => preconfiguredRound;
    }
}
