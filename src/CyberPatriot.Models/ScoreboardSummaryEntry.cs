using System;

namespace CyberPatriot.Models
{
    public class ScoreboardSummaryEntry
    {
        public TeamId TeamId { get; set; }
        public string Location { get; set; }
        public string Category { get; set; }
        public Division Division { get; set; }
        public Tier? Tier { get; set; }
        public int ImageCount { get; set; }
        public TimeSpan PlayTime { get; set; }
        public int TotalScore { get; set; }
        public ScoreWarnings Warnings { get; set; }
        public Advancement? Advancement { get; set; }
        public AuxiliaryScoreComponents AuxiliaryScoreComponents { get; set; }

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
                hashCode = (hashCode * 397) ^ TotalScore;
                hashCode = (hashCode * 397) ^ (int)Warnings;
                hashCode = (hashCode * 397) ^ (Advancement != null ? Advancement.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ AuxiliaryScoreComponents.GetHashCode();
                return hashCode;
            }
        }

        protected bool Equals(ScoreboardSummaryEntry other)
        {
            return TeamId == other.TeamId && string.Equals(Location, other.Location) && string.Equals(Category, other.Category) && Division == other.Division && Tier == other.Tier && ImageCount == other.ImageCount && PlayTime.Equals(other.PlayTime) && TotalScore == other.TotalScore && Warnings == other.Warnings && Advancement == other.Advancement && AuxiliaryScoreComponents.Equals(other.AuxiliaryScoreComponents);
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