using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberPatriot.Models
{
    public class CompleteScoreboardSummary : ICloneable
    {
        public IAsyncEnumerable<ScoreboardSummaryEntry> TeamList { get; set; }
        public DateTimeOffset SnapshotTimestamp { get; set; }
        public Division? DivisionFilter { get; set; }
        public string TierFilter { get; set; }
        public Uri OriginUri { get; set; }

        public CompleteScoreboardSummary Clone()
        {
            return new CompleteScoreboardSummary
            {
                TeamList = TeamList,
                SnapshotTimestamp = SnapshotTimestamp,
                DivisionFilter = DivisionFilter,
                TierFilter = TierFilter,
                OriginUri = OriginUri
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public CompleteScoreboardSummary WithFilter(Division? newDivisionFilter, string newTierFilter)
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
            TeamList = newTeamList;
            return this;
        }

        public async System.Threading.Tasks.Task<CompleteScoreboardSummary> WithInternalListAsync()
        {
            if (!(TeamList is IList<ScoreboardSummaryEntry>))
            {
                // ToAsyncEnumerable uses an optimized IAsyncEnumerable which implements IList
                TeamList = (await TeamList.ToList()).ToAsyncEnumerable();
            }

            return this;
        }
    }
}