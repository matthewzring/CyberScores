#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;

namespace CyberPatriot.Models
{
    /// <summary>
    /// Validates and provides formatting information for <see cref="TeamId"/> objects.
    /// Formatting depends upon <see cref="TeamId.CompetitionIdentifier"/> via this class.
    /// </summary>
    public static class CompetitionIdentificationValidator
    {
        /// <summary>
        /// Attempts to get the number of digits in the numeric part of the given competition's IDs.
        /// Returns false if the given competition is invalid.
        /// </summary>
        /// <param name="competitionId">The identifier for the competition/</param>
        /// <param name="digits">The output variable for the number of digits in this competition's IDs.</param>
        /// <returns>True if and only if the given competition is valid and has a defined, known digit length for its IDs.</returns>
        public static bool TryGetTeamIdNumberLength(string competitionId, out int digits)
        {
            if (competitionId == null)
            {
                digits = 0;
                return false;
            }

            // all currently supported IDs are length 2
            if (competitionId.Length != 2)
            {
                digits = 0;
                return false;
            }

            // ##-####
            // two-number competition ID = CyberPatriot (a season number)
            if (char.IsDigit(competitionId[0]) && char.IsDigit(competitionId[1]))
            {
                digits = 4;
                return true;
            }

            // special cases
            switch (competitionId)
            {
                // CyberCenturion
                case "CC":
                    digits = 4;
                    return true;
                // CyberTaipan
                case "CT":
                    digits = 3;
                    return true;
            }

            digits = 0;
            return false;
        }

        private static void ValidateDigitsCount(int digits, string paramName = "digits")
        {
            if (digits <= 0)
            {
                throw new ArgumentOutOfRangeException(paramName, digits, "The digit count must be greater than zero.");
            }
        }

        /// <summary>
        /// Gets the maximum numeric ID component given a maximum number of digits for that component.
        /// </summary>
        /// <param name="digits">The number of digits in a numeric ID. Must be a whole number.</param>
        /// <returns>The highest valid numeric ID component with the given number of digits.</returns>
        public static int GetMaximumNumericId(int digits)
        {
            ValidateDigitsCount(digits);

            int retVal = 1;
            while (digits > 0)
            {
                retVal *= 10;
                digits--;
            }

            return retVal - 1;
        }

        /// <summary>
        /// Gets an <see cref="int.ToString(string)"/> format string for numeric components of team IDs.
        /// </summary>
        /// <param name="digits">The number of digits in a numeric ID. Must be a whole number.</param>
        /// <returns></returns>
        public static string GetFormatString(int digits)
        {
            ValidateDigitsCount(digits);

            // common digits values
            switch (digits)
            {
                case 3:
                    return "000";
                case 4:
                    return "0000";
                default:
                    return new string('0', digits);
            }
        }
    }
}
