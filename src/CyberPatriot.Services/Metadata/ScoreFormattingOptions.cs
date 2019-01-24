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