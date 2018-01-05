namespace CyberPatriot.Models
{
    public struct ScoreboardFilterInfo
    {
        public static readonly ScoreboardFilterInfo NoFilter = new ScoreboardFilterInfo(null, null);

        public ScoreboardFilterInfo(Division? divFilter, Tier? tierFilter)
        {
            Division = divFilter;
            Tier = tierFilter;
        }

        public Division? Division { get; private set; }
        public Tier? Tier { get; private set; }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is ScoreboardFilterInfo other))
            {
                return false;
            }

            return Division == other.Division && Tier == other.Tier;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 19;
                hashCode = (hashCode * 401) ^ (Division.HasValue ? Division.Value.GetHashCode() : 0);
                hashCode = (hashCode * 401) ^ (Tier.HasValue ? Tier.Value.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(ScoreboardFilterInfo a, ScoreboardFilterInfo b)
        {
            // value type, no null check
            return a.Equals(b);
        }

        public static bool operator !=(ScoreboardFilterInfo a, ScoreboardFilterInfo b)
        {
            // value type, no null check
            return !a.Equals(b);
        }
    }
}