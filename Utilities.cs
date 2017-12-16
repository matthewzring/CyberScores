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
            // works nicely but we put it here in case it doesn't
            return TimeSpan.Parse(hhmm?.Trim() ?? throw new ArgumentNullException(nameof(hhmm)));
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
    }
}
