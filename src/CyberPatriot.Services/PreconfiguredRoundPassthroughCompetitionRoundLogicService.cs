#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;
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

        double ICompetitionRoundLogicService.GetAdjustPointsPossible(CompetitionRound round, Division division, Tier? tier) => underlyingService.GetAdjustPointsPossible(round, division, tier);

        double ICompetitionRoundLogicService.GetChallengePointsPossible(CompetitionRound round, Division division, Tier? tier) => underlyingService.GetChallengePointsPossible(round, division, tier);

        string ICompetitionRoundLogicService.GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team) => underlyingService.GetEffectiveDivisionDescriptor(team);

        ScoreboardFilterInfo ICompetitionRoundLogicService.GetPeerFilter(CompetitionRound round, ScoreboardSummaryEntry teamInfo) => underlyingService.GetPeerFilter(round, teamInfo);
        
        public CompetitionRound InferRound(DateTimeOffset date) => preconfiguredRound;
    }
}
