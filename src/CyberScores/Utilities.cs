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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CyberScores.Services;
using CyberPatriot.Models;
using CyberPatriot.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CyberScores;

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
            if (char.IsUpper(initStr[i]) || char.IsNumber(initStr[i]))
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

    /// <summary>
    /// Converts this <see cref="TimeSpan"/> into a string with its total hours and total minutes.
    /// Even if it is greater than 24 hours in duration, hours and minutes will still be displayed which total to the entire timespan.
    /// </summary>
    /// <param name="timespan"></param>
    /// <param name="minDigits">The minimum number of hours digits to display.</param>
    /// <returns></returns>
    public static string ToHoursMinutesSecondsString(this TimeSpan timespan, int minDigits = 2)
    {
        long hourTicks = timespan.Ticks - ((timespan.Minutes * TimeSpan.TicksPerMinute) + (timespan.Seconds * TimeSpan.TicksPerSecond) + (timespan.Milliseconds * TimeSpan.TicksPerMillisecond));
        int hours = (int)(hourTicks / TimeSpan.TicksPerHour);

        return $"{hours.ToString(new string('0', minDigits))}:{timespan.Minutes:00}:{timespan.Seconds:00}";
    }

    public static int Clamp(this int i, int lowerInclusive, int upperExclusive)
    {
        if (i < lowerInclusive)
        {
            return lowerInclusive;
        }

        if (i >= upperExclusive)
        {
            return upperExclusive - 1;
        }

        return i;
    }
    public static string ToConciseString(this ScoreWarnings warnings)
    {
        StringBuilder resBuild = new StringBuilder(2);
        if ((warnings & ScoreWarnings.MultiImage) == ScoreWarnings.MultiImage)
        {
            resBuild.Append('M');
        }
        if ((warnings & ScoreWarnings.TimeOver) == ScoreWarnings.TimeOver)
        {
            resBuild.Append('T');
        }
        if ((warnings & ScoreWarnings.Withdrawn) == ScoreWarnings.Withdrawn)
        {
            resBuild.Append('W');
        }
        return resBuild.ToString();
    }

    public static string ToConciseString(this Division division)
    {
        switch (division)
        {
            case Division.Open:
                return "Open";
            case Division.AllService:
                return "A.S.";
            case Division.MiddleSchool:
                return "M.S.";
        }

        throw new ArgumentOutOfRangeException();
    }

    public static string ToConciseString(this Advancement advancement)
    {
        switch (advancement)
        {
            case Advancement.Advances:
                return "ADVN";
            case Advancement.Wildcard:
                return "WILD";
            case Advancement.Eliminated:
                return "ELIM";
        }

        throw new ArgumentOutOfRangeException();
    }

    public static IAsyncEnumerable<T> ToTaskResultEnumerable<T>(this IEnumerable<Task<T>> rootEnum)
    {
        return AsyncEnumerable.Create(_ =>
        {
            IEnumerator<Task<T>> rootEnumerator = rootEnum.GetEnumerator();
            T val = default(T);
            return AsyncEnumerator.Create(async () =>
            {
                if (!rootEnumerator.MoveNext())
                {
                    return false;
                }

                val = await rootEnumerator.Current.ConfigureAwait(false);
                return true;
            }, () => val, () => { rootEnumerator.Dispose(); return default; });
        });
    }

    /*public static IAsyncEnumerable<T> WhereAsync<T>(this IAsyncEnumerable<T> enumerable, Func<T, Task<bool>> predicate)
    {
        return AsyncEnumerable.Create(_ =>
        {
            IAsyncEnumerator<T> rootEnumerator = enumerable.GetEnumerator();
            return AsyncEnumerable.CreateEnumerator(async ct =>
            {
                bool isNext;
                while ((isNext = await rootEnumerator.MoveNext().ConfigureAwait(false)) && !(await predicate(rootEnumerator.Current).ConfigureAwait(false)))
                {

                }
                return isNext;
            }, () => rootEnumerator.Current, () => rootEnumerator.Dispose());
        });
    }*/

    public static async Task<int> SumParallelAsync<T>(this IAsyncEnumerable<T> enumerable, Func<T, Task<int>> transform)
    {
        List<Task<int>> transformTasks = await enumerable.Select(transform).ToListAsync().ConfigureAwait(false);
        int sum = 0;
        while (transformTasks.Count > 0)
        {
            Task<int> completedSum = await Task.WhenAny(transformTasks).ConfigureAwait(false);
            transformTasks.Remove(completedSum);
            // FIXME is interlocked really needed? I doubt it
            Interlocked.Add(ref sum, completedSum.Result);
        }

        return sum;
    }

    public static IAsyncEnumerable<TElem> TaskToAsyncEnumerable<TElem, TEnumerable>(this Task<TEnumerable> enumerableTask) where TEnumerable : IEnumerable<TElem>
        => new AsyncEnumerableTaskWrapper<TElem>(enumerableTask.ToSuperTask<TEnumerable, IEnumerable<TElem>>());

    public static IAsyncEnumerable<TElem> TaskPropertyToAsyncEnumerable<TElem, TEnumerableContainer>(this Task<TEnumerableContainer> containerTask, Func<TEnumerableContainer, IEnumerable<TElem>> selector)
    {
        // TODO error handling
        return new AsyncEnumerableTaskWrapper<TElem>(containerTask.ContinueWith<IEnumerable<TElem>>(finishedTask => selector(finishedTask.Result)));
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

    public static TimeZoneNames.TimeZoneValues GetAbbreviations(this TimeZoneInfo tzInfo) => TimeZoneNames.TZNames.GetAbbreviationsForTimeZone(tzInfo.Id, "en-US");

    public static void AddAll<T>(this ICollection<T> collection, IEnumerable<T> otherSequence)
    {
        foreach (var element in otherSequence)
        {
            collection.Add(element);
        }
    }

    public static IEnumerable<T> Conditionally<T>(this IEnumerable<T> enumerable, bool condition, Func<IEnumerable<T>, IEnumerable<T>> action)
    {
        if (condition)
        {
            return action(enumerable);
        }

        // TODO covariance
        return enumerable;
    }

    public static IEnumerable<TOut> Ternary<TIn, TOut>(this IEnumerable<TIn> enumerable, bool condition, Func<IEnumerable<TIn>, IEnumerable<TOut>> trueAction, Func<IEnumerable<TIn>, IEnumerable<TOut>> falseAction) => condition ? trueAction(enumerable) : falseAction(enumerable);
    public static IAsyncEnumerable<TOut> TernaryAsync<TIn, TOut>(this IEnumerable<TIn> enumerable, bool condition, Func<IEnumerable<TIn>, IAsyncEnumerable<TOut>> trueAction, Func<IEnumerable<TIn>, IAsyncEnumerable<TOut>> falseAction) => condition ? trueAction(enumerable) : falseAction(enumerable);

    /// <summary>
    /// Skips a given number of elements, which are processed specially, and wraps the remainder of the enumerable.
    /// </summary>
    /// <typeparam name="T">The type of elements of the sequence.</typeparam>
    /// <param name=""></param>
    /// <returns></returns>
    public static IEnumerable<T> SkipProcess<T>(this IEnumerable<T> enumerable, int num, Action<T> process)
    {
        if (process == null)
        {
            throw new ArgumentNullException(nameof(process));
        }
        if (num < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(num));
        }

        int i = 0;
        foreach (var elem in enumerable)
        {
            if (i++ < num)
            {
                process(elem);
            }
            else
            {
                yield return elem;
            }
        }
    }

    public static Discord.EmbedBuilder Conditionally(this Discord.EmbedBuilder builder, bool condition, Func<Discord.EmbedBuilder, Discord.EmbedBuilder> action) => condition ? action(builder) : builder;

    public static Discord.EmbedBuilder ForEach<T>(this Discord.EmbedBuilder builder, IEnumerable<T> collection, Action<Discord.EmbedBuilder, T> action)
    {
        foreach (var item in collection)
        {
            action(builder, item);
        }
        return builder;
    }

    public static async Task<Discord.EmbedBuilder> AddField(this Task<Discord.EmbedBuilder> builderTask, Action<Discord.EmbedFieldBuilder> fb)
    {
        return (await builderTask.ConfigureAwait(false)).AddField(fb);
    }

    public static Discord.EmbedBuilder AddInlineField(this Discord.EmbedBuilder builder, string name, object value)
    {
        return builder.AddField(name, value, inline: true);
    }

    public static async Task<Discord.EmbedBuilder> AddFieldAsync(this Discord.EmbedBuilder builder, Func<Discord.EmbedFieldBuilder, Task> fieldBuilder)
    {
        var field = new Discord.EmbedFieldBuilder();
        await fieldBuilder(field).ConfigureAwait(false);
        builder.AddField(field);
        return builder;
    }

    public static async Task<Discord.EmbedBuilder> AddFieldAsync(this Task<Discord.EmbedBuilder> builderTask, Func<Discord.EmbedFieldBuilder, Task> fieldBuilder)
    {
        var field = new Discord.EmbedFieldBuilder();
        var builder = await builderTask.ConfigureAwait(false);
        await fieldBuilder(field);
        builder.AddField(field);
        return builder;
    }

    public static async Task<Discord.Embed> BuildAsync(this Task<Discord.EmbedBuilder> builderTask) => (await builderTask.ConfigureAwait(false)).Build();

    public static string ToLongString(this TimeSpan difference, bool showSeconds = true, bool showZeroValues = false)
    {
        var response = new StringBuilder();
        if (showZeroValues)
        {
            response.Append(difference.Days != 0 ? $"{Pluralize("day", difference.Days)} " : "");
            response.Append(difference.TotalHours >= 1 ? $"{Pluralize("hour", difference.Hours)} " : "");
            response.Append(difference.TotalMinutes >= 1 ? $"{Pluralize("minute", difference.Minutes)} " : "");
            response.Append(showSeconds ? $"{Pluralize("second", difference.Seconds)}" : "");
        }
        else
        {
            response.Append(difference.Days != 0 ? $"{Pluralize("day", difference.Days)} " : "");
            response.Append(difference.Hours != 0 ? $"{Pluralize("hour", difference.Hours)} " : "");
            response.Append(difference.Minutes != 0 ? $"{Pluralize("minute", difference.Minutes)} " : "");
            response.Append((showSeconds || difference.TotalMinutes < 1) && difference.Seconds != 0 ? $"{Pluralize("second", difference.Seconds)}" : "");
        }
        return response.ToString().Trim();
    }

    /// <summary>
    /// Finds a read-only predicated list from a data persistence service, managing the context automatically.
    /// ToIList is called on the whole thing.
    /// </summary>
    public static async Task<IList<T>> FindAllAsync<T>(this IDataPersistenceService persistence) where T : class
    {
        using (var context = persistence.OpenContext<T>(false))
        {
            return await context.FindAllAsync().ToIListAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Finds a read-only predicated list from a data persistence service, managing the context automatically.
    /// ToIList is called on the whole thing.
    /// </summary>
    public static async Task<IList<T>> FindAllAsync<T>(this IDataPersistenceService persistence, Expression<Func<T, bool>> predicate) where T : class
    {
        using (var context = persistence.OpenContext<T>(false))
        {
            return await context.FindAllAsync(predicate).ToIListAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Finds a model element.
    /// </summary>
    public static async Task<T> FindOneAsync<T>(this IDataPersistenceService persistence, Expression<Func<T, bool>> predicate) where T : class
    {
        using (var context = persistence.OpenContext<T>(false))
        {
            return await context.FindOneAsync(predicate).ConfigureAwait(false);
        }
    }

    public static string GetOrdinalSuffix(int number)
    {
        int tensCheck = number % 100;
        if (tensCheck > 10 && tensCheck < 20)
        {
            return "th";
        }

        switch (number % 10)
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

    public static int CeilingDivision(int dividend, int divisor)
    {
        int result = dividend / divisor;
        if (dividend % divisor != 0)
        {
            result++;
        }
        return result;
    }

    public static IEnumerable<T> Except<T>(this IEnumerable<T> enumerable, T element)
    {
        Func<T, T, bool> eqCompare = (a, b) => object.ReferenceEquals(a, null) ? object.ReferenceEquals(b, null) : a.Equals(b);

        foreach (var item in enumerable)
        {
            if (!eqCompare(item, element))
            {
                yield return item;
            }
        }
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
            while (enumerator.MoveNext())
            {
                ;
            }
        }
    }

    public static bool StartsWithWhereElement<T>(this IEnumerable<T> enumerableToCheck, Func<T, bool> predicate, IEnumerable<T> comparisonEnumerable, IEqualityComparer<T> equalityComparer = null)
    {
        equalityComparer = equalityComparer ?? EqualityComparer<T>.Default;
        var filteredCheck = enumerableToCheck.Where(predicate).GetEnumerator();
        var filteredOther = comparisonEnumerable.Where(predicate).GetEnumerator();
        while (filteredOther.MoveNext())
        {
            if (!filteredCheck.MoveNext())
            {
                return false;
            }
            if (!equalityComparer.Equals(filteredCheck.Current, filteredOther.Current))
            {
                return false;
            }
        }

        return true;
    }

    public static IList<T> ToIList<T>(this IEnumerable<T> enumerable)
    {
        if (enumerable is IList<T> list)
        {
            return list;
        }

        return enumerable.ToList();
    }

    public static ValueTask<IList<T>> ToIListAsync<T>(this IAsyncEnumerable<T> enumerable)
    {
        if (enumerable is IList<T> list)
        {
            return new ValueTask<IList<T>>(list);
        }

        return enumerable.ToListAsync().ToSuperTask<List<T>, IList<T>>();
    }

    public static async Task<TSuper> ToSuperTask<TDerived, TSuper>(this Task<TDerived> derivedTask, bool continueOnCapturedContext = false) where TDerived : TSuper
    {
        // feels like there should be a nicer way
        return await derivedTask.ConfigureAwait(continueOnCapturedContext);
    }

    public static async ValueTask<TSuper> ToSuperTask<TDerived, TSuper>(this ValueTask<TDerived> derivedTask, bool continueOnCapturedContext = false) where TDerived : TSuper
    {
        // feels like there should be a nicer way
        return await derivedTask.ConfigureAwait(continueOnCapturedContext);
    }

    public static TService GetRoot<TRoot, TService>(this TRoot service) where TRoot : IComposingService<TService>, TService
    {
        TService serv = service;
        while (serv is IComposingService<TService> tempServ)
        {
            serv = tempServ.Backend;
        }
        return serv;
    }

    public static TService GetFirstFromChain<TService>(this TService chainRoot, Func<TService, bool> predicate, TService defVal = default(TService)) where TService : class
    {
        if (chainRoot == null)
        {
            return defVal;
        }

        TService service = chainRoot;
        do
        {
            if (predicate(service))
            {
                return service;
            }
        } while ((service = (service as IComposingService<TService>)?.Backend) != null);

        return defVal;
    }

    public static IServiceProvider Overlay<TService>(this IServiceProvider provider, TService newService) where TService : class
    {
        var internalServiceDescriptors = provider.GetService<IList<ServiceDescriptor>>();
        if (internalServiceDescriptors != null)
        {
            // we can use .NET's API for this, kind of
            var newServColl = new ServiceCollection();
            foreach (var serviceDescriptor in internalServiceDescriptors)
            {
                if (serviceDescriptor.ServiceType == typeof(TService)
                    || serviceDescriptor.ServiceType == typeof(IList<ServiceDescriptor>))
                {
                    continue;
                }

                ServiceDescriptor newDescriptor = null;
                if (serviceDescriptor.Lifetime == ServiceLifetime.Singleton)
                {
                    try
                    {
                        newDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, provider.GetService(serviceDescriptor.ServiceType));
                    }
                    catch { }
                }

                if (newDescriptor == null)
                {
                    if (serviceDescriptor.ImplementationType != null)
                    {
                        newDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationType, serviceDescriptor.Lifetime);
                    }
                    else if (serviceDescriptor.ImplementationInstance != null && serviceDescriptor.Lifetime == ServiceLifetime.Singleton)
                    {
                        newDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationInstance);
                    }
                    else if (serviceDescriptor.ImplementationFactory != null)
                    {
                        newDescriptor = new ServiceDescriptor(serviceDescriptor.ServiceType, serviceDescriptor.ImplementationFactory, serviceDescriptor.Lifetime);
                    }
                }

                newServColl.Add(newDescriptor);
            }

            newServColl.AddSingleton(typeof(TService), newService);
            return newServColl.BuildServiceProvider();
        }
        else
        {
            // create a thin overlay
            // default to adding to existing overlay, otherwise create the overlay
            var overlay = provider as OverlayServiceProvider;
            if (overlay == null)
            {
                overlay = new OverlayServiceProvider()
                {
                    Parent = provider
                };
            }

            overlay.OverlayedServices[typeof(TService)] = newService;
            return overlay;
        }
    }

    private class OverlayServiceProvider : IServiceProvider
    {
        public Dictionary<Type, object> OverlayedServices { get; } = new Dictionary<Type, object>();
        public IServiceProvider Parent { get; set; }

        public object GetService(Type serviceType)
        {
            if (OverlayedServices.TryGetValue(serviceType, out object service))
            {
                return service;
            }

            return Parent.GetService(serviceType);
        }
    }

    public static string JoinNonNullNonEmpty(string joinString, params object[] objects) => JoinNonNullNonEmpty(joinString, objects?.Select(o => o?.ToString()));

    public static string JoinNonNullNonEmpty(string joinString, IEnumerable<string> objects) => string.Join(joinString, objects?.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static string CoalesceBlank(this string baseString, string alternate) => string.IsNullOrWhiteSpace(baseString) ? alternate : baseString;

    public static string GetAvatarUrlOrDefault(this Discord.IUser user) => (user ?? throw new NullReferenceException()).GetAvatarUrl() ?? $"https://cdn.discordapp.com/embed/avatars/{user.DiscriminatorValue % 5}.png";

    public static string AppendPrepend(this string baseString, string prev, string next = null)
    {
        if (next == null)
        {
            next = prev;
        }

        return prev + baseString + next;
    }

    /// <summary>
    /// Appends and prepends to a string, unless the base string is null or whitespace.
    /// In that case, returns the empty string.
    /// </summary>
    public static string AppendPrependIfNonEmpty(this string baseString, string prev, string next = null)
    {
        if (string.IsNullOrWhiteSpace(baseString))
        {
            return string.Empty;
        }

        return AppendPrepend(baseString, prev, next);
    }

    public static decimal Median(this decimal[] sortedData)
    {
        if (sortedData.Length == 0)
        {
            throw new ArgumentException("Cannot compute the median of a zero-length array.");
        }
        else if (sortedData.Length == 1)
        {
            return sortedData[0];
        }
        else if (sortedData.Length == 2)
        {
            return (sortedData[0] + sortedData[1]) / 2;
        }

        int center = sortedData.Length / 2;

        if (sortedData.Length % 2 == 0)
        {
            return (sortedData[center] + sortedData[center + 1]) / 2;
        }

        return sortedData[center];
    }

    public static decimal StandardDeviation(this decimal[] data)
    {
        if (data.Length == 0)
        {
            throw new ArgumentException("Cannot compute the standard deviation of a zero-length array.");
        }

        decimal average = data.Average();
        decimal sumOfSquaresOfDifferences = data.Select(val => (val - average) * (val - average)).Sum();
        return (decimal)Math.Sqrt((double)(sumOfSquaresOfDifferences / data.Length));
    }

    public static class PeriodicTask
    {
        public static void Run<TState>(Func<TState, Task> action, TimeSpan period, TState state, System.Threading.CancellationToken cancellationToken)
        {
            const int threadSleepChunks = 5;
            TimeSpan chunk = period / threadSleepChunks;

            var t = new Thread(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; i < threadSleepChunks; i++)
                    {
                        Thread.Sleep(chunk);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        action(state).Wait();
                    }
                }
            });

            t.Start();
        }

        public static void Run<TState>(Func<TState, Task> action, TimeSpan period, TState state)
        {
            Run(action, period, state, CancellationToken.None);
        }

        public static void Run(Func<Task> action, TimeSpan period, System.Threading.CancellationToken cancellationToken)
        {
            const int threadSleepChunks = 5;
            TimeSpan chunk = period / threadSleepChunks;

            var t = new Thread(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; i < threadSleepChunks; i++)
                    {
                        Thread.Sleep(chunk);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                    }

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        action().Wait();
                    }
                }
            });

            t.Start();
        }

        public static void Run(Func<Task> action, TimeSpan period)
        {
            Run(action, period, System.Threading.CancellationToken.None);
        }
    }

    // FIXME how should this work if our values are null

    public static System.Runtime.CompilerServices.ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter(this System.Runtime.CompilerServices.ConfiguredTaskAwaitable? task)
    {
        return task.Value.GetAwaiter();
    }

    public static System.Runtime.CompilerServices.ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter GetAwaiter<T>(this System.Runtime.CompilerServices.ConfiguredTaskAwaitable<T>? task)
    {
        return task.Value.GetAwaiter();
    }

    public class SimpleNode<T>
    {
        public T Value { get; set; }
        public IList<SimpleNode<T>> Children { get; } = new List<SimpleNode<T>>();

        public SimpleNode<T> FindBreadthFirst(Func<SimpleNode<T>, bool> predicate)
        {
            var queue = new Queue<SimpleNode<T>>();
            queue.Enqueue(this);
            while (queue.Count > 0)
            {
                SimpleNode<T> node = queue.Dequeue();
                if (predicate(node))
                {
                    return node;
                }
                foreach (var child in node.Children)
                {
                    queue.Enqueue(child);
                }
            }

            return null;
        }

        public SimpleNode<T> FindDepthFirst(Func<SimpleNode<T>, bool> predicate)
        {
            if (predicate(this))
            {
                return this;
            }

            foreach (var child in Children)
            {
                SimpleNode<T> depthFirstResult = child.FindDepthFirst(predicate);
                if (depthFirstResult != null)
                {
                    return depthFirstResult;
                }
            }

            return null;
        }

        public void Add(T value)
        {
            Children.Add(new SimpleNode<T>() { Value = value });
        }
    }
}
