using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberPatriot.Models
{
    public class CompleteScoreboardSummary : ICloneable
    {
        public IList<ScoreboardSummaryEntry> TeamList { get; set; }
        public DateTimeOffset SnapshotTimestamp { get; set; }
        public Uri OriginUri { get; set; }

        public ScoreboardFilterInfo Filter { get; set; } = ScoreboardFilterInfo.NoFilter;


        public CompleteScoreboardSummary Clone()
        {
            return new CompleteScoreboardSummary
            {
                TeamList = TeamList,
                SnapshotTimestamp = SnapshotTimestamp,
                Filter = Filter,
                OriginUri = OriginUri
            };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public CompleteScoreboardSummary WithFilter(ScoreboardFilterInfo filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (Filter.Division.HasValue && Filter.Division != filter.Division)
            {
                throw new ArgumentException("Cannot change an existing DivisionFilter.");
            }
            if (Filter.Tier.HasValue && Filter.Tier != filter.Tier)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            IEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;

            if (filter.Division.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == filter.Division.Value);
            }
            if (filter.Tier.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == filter.Tier.Value);
            }

            Filter = filter;
            TeamList = newTeamList as IList<ScoreboardSummaryEntry> ?? newTeamList.ToList();
            return this;
        }

        public CompleteScoreboardSummary WithFilter(Division? newDivisionFilter, Tier? newTierFilter)
        {
            if (Filter.Division.HasValue && Filter.Division != newDivisionFilter)
            {
                throw new ArgumentException("Cannot change an existing DivisionFilter.");
            }
            if (Filter.Tier.HasValue && Filter.Tier != newTierFilter)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            IEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;
            if (newDivisionFilter.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == newDivisionFilter.Value);
            }
            if (newTierFilter.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == newTierFilter.Value);
            }
            Filter = new ScoreboardFilterInfo(newDivisionFilter, newTierFilter);
            TeamList = newTeamList as IList<ScoreboardSummaryEntry> ?? newTeamList.ToList();
            return this;
        }
    }
}