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

        public CompleteScoreboardSummary WithFilter(ScoreboardFilterInfo newFilter)
        {
            if (newFilter == null)
            {
                throw new ArgumentNullException(nameof(newFilter));
            }

            if (Filter.Division.HasValue && Filter.Division != newFilter.Division)
            {
                throw new ArgumentException("Cannot change an existing DivisionFilter.");
            }
            if (Filter.Tier.HasValue && Filter.Tier != newFilter.Tier)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            if (Filter.Category.HasValue && Filter.Category != newFilter.Category)
            {
                throw new ArgumentException("Cannot change an existing CategoryFilter.");
            }
            if (Filter.Location != null && Filter.Location != newFilter.Location)
            {
                throw new ArgumentException("Cannot change an existing LocationFilter.");
            }
            IEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;

            if (newFilter.Division.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == newFilter.Division.Value);
            }
            if (newFilter.Tier.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == newFilter.Tier.Value);
            }
            if (newFilter.Category.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Category == newFilter.Category.Value);
            }
            if (newFilter.Location != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Location == newFilter.Location);
            }

            Filter = newFilter;
            TeamList = newTeamList as IList<ScoreboardSummaryEntry> ?? newTeamList.ToList();
            return this;
        }
    }
}