using System;
using System.Text;

namespace CyberPatriot
{
    public static class Utilities
    {
        public static bool TryParseEnumSpaceless<TEnum>(string value, out TEnum @enum) where TEnum : struct => Enum.TryParse<TEnum>(value.Replace(" ", string.Empty), out @enum);
        public static string ToStringCamelCaseToSpace(this object obj)
        {
            if (obj == null)
            {
                throw new NullReferenceException();
            }

            char[] initStr = obj.ToString().ToCharArray();
            if (initStr.Length <= 1)
            {
                return new string(initStr);
            }
            StringBuilder result = new StringBuilder(initStr.Length);
            result.Append(initStr[0]);
            for (int i = 1; i < initStr.Length; i++)
            {
                if (char.IsUpper(initStr[i]))
                {
                    result.Append(' ');
                }
                result.Append(initStr[i]);
            }
            return result.ToString();
        }
        public static TimeSpan ParseHourMinuteTimespan(string hhmm)
        {
            // works nicely in normal cases but we put it here in case it doesn't
            //return TimeSpan.Parse(hhmm?.Trim() ?? throw new ArgumentNullException(nameof(hhmm)));

            // some teams run > 24 hours
            // these time penalties mean I need an additional half dozen lines of code :(
            if (string.IsNullOrWhiteSpace(hhmm))
            {
                throw new ArgumentNullException(nameof(hhmm));
            }

            string[] hhmmSplit = hhmm.Split(':');

            return new TimeSpan(int.Parse(hhmmSplit[0]),    // hours
                           int.Parse(hhmmSplit[1]),         // minutes
                           0);                              // seconds
        }
        public static bool IsFakeTier(string tierString)
        {
            if (string.IsNullOrWhiteSpace(tierString))
            {
                return true;
            }
            string tier = tierString.Trim().ToLower();
            return tier == "high school" || tier == "middle school";
        }

        public static string ToConciseString(this Models.ScoreWarnings warnings)
        {
            StringBuilder resBuild = new StringBuilder(2);
            if ((warnings & Models.ScoreWarnings.MultiImage) == Models.ScoreWarnings.MultiImage)
            {
                resBuild.Append('M');
            }
            if ((warnings & Models.ScoreWarnings.TimeOver) == Models.ScoreWarnings.TimeOver)
            {
                resBuild.Append('T');
            }
            return resBuild.ToString();
        }

        public static string ToConciseString(this Models.Division division)
        {
            switch (division)
            {
                case Models.Division.Open:
                    return "Open";
                case Models.Division.AllService:
                    return "A.S.";
                case Models.Division.MiddleSchool:
                    return "M.S.";
            }

            throw new ArgumentOutOfRangeException();
        }

        public static string GetOrdinalSuffix(int number)
        {
            switch (number)
            {
                case 1:
                    return "st";
                case 2:
                    return "nd";
                case 3:
                    return "rd";
                default:
                    return "th";
            }
        }

        public static string AppendOrdinalSuffix(int number) => number + GetOrdinalSuffix(number);

        public static int IndexOfWhere<T>(this System.Collections.Generic.IEnumerable<T> enumerable, Func<T, bool> predicate)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                if (predicate(item))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public static string Pluralize(string noun, int quantity)
        {
            if (quantity == 1)
            {
                return noun;
            }

            if (noun.EndsWith("ch") || noun.EndsWith("sh") || noun.EndsWith("s") || noun.EndsWith("x") || noun.EndsWith("z"))
            {
                return noun + "es";
            }

            return noun + "s";
        }
    }
}
