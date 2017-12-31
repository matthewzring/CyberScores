using System;

namespace CyberPatriot.DiscordBot
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

        public Func<int, string> FormatScore { get; set; } = i => i.ToString();
        public Func<int, string> FormatLabeledScoreDifference { get; set; } = i => Utilities.Pluralize("point", i);
        public Func<int, string> FormatScoreForLeaderboard { get; set; } = i => i.ToString();
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