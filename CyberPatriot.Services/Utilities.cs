using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.Services
{
    internal static class Utilities
    {
        public static bool TryParseEnumSpaceless<TEnum>(string value, out TEnum @enum) where TEnum : struct => Enum.TryParse<TEnum>(value.Replace(" ", string.Empty), out @enum);
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

        public static TimeSpan MultiplyBy(this TimeSpan span, int factor) => new TimeSpan(span.Ticks * factor);

        public static int IndexOfWhere<T>(this IEnumerable<T> enumerable, Func<T, bool> predicate)
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

        public static T SingleIfOne<T>(this IEnumerable<T> sequence, T defVal = default(T))
        {
            using (var enumerator = sequence.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    return defVal;
                }
                T value = enumerator.Current;
                if (enumerator.MoveNext())
                {
                    // more than one
                    return defVal;
                }
                return value;
            }
        }

        public static T Max<T>(params T[] args) where T : struct, IComparable<T>
        {
            T max = args[0];
            for (int i = 0; i < args.Length; i++)
            {
                T entry = args[i];
                if (entry.CompareTo(max) > 0)
                {
                    max = entry;
                }
            }
            return max;
        }

        public static T Min<T>(params T[] args) where T : struct, IComparable<T>
        {
            T min = args[0];
            for (int i = 0; i < args.Length; i++)
            {
                T entry = args[i];
                if (entry.CompareTo(min) < 0)
                {
                    min = entry;
                }
            }
            return min;
        }

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
                if (char.IsUpper(initStr[i]) || char.IsNumber(initStr[i]))
                {
                    result.Append(' ');
                }
                result.Append(initStr[i]);
            }
            return result.ToString();
        }

        public static async Task<string[]> ReadAllLinesAsync(string path, Encoding enc = null)
        {
            var lines = new List<string>();

            using (var filestream = File.OpenRead(path))
            using (var filereader = enc == null ? new StreamReader(filestream) : new StreamReader(filestream, enc))
            {
                while (!filereader.EndOfStream)
                {
                    lines.Add(await filereader.ReadLineAsync().ConfigureAwait(false));
                }
            }

            return lines.ToArray();
        }

        public static async Task<string> ReadAllTextAsync(string path, Encoding enc = null)
        {
            using (var filestream = File.OpenRead(path))
            using (var filereader = enc == null ? new StreamReader(filestream) : new StreamReader(filestream, enc))
            {
                return await filereader.ReadToEndAsync().ConfigureAwait(false);
            }
        }
        
        public static IList<T> ToIList<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is IList<T> list)
            {
                return list;
            }

            return enumerable.ToList();
        }

    }
}
