using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberPatriot.Models
{
    public class CompleteScoreboardSummary
    {
        public IAsyncEnumerable<ScoreboardSummaryEntry> TeamList { get; set; }
        public DateTimeOffset SnapshotTimestamp { get; set; }
        public Division? DivisionFilter { get; set; }
        public string TierFilter { get; set; }
        public Uri OriginUri { get; set; }

        public CompleteScoreboardSummary Filter(Division? newDivisionFilter, string newTierFilter)
        {
            if (DivisionFilter.HasValue && DivisionFilter != newDivisionFilter)
            {
                throw new ArgumentException("Cannot change an existing DivisionFilter.");
            }
            if (TierFilter != null && TierFilter != newTierFilter)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            IAsyncEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;
            if (newDivisionFilter != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == newDivisionFilter.Value);
            }
            if (newTierFilter != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == newTierFilter);
            }
            return new CompleteScoreboardSummary
            {
                TeamList = newTeamList,
                SnapshotTimestamp = SnapshotTimestamp,
                DivisionFilter = newDivisionFilter,
                TierFilter = newTierFilter
            };
        }
    }
}