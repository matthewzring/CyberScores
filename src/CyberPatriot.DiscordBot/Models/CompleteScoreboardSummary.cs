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

        private ScoreboardFilterInfo filterInfo = ScoreboardFilterInfo.NoFilter;
        public ScoreboardFilterInfo Filter
        {
            get
            {
                return filterInfo;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }
                filterInfo = value;
            }
        }


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
            if (Filter.Tier != null && Filter.Tier != filter.Tier)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            IEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;

            if (filter.Division.HasValue)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == filter.Division.Value);
            }
            if (filter.Tier != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == filter.Tier);
            }
            
            Filter = filter;
            TeamList = newTeamList.ToIList();
            return this;
        }
        
        public CompleteScoreboardSummary WithFilter(Division? newDivisionFilter, string newTierFilter)
        {
            if (Filter.Division.HasValue && Filter.Division != newDivisionFilter)
            {
                throw new ArgumentException("Cannot change an existing DivisionFilter.");
            }
            if (Filter.Tier != null && Filter.Tier != newTierFilter)
            {
                throw new ArgumentException("Cannot change an existing TierFilter.");
            }
            IEnumerable<ScoreboardSummaryEntry> newTeamList = TeamList;
            if (newDivisionFilter != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Division == newDivisionFilter.Value);
            }
            if (newTierFilter != null)
            {
                newTeamList = newTeamList.Where(summary => summary.Tier == newTierFilter);
            }
            Filter = new ScoreboardFilterInfo(newDivisionFilter, newTierFilter);
            TeamList = newTeamList.ToIList();
            return this;
        }
    }
}