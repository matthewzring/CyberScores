using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    public interface ICompetitionRoundLogicService
    {
        CompetitionRound InferRound(DateTimeOffset date);

        IList<ScoreboardSummaryEntry> GetPeerTeams(CompetitionRound round, CompleteScoreboardSummary divisionScoreboard, ScoreboardSummaryEntry teamInfo);

        string GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team);

        TeamDetailRankingInformation GetRankingInformation(CompetitionRound round, CompleteScoreboardSummary divisionScoreboard, ScoreboardSummaryEntry teamInfo);

        int? GetCiscoPointsPossible(CompetitionRound round);
    }

    public abstract class CyberPatriotCompetitionRoundLogicService : ICompetitionRoundLogicService
    {
        public virtual string GetEffectiveDivisionDescriptor(ScoreboardSummaryEntry team) => team.Category ?? team.Division.ToStringCamelCaseToSpace();

        public virtual int? GetCiscoPointsPossible(CompetitionRound round) => null;

        public abstract CompetitionRound InferRound(DateTimeOffset date);

        public abstract IList<ScoreboardSummaryEntry> GetPeerTeams(CompetitionRound round, CompleteScoreboardSummary divisionScoreboard, ScoreboardSummaryEntry teamInfo);

        private static Func<ScoreboardSummaryEntry, bool> BuildSummaryComparer(TeamId target) => sse => sse.TeamId == target;

        public virtual TeamDetailRankingInformation GetRankingInformation(CompetitionRound round, CompleteScoreboardSummary divisionScoreboard, ScoreboardSummaryEntry teamInfo)
        {
            divisionScoreboard = divisionScoreboard.Clone().WithFilter(teamInfo.Division, null);

            // may be equal to division scoreboard, that's fine
            var tierScoreboard = divisionScoreboard.Clone().WithFilter(teamInfo.Division, teamInfo.Tier);
            var peers = GetPeerTeams(round, divisionScoreboard, teamInfo);

            var summaryComparer = BuildSummaryComparer(teamInfo.TeamId);
            return new TeamDetailRankingInformation()
            {
                TeamId = teamInfo.TeamId,
                Peers = peers,
                PeerIndex = peers.IndexOfWhere(summaryComparer),
                PeerCount = peers.Count,
                DivisionIndex = divisionScoreboard.TeamList.IndexOfWhere(summaryComparer),
                DivisionCount = divisionScoreboard.TeamList.Count,
                TierIndex = tierScoreboard.TeamList.IndexOfWhere(summaryComparer),
                TierCount = tierScoreboard.TeamList.Count
            };
        }
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

        public override IList<ScoreboardSummaryEntry> GetPeerTeams(CompetitionRound round, CompleteScoreboardSummary divisionScoreboard, ScoreboardSummaryEntry teamDetails)
        {
            // make a clone because we'll mutate this later
            divisionScoreboard = divisionScoreboard.Clone().WithFilter(teamDetails.Division, null);
            if (teamDetails.Division == Division.MiddleSchool)
            {
                // middle school doesn't have tiers or categories
                return divisionScoreboard.TeamList;
            }

            // open/service

            if ((teamDetails.Division == Division.Open && round > CompetitionRound.Round2) || (teamDetails.Division == Division.AllService && round == CompetitionRound.Round3))
            {
                // In open past R2, tier matters, but that's it
                // In all service R3, category doesn't* matter, just tier
                // See issue #14
                return divisionScoreboard.WithFilter(teamDetails.Division, teamDetails.Tier).TeamList;
            }

            // open/service, service: category matters; open: no tiers
            if (teamDetails.Division == Division.Open)
            {
                // unknown round - if our candidate team has a tier, filter by tier, otherwise return the whole division
                if (round == 0 && teamDetails.Tier != null)
                {
                    return divisionScoreboard.WithFilter(teamDetails.Division, teamDetails.Tier).TeamList;
                }

                // either R1 or R2
                // safe to return the whole division as a peer list
                return divisionScoreboard.TeamList;
            }

            // all-service round where category matters ("R0" we default to factoring in category)

            // filter by tier, where available
            if (round > CompetitionRound.Round2)
            {
                divisionScoreboard.WithFilter(Division.AllService, teamDetails.Tier);
            }

            // just need to filter the list by category
            if (teamDetails.Category == null)
            {
                // silent fail
                return divisionScoreboard.TeamList;
            }

            // there might be some A.S. teams whose categories we don't know
            // they get treated as not-my-problem, that is, not part of my category
            return divisionScoreboard.TeamList.Where(t => t.Category == teamDetails.Category).ToIList();
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
    }
}