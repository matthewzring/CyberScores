using System;
namespace CyberPatriot.Models
{
    /// <summary>
    /// An immutable team identifier.
    /// </summary>
    public class TeamID : IComparable<TeamID>
    {
        public int SeasonID { get; }
        public int TeamNumber { get; }

        public TeamID(string teamIdStr)
        {
            if (teamIdStr == null)
            {
                throw new ArgumentNullException(nameof(teamIdStr));
            }
        }

        public TeamID(int seasonId, int teamNumber)
        {
            if (seasonId <= 0 || seasonId >= 100)
            {
                throw new ArgumentOutOfRangeException(nameof(seasonId));
            }
            if (teamNumber <= 0 || teamNumber >= 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(teamNumber));
            }
            SeasonID = seasonId;
            TeamNumber = teamNumber;
        }

        public override int GetHashCode()
        {
            // due to structure of the teamID we can make the hashcode a unique integer representation of the teamID
            // 1 <= seasonID < 100
            // 1 <= teamNumber < 10000
            // not sure how this affects distribution of hashcodes
            return SeasonID << 16 | TeamNumber;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is TeamID t))
            {
                return false;
            }

            return t.SeasonID == SeasonID && t.TeamNumber == TeamNumber;
        }

        public override string ToString()
        {
            return string.Format("{0:00}-{1:0000}", SeasonID, TeamNumber);
        }

        public int CompareTo(TeamID other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (SeasonID != other.SeasonID)
            {
                throw new ArgumentException("Cannot compare team IDs from different seasons.");
            }

            return TeamNumber.CompareTo(other.TeamNumber);
        }

        public static bool operator ==(TeamID a, TeamID b)
        {
            if (object.ReferenceEquals(a, null))
            {
                return object.ReferenceEquals(b, null);
            }
            return a.Equals(b);
        }

        public static bool operator !=(TeamID a, TeamID b)
        {
            return !(a == b);
        }

        public static bool operator <(TeamID a, TeamID b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) < 0;
        }

        public static bool operator >(TeamID a, TeamID b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) > 0;
        }

        public static bool operator <=(TeamID a, TeamID b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) <= 0;
        }

        public static bool operator >=(TeamID a, TeamID b)
        {
            return (a ?? throw new NullReferenceException()).CompareTo(b) >= 0;
        }
    }
}