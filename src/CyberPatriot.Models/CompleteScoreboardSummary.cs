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