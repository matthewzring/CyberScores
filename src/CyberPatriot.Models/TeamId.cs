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
using CyberPatriot.Models.Serialization;

namespace CyberPatriot.Models
{
    /// <summary>
    /// A team identification number. Comprised of two parts: a competition identifier, and a team number. These parts are joined with a hyphen.
    /// </summary>
    /// <example>
    /// 10-0001
    /// </example>
    /// <example>
    /// 11-1234
    /// </example>
    /// <example>
    /// CT-001
    /// </example>
    /// <remarks>
    /// This structure is immutable.
    /// </remarks>
    [Newtonsoft.Json.JsonConverter(typeof(TeamIdJsonConverter))]
    [System.ComponentModel.TypeConverter(typeof(TeamIdTypeConverter))]
    public struct TeamId : IComparable<TeamId>
    {
        public string CompetitionIdentifier { get; }
        public int TeamNumber { get; }

        private readonly int digitCount;

        public TeamId(string competitionIdentifier, int teamNumber)
        {
            if (!CompetitionIdentificationValidator.TryGetTeamIdNumberLength(competitionIdentifier, out digitCount))
            {
                throw new ArgumentOutOfRangeException(nameof(competitionIdentifier));
            }
            if (teamNumber <= 0 || teamNumber > CompetitionIdentificationValidator.GetMaximumNumericId(digitCount))
            {
                throw new ArgumentOutOfRangeException(nameof(teamNumber));
            }
            CompetitionIdentifier = competitionIdentifier;
            TeamNumber = teamNumber;
        }

        private TeamId(string competitionIdentifier, int teamNumber, int digitCt)
        {
            digitCount = digitCt;
            TeamNumber = teamNumber;
            CompetitionIdentifier = competitionIdentifier;
        }

        public int CompareTo(TeamId other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (CompetitionIdentifier != other.CompetitionIdentifier)
            {
                throw new ArgumentException("Cannot compare team IDs from different competitions.");
            }

            return TeamNumber.CompareTo(other.TeamNumber);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = -1305224960;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(CompetitionIdentifier);
                hashCode = (hashCode * -1521134295) + TeamNumber.GetHashCode();
                return hashCode;
            }
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

            return t.CompetitionIdentifier == CompetitionIdentifier && t.TeamNumber == TeamNumber;
        }

        public override string ToString()
        {
            return string.Format("{0}-{1}", CompetitionIdentifier, TeamNumber.ToString(CompetitionIdentificationValidator.GetFormatString(digitCount)));
        }

        public static bool TryParse(string idString, out TeamId teamId)
        {
            teamId = default(TeamId);
            // length must be at least 3: 1 or more chars competition ID, 1 char '-', 1 or more chars numeric ID
            if (idString == null || idString.Length < 3)
            {
                return false;
            }

            var competitionIdBuilder = new System.Text.StringBuilder(idString.Length / 2);
            string competitionId = null;
            int numericId = 0;
            int expectedDigitCt = 0;
            int receivedDigitCt = 0;

            // parser state
            int state = 0;

            foreach (char c in idString)
            {
                switch (state)
                {
                    case 0:
                        // reading competition ID
                        if (c != '-')
                        {
                            competitionIdBuilder.Append(c);
                        }
                        else
                        {
                            state = 1;
                            competitionId = competitionIdBuilder.ToString();
                            if (!CompetitionIdentificationValidator.TryGetTeamIdNumberLength(competitionId, out expectedDigitCt))
                            {
                                return false;
                            }
                        }
                        break;
                    case 1:
                        // reading numeric ID
                        if (c >= '0' && c <= '9')
                        {
                            numericId *= 10;
                            numericId += c - '0';
                            receivedDigitCt++;
                        }
                        else
                        {
                            return false;
                        }
                        break;
                }
            }

            if (state == 0 || receivedDigitCt != expectedDigitCt)
            {
                return false;
            }
            
            teamId = new TeamId(competitionId, numericId, expectedDigitCt);

            return true;
        }

        public static TeamId Parse(string s)
        {
            if (!TryParse(s, out TeamId retVal))
            {
                throw new FormatException("Input string was not in a correct team ID format.");
            }
            return retVal;
        }

        public static bool operator ==(TeamId a, TeamId b) => a.Equals(b);

        public static bool operator !=(TeamId a, TeamId b) => !a.Equals(b);

        public static bool operator <(TeamId a, TeamId b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(TeamId a, TeamId b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(TeamId a, TeamId b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(TeamId a, TeamId b)
        {
            return a.CompareTo(b) >= 0;
        }
    }
}