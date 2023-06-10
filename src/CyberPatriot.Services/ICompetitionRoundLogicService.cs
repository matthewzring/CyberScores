#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.Services
{
    public interface ICompetitionRoundLogicService
    {
        CompetitionRound InferRound(DateTimeOffset date);

        ScoreboardFilterInfo GetPeerFilter(CompetitionRound round, ScoreboardSummaryEntry teamInfo);

        string GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team);
        
        /// <summary>
        /// Gets the number of points possible for the Cisco component of competition in the given round.
        /// The points returned should be in the same scale as CCS points.
        /// </summary>
        /// <param name="round">The round whose competition structure is being queried.</param>
        /// <param name="division">The division of competition.</param>
        /// <param name="tier">The tier of competition.</param>
        /// <exception cref="ArgumentException">Thrown if the Cisco worth for the given round is unknown.</exception>
        /// <returns>The number of Cisco points possible in the given round for the given division and tier.</returns>
        double GetCiscoPointsPossible(CompetitionRound round, Division division, Tier? tier);

        double GetAdjustPointsPossible(CompetitionRound round, Division division, Tier? tier);

        double GetChallengePointsPossible(CompetitionRound round, Division division, Tier? tier);
    }

    public abstract class CyberPatriotCompetitionRoundLogicService : ICompetitionRoundLogicService
    {
        public virtual string GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team) => team.Category.HasValue ? CyberPatriot.Models.Serialization.ServiceCategoryExtensions.ToCanonicalName(team.Category.Value) : team.Division.ToStringCamelCaseToSpace();

        public abstract double GetCiscoPointsPossible(CompetitionRound round, Division division, Tier? tier);

        public abstract double GetAdjustPointsPossible(CompetitionRound round, Division division, Tier? tier);

        public abstract double GetChallengePointsPossible(CompetitionRound round, Division division, Tier? tier);

        public abstract CompetitionRound InferRound(DateTimeOffset date);
        
        private static Func<ScoreboardSummaryEntry, bool> BuildSummaryComparer(TeamId target) => sse => sse.TeamId == target;
        
        public abstract ScoreboardFilterInfo GetPeerFilter(CompetitionRound round, ScoreboardSummaryEntry teamInfo);
    }

    public class CyberPatriotTenCompetitionRoundLogicService : CyberPatriotCompetitionRoundLogicService
    {
        public override CompetitionRound InferRound(DateTimeOffset date)
        {
            // approximation of eastern time
            // the precision on this estimation is limited anyway
            DateTimeOffset easternDate = date.ToOffset(TimeSpan.FromHours(-5));

            // CP-X only
            if (!((easternDate.Year == 2017 && easternDate.Month > 6) || (easternDate.Year == 2018 && easternDate.Month < 6)))
            {
                // cannot estimate for non-CPX
                return 0;
            }

            int day = easternDate.Day;
            // 1-12
            switch (easternDate.Month)
            {
                case 11:
                    // November, round 1
                    return day == 3 || day == 4 || day == 5 || day == 11 ? CompetitionRound.Round1 : 0;
                case 12:
                    // December, round 2
                    return day == 8 || day == 9 || day == 10 || day == 16 ? CompetitionRound.Round2 : 0;
                case 1:
                    // January, states round
                    return day == 19 || day == 20 || day == 21 || day == 27 ? CompetitionRound.Round3 : 0;
                case 2:
                    // February, semifinals
                    return day == 9 || day == 10 || day == 11 || day == 17 ? CompetitionRound.Semifinals : 0;
            }

            // no round predicted on the given date
            return 0;
        }

        public override ScoreboardFilterInfo GetPeerFilter(CompetitionRound round, ScoreboardSummaryEntry teamDetails)
        {
            if (teamDetails.Division == Division.MiddleSchool)
            {
                // middle school doesn't have tiers or categories
                return new ScoreboardFilterInfo(Division.MiddleSchool, null);
            }

            // open/service

            if ((teamDetails.Division == Division.Open && round > CompetitionRound.Round2) || (teamDetails.Division == Division.AllService && round == CompetitionRound.Round3))
            {
                // In open past R2, tier matters, but that's it
                // In all service R3, category doesn't* matter, just tier
                // See issue #14
                return new ScoreboardFilterInfo(teamDetails.Division, teamDetails.Tier);
            }

            // open/service, service: category matters; open: no tiers
            if (teamDetails.Division == Division.Open)
            {
                // unknown round - if our candidate team has a tier, filter by tier, otherwise return the whole division
                if (round == 0 && teamDetails.Tier != null)
                {
                    return new ScoreboardFilterInfo(teamDetails.Division, teamDetails.Tier);
                }

                // either R1 or R2
                // safe to return the whole division as a peer list
                return new ScoreboardFilterInfo(Division.Open, null);
            }

            // all-service round where category matters ("R0" we default to factoring in category)

            Tier? tierFilter = null;

            // filter by tier, where available
            if (round > CompetitionRound.Round2)
            {
                tierFilter = teamDetails.Tier;
            }
            
            // there might be some A.S. teams whose categories we don't know
            // they get treated as not-my-problem, that is, not part of my category
            // unknown category -> null -> no filtering on that, a good enough fallback
            return new ScoreboardFilterInfo(Division.AllService, tierFilter, teamDetails.Category, null);
        }

        public override double GetCiscoPointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            throw new NotImplementedException("CP-XV Cisco totals are not implemented.");
        }

        public override double GetAdjustPointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            throw new NotImplementedException("CP-XV Adjust totals are not implemented.");
        }

        public override double GetChallengePointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            throw new NotImplementedException("CP-XV Challenge totals are not implemented.");
        }
    }

    // extend CP-X because advancement rules are supposed to be similar; also at time of authorship round dates were not released
    public class CyberPatriotElevenCompetitionRoundLogicService : CyberPatriotTenCompetitionRoundLogicService
    {
        public override CompetitionRound InferRound(DateTimeOffset date)
        {
            // approximation of eastern time
            // the precision on this estimation is limited anyway
            DateTimeOffset easternDate = date.ToOffset(TimeSpan.FromHours(-5));

            // CP-XI only
            if (!((easternDate.Year == 2018 && easternDate.Month > 6) || (easternDate.Year == 2019 && easternDate.Month < 6)))
            {
                // cannot estimate for non-CPXI
                return 0;
            }

            int day = easternDate.Day;
            // 1-12
            switch (easternDate.Month)
            {
                case 11:
                    // November, round 1
                    return day == 2 || day == 3 || day == 4 || day == 10 ? CompetitionRound.Round1 : 0;
                case 12:
                    // December, round 2
                    return day == 7 || day == 8 || day == 9 || day == 15 ? CompetitionRound.Round2 : 0;
                case 1:
                    // January, states round
                    return day == 11 || day == 12 || day == 13 || day == 19 ? CompetitionRound.Round3 : 0;
                case 2:
                    // February, semifinals
                    return day == 1 || day == 2 || day == 3 || day == 9 ? CompetitionRound.Semifinals : 0;
            }

            // no round predicted on the given date
            return 0;
        }

        public override double GetCiscoPointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            // http://www.uscyberpatriot.org/competition/competition-challenges-by-round
            switch (round)
            {
                case CompetitionRound.Round1:
                    return division == Division.MiddleSchool ? 20 : 20;
                case CompetitionRound.Round2:
                    return division == Division.MiddleSchool ? 30 : 30;
                case CompetitionRound.Round3:
                    return division == Division.MiddleSchool ? 30 : 100;
                case CompetitionRound.Semifinals:
                    return division == Division.MiddleSchool ? 30 : tier == Tier.Platinum ? 240 : 100;
            }

            throw new ArgumentException("Unknown round.");
        }

        public override double GetAdjustPointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            // http://www.uscyberpatriot.org/competition/competition-challenges-by-round
            switch (round)
            {
                case CompetitionRound.Round1:
                case CompetitionRound.Round2:
                case CompetitionRound.Round3:
                    return 0;
                case CompetitionRound.Semifinals:
                    return 0;
            }

            throw new ArgumentException("Unknown round.");
        }

        public override double GetChallengePointsPossible(CompetitionRound round, Division division, Tier? tier)
        {
            // http://www.uscyberpatriot.org/competition/competition-challenges-by-round
            switch (round)
            {
                case CompetitionRound.Round1:
                case CompetitionRound.Round2:
                case CompetitionRound.Round3:
                    return 0;
                case CompetitionRound.Semifinals:
                    return tier == Tier.Platinum ? 160 : 0;
            }

            throw new ArgumentException("Unknown round.");
        }
    }
}