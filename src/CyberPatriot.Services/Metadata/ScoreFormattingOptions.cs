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

namespace CyberPatriot.Services.Metadata
{
    public class ScoreFormattingOptions
    {
        public enum NumberDisplayCriteria
        {
            Always,
            WhenNonZero,
            WhenNonNegative,
            Never
        }

        public Func<double, string> FormatScore { get; set; } = i => i.ToString(i % 1 == 0 ? "" : "0.00");
        public Func<double, string> FormatLabeledScoreDifference { get; set; } = i => i.ToString(i % 1 == 0 ? "" : "0.00") + " point" + (i == 1 ? "" : "s");
        public Func<double, string> FormatScoreForLeaderboard { get; set; } = i => i.ToString("0.00");
        public NumberDisplayCriteria TimeDisplay { get; set; } = NumberDisplayCriteria.Always;
        public NumberDisplayCriteria VulnerabilityDisplay { get; set; } = NumberDisplayCriteria.WhenNonZero;

        public static bool EvaluateNumericDisplay(NumberDisplayCriteria criteria, int number)
        {
            switch (criteria)
            {
                case NumberDisplayCriteria.Always:
                    return true;
                case NumberDisplayCriteria.WhenNonNegative:
                    return number >= 0;
                case NumberDisplayCriteria.WhenNonZero:
                    return number != 0;
                case NumberDisplayCriteria.Never:
                    return false;
            }

            throw new ArgumentOutOfRangeException();
        }

        public static bool EvaluateNumericDisplay(NumberDisplayCriteria criteria, double number)
        {
            switch (criteria)
            {
                case NumberDisplayCriteria.Always:
                    return true;
                case NumberDisplayCriteria.WhenNonNegative:
                    return number >= 0;
                case NumberDisplayCriteria.WhenNonZero:
                    return number != 0;
                case NumberDisplayCriteria.Never:
                    return false;
            }

            throw new ArgumentOutOfRangeException();
        }

        public static bool EvaluateNumericDisplay(NumberDisplayCriteria criteria, int a, int b)
        {
            // checks both
            switch (criteria)
            {
                case NumberDisplayCriteria.Always:
                    return true;
                case NumberDisplayCriteria.WhenNonNegative:
                    // both must be nonnegative to DISPLAY
                    return a >= 0 && b >= 0;
                case NumberDisplayCriteria.WhenNonZero:
                    // both must be zero to hide
                    return !(a == 0 && b == 0);
                case NumberDisplayCriteria.Never:
                    return false;
            }

            throw new ArgumentOutOfRangeException();
        }

        public static bool EvaluateNumericDisplay(NumberDisplayCriteria criteria, TimeSpan number)
        {
            switch (criteria)
            {
                case NumberDisplayCriteria.Always:
                    return true;
                case NumberDisplayCriteria.WhenNonNegative:
                    return number >= TimeSpan.Zero;
                case NumberDisplayCriteria.WhenNonZero:
                    return number != TimeSpan.Zero;
                case NumberDisplayCriteria.Never:
                    return false;
            }

            throw new ArgumentOutOfRangeException();
        }
    }
}
