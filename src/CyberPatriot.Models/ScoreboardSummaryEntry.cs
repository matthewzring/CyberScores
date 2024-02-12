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

namespace CyberPatriot.Models
{
    public class ScoreboardSummaryEntry
    {
        public TeamId TeamId { get; set; }
        public string Location { get; set; }
        public ServiceCategory? Category { get; set; }
        public Division Division { get; set; }
        public Tier? Tier { get; set; }
        public int ImageCount { get; set; }
        public TimeSpan PlayTime { get; set; }
        public TimeSpan ScoreTime { get; set; }
        public double TotalScore { get; set; }
        public ScoreWarnings Warnings { get; set; }
        public Advancement? Advancement { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (TeamId != null ? TeamId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Location != null ? Location.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Category != null ? Category.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)Division;
                hashCode = (hashCode * 397) ^ (Tier != null ? Tier.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ ImageCount;
                hashCode = (hashCode * 397) ^ PlayTime.GetHashCode();
                hashCode = (hashCode * 397) ^ TotalScore.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)Warnings;
                hashCode = (hashCode * 397) ^ (Advancement != null ? Advancement.GetHashCode() : 0);
                return hashCode;
            }
        }

        protected bool Equals(ScoreboardSummaryEntry other)
        {
            return TeamId == other.TeamId && string.Equals(Location, other.Location) && string.Equals(Category, other.Category) && Division == other.Division && Tier == other.Tier && ImageCount == other.ImageCount && PlayTime.Equals(other.PlayTime) && TotalScore == other.TotalScore && Warnings == other.Warnings && Advancement == other.Advancement;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ScoreboardSummaryEntry)obj);
        }
    }
}
