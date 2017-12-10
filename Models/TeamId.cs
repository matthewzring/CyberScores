using System;
namespace CyberPatriot.Models
{
    /// <summary>
    /// An immutable team identifier.
    /// </summary>
    public class TeamId : IComparable<TeamId>
    {
        public int SeasonId { get; }
        public int TeamNumber { get; }

        public TeamId(int seasonId, int teamNumber)
        {
            if (seasonId <= 0 || seasonId >= 100)
            {
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            }
            if (teamNumber <= 0 || teamNumber >= 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(teamNumber));
            }
            SeasonId = seasonId;
            TeamNumber = teamNumber;
        }

        public int CompareTo(TeamId other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (SeasonId != other.SeasonId)
            {
                throw new ArgumentException("Cannot compare team IDs from different seasons.");
            }

            return TeamNumber.CompareTo(other.TeamNumber);
        }

        public override int GetHashCode()
        {
            // due to structure of the teamID we can make the hashcode a unique integer representation of the teamID
            // 1 <= seasonID < 100
            // 1 <= teamNumber < 10000
            // not sure how this affects distribution of hashcodes
            return SeasonId << 16 | TeamNumber;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is TeamId t))
            {
                return false;
            }

            return t.SeasonId == SeasonId && t.TeamNumber == TeamNumber;
        }

        public override string ToString()
        {
            return string.Format("{0:00}-{1:0000}", SeasonId, TeamNumber);
        }


        private static readonly System.Text.RegularExpressions.Regex ParseRegex = new System.Text.RegularExpressions.Regex(@"^(\d{2})-(\d{4})$");

        public static bool TryParse(string idString, out TeamId teamId)
        {
            teamId = null;
            if (idString == null) {
                return false;
            }
            var regexMatch = ParseRegex.Match(idString);
            if (!regexMatch.Success) {
                return false;
            }

            teamId = new TeamId(int.Parse(regexMatch.Groups[1].Value), int.Parse(regexMatch.Groups[2].Value));

            return true;
        }

        public static bool operator ==(TeamId a, TeamId b)
        {
            if (object.ReferenceEquals(a, null))
            {
                return object.ReferenceEquals(b, null);
            }
            return a.Equals(b);
        }

        public static bool operator !=(TeamId a, TeamId b)
        {
            return !(a == b);
        }

        public static bool operator <(TeamId a, TeamId b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) < 0;
        }

        public static bool operator >(TeamId a, TeamId b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) > 0;
        }

        public static bool operator <=(TeamId a, TeamId b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) <= 0;
        }

        public static bool operator >=(TeamId a, TeamId b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) >= 0;
        }
    }
}