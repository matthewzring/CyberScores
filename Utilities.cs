using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using CyberPatriot.DiscordBot.Services;

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

        public static Func<T> ThenPassive<T>(this Func<T> func, Action<T> passiveTransform)
        {
            return new Func<T>(() =>
            {
                T val = func();
                passiveTransform(val);
                return val;
            });
        }
        
        public static Func<TParam, TReturn> ThenPassive<TParam, TReturn>(this Func<TParam, TReturn> func, Action<TParam, TReturn> passiveTransform)
        {
            return new Func<TParam, TReturn>(param =>
            {
                TReturn val = func(param);
                passiveTransform(param, val);
                return val;
            });
        }

        public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> otherSequence)
        {
            foreach (var element in otherSequence)
            {
                collection.Add(element);
            }
        }

        /// <summary>
        /// Finds a read-only predicated list from a data persistence service, managing the context automatically.
        /// ToIList is called on the whole thing.
        /// </summary>
        public static Task<IList<T>> FindAllAsync<T>(this IDataPersistenceService persistence) where T : class
        {
            using (var context = persistence.OpenContext<T>(false))
            {
                // enumeration could be in progress while disposal happens because tasks
                // not a big deal for the LiteDb implementation
                return context.FindAllAsync().ToIListAsync();
            }
        }
        
        /// <summary>
        /// Finds a read-only predicated list from a data persistence service, managing the context automatically.
        /// ToIList is called on the whole thing.
        /// </summary>
        public static Task<IList<T>> FindAllAsync<T>(this IDataPersistenceService persistence, Expression<Func<T, bool>> predicate) where T : class
        {
            using (var context = persistence.OpenContext<T>(false))
            {
                // enumeration could be in progress while disposal happens because tasks
                // not a big deal for the LiteDb implementation
                return context.FindAllAsync(predicate).ToIListAsync();
            }
        }
        
        /// <summary>
        /// Finds a model element.
        /// </summary>
        public static Task<T> FindOneAsync<T>(this IDataPersistenceService persistence, Expression<Func<T, bool>> predicate) where T : class
        {
            using (var context = persistence.OpenContext<T>(false))
            {
                // same issue with task and disposal, see Utilities.FindAllAsync<T>
                return context.FindOneAsync(predicate);
            }
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

        public static string Pluralize(string noun, int quantity, bool prependQuantity = true)
        {
            if (quantity == 1)
            {
                return prependQuantity ? quantity + " " + noun : noun;
            }

            if (noun.EndsWith("ch") || noun.EndsWith("sh") || noun.EndsWith("s") || noun.EndsWith("x") || noun.EndsWith("z"))
            {
                return (prependQuantity ? quantity + " " : string.Empty) + noun + "es";
            }

            // "penalty"
            if (noun.Length >= 2)
            {
                char penultimate = noun[noun.Length - 2];
                if (noun.EndsWith('y') && penultimate != 'a' && penultimate != 'e' && penultimate != 'i' &&
                    penultimate != 'o' && penultimate != 'u')
                {
                    return (prependQuantity ? quantity + " " : string.Empty) + noun.Substring(0, noun.Length - 1) + "ies";
                }

            }
            return (prependQuantity ? quantity + " " : string.Empty) + noun + "s";
        }

        public static void Consume<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }

            using (var enumerator = enumerable.GetEnumerator())
            {
                while (enumerator.MoveNext()) ;
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
        
        public static Task<IList<T>> ToIListAsync<T>(this IAsyncEnumerable<T> enumerable)
        {
            if (enumerable is IList<T> list)
            {
                return Task.FromResult(list);
            }
            
            return enumerable.ToList().ToSuperTask<List<T>, IList<T>>();
        }

        public static async Task<TSuper> ToSuperTask<TDerived, TSuper>(this Task<TDerived> derivedTask) where TDerived : TSuper
        {
            // feels like there should be a nicer way
            return await derivedTask;
        }

        public static class PeriodicTask
        {
            public static async Task Run<TState>(Func<TState, Task> action, TimeSpan period, TState state, System.Threading.CancellationToken cancellationToken)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(period, cancellationToken).ConfigureAwait(false);

                    if (!cancellationToken.IsCancellationRequested)
                        await action(state).ConfigureAwait(false);
                }
            }

            public static Task Run<TState>(Func<TState, Task> action, TimeSpan period, TState state)
            {
                return Run(action, period, state, System.Threading.CancellationToken.None);
            }
        }
    }
}
