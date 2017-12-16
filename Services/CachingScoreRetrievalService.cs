using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CyberPatriot.Models;
using System.Collections;

namespace CyberPatriot.DiscordBot.Services
{
    public class CachingScoreRetrievalService : IScoreRetrievalService
    {
        public IScoreRetrievalService Backend { get; set; }
        public TimeSpan MaxTeamLifespan { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan MaxCompleteScoreboardLifespan { get; set; } = TimeSpan.FromMinutes(3);
        public int MaxCachedTeamDetails { get; set; } = 20;

        public CachingScoreRetrievalService(IScoreRetrievalService backend)
        {
            Backend = backend;
        }

        protected class HitTrackingCachedObject<TCachee>
        {
            public TCachee Value;
            public int HitCount;

            public DateTimeOffset Timestamp
            {
                get
                {
                    return getTimestamp(Value);
                }
            }

            private Func<TCachee, DateTimeOffset> getTimestamp;

            public HitTrackingCachedObject(TCachee value, Func<TCachee, DateTimeOffset> timestampGet)
            {
                Value = value;
                getTimestamp = timestampGet;
            }

            public TimeSpan Age
            {
                get
                {
                    return DateTimeOffset.UtcNow - Timestamp;
                }
            }
        }

        #region Team Details

        protected ConcurrentDictionary<TeamId, HitTrackingCachedObject<ScoreboardDetails>> cachedTeamInformations = new ConcurrentDictionary<TeamId, HitTrackingCachedObject<ScoreboardDetails>>();
        protected readonly SemaphoreSlim teamCacheLock = new SemaphoreSlim(1);

        // does NOT enter the teamCacheLock, caller MUST
        protected void EnsureTeamCacheCapacity()
        {
            if (cachedTeamInformations.Count >= MaxCachedTeamDetails)
            {
                // start by purging all old items
                // ToArray to avoid enumeration + modification
                foreach (var oldTeamInfoId in cachedTeamInformations.Where(kvp => kvp.Value.Age >= MaxTeamLifespan).Select(kvp => kvp.Key).ToArray())
                {
                    cachedTeamInformations.Remove(oldTeamInfoId, out var _);
                }
            }
            if (cachedTeamInformations.Count > MaxCachedTeamDetails)
            {
                // we've purged old ones but it's not good enough
                // keep the most commonly accessed ones (hits per time)
                // untested algorithm, but this is a low volume cache anyway
                // clears cache to half capacity (not max) to avoid constantly calling this
                // that also attempts to avoid the common request of an item which is always new and getting purged from cache
                TeamId[] destroy = cachedTeamInformations.OrderBy(tInf => Math.Round((20.0 * tInf.Value.HitCount) / Math.Min(tInf.Value.Age.TotalSeconds, MaxTeamLifespan.TotalSeconds * 1.5))).ThenByDescending(tInf => tInf.Value.Age).Take(cachedTeamInformations.Count - (MaxCachedTeamDetails / 2)).Select(tInf => tInf.Key).ToArray();
                foreach (var oldTeamInfoId in destroy)
                {
                    cachedTeamInformations.Remove(oldTeamInfoId, out var _);
                }
            }
        }

        public async Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            HitTrackingCachedObject<ScoreboardDetails> cachedTeamData = null;
            Func<bool> cachedGoodEnough = () => cachedTeamInformations.TryGetValue(team, out cachedTeamData) && cachedTeamData.Age <= MaxTeamLifespan;

            // FIXME potential undefined/unwanetd behavior if reordering dictionary in EnsureTeamCacheCapacity
            // while hitting this branch (it reorders based on HitCount while we alter it)
            // I dont think this will ever do worse than return a just-removed cache item,
            // or slightly screw up removal sorting, but I'm not sure
            if (cachedGoodEnough())
            {
                // cached info good enough
                Interlocked.Increment(ref cachedTeamData.HitCount);
                return cachedTeamData.Value;
            }
            else
            {
                await teamCacheLock.WaitAsync();
                try
                {
                    // try our read again, but within the lock
                    // if it's succeeded, it means we entered lock in parallel with someone else
                    // but they fixed the problem first
                    if (cachedGoodEnough())
                    {
                        // cached info good enough
                        Interlocked.Increment(ref cachedTeamData.HitCount);
                        return cachedTeamData.Value;
                    }

                    // cached info either does not exist or is not good enough, and we have exclusive license to write to the cache
                    // make sure we have space
                    // note that this method will write to more than just 
                    EnsureTeamCacheCapacity();

                    // pull from backend
                    ScoreboardDetails teamInfo = await Backend.GetDetailsAsync(team);
                    // add to cache
                    cachedTeamData = new HitTrackingCachedObject<ScoreboardDetails>(teamInfo, tInf => tInf.SnapshotTimestamp);
                    Interlocked.Increment(ref cachedTeamData.HitCount);
                    cachedTeamInformations[team] = cachedTeamData;
                    // return the fresh object
                    // in this case we don't do any fancy wrapping so its ok
                    return teamInfo;
                }
                finally
                {
                    teamCacheLock.Release();
                }
            }

        }

        #endregion

        #region Scoreboard Summary
        protected SemaphoreSlim scoreboardCacheLock = new SemaphoreSlim(1);

        protected struct FilterInfo
        {
            public static readonly FilterInfo NoFilter = new FilterInfo(null, null);

            public FilterInfo(Division? divFilter, string tierFilter)
            {
                Division = divFilter;
                Tier = tierFilter;
            }

            public Division? Division;
            public string Tier;

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is FilterInfo other))
                {
                    return false;
                }

                return Division == other.Division && Tier == other.Tier;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 19;
                    hashCode = (hashCode * 401) ^ (Division.HasValue ? Division.Value.GetHashCode() : 0);
                    hashCode = (hashCode * 401) ^ (!string.IsNullOrEmpty(Tier) ? Tier.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public static bool operator ==(FilterInfo a, FilterInfo b)
            {
                // value type, no null check
                return a.Equals(b);
            }

            public static bool operator !=(FilterInfo a, FilterInfo b)
            {
                // value type, no null check
                return !a.Equals(b);
            }
        }

        protected ConcurrentDictionary<FilterInfo, CompleteScoreboardSummary> cachedScoreboards = new ConcurrentDictionary<FilterInfo, CompleteScoreboardSummary>();

        public async Task<CompleteScoreboardSummary> GetScoreboardAsync(Division? divisionFilter, string tierFilter)
        {
            await scoreboardCacheLock.WaitAsync();
            try
            {
                var filter = new FilterInfo(divisionFilter, tierFilter);
                if (!cachedScoreboards.TryGetValue(filter, out CompleteScoreboardSummary scoreboard) || DateTimeOffset.UtcNow - scoreboard.SnapshotTimestamp >= MaxCompleteScoreboardLifespan)
                {
                    // need to replace cached for this entry
                    // try querying the "master" cached scoreboard before querying the backend

                    if (filter != FilterInfo.NoFilter && cachedScoreboards.TryGetValue(FilterInfo.NoFilter, out CompleteScoreboardSummary masterScoreboard) && DateTimeOffset.UtcNow - scoreboard.SnapshotTimestamp < MaxCompleteScoreboardLifespan)
                    {
                        // we have a fresh complete scoreboard, just create the more specialized one
                        scoreboard = await masterScoreboard.Clone().WithFilter(filter.Division, filter.Tier).WithInternalListAsync();
                        cachedScoreboards[filter] = scoreboard;
                    }
                    else
                    {
                        // we need a new master scoreboard
                        masterScoreboard = await (await Backend.GetScoreboardAsync(null, null)).WithInternalListAsync();
                        cachedScoreboards[FilterInfo.NoFilter] = masterScoreboard;
                        if (filter == FilterInfo.NoFilter)
                        {
                            scoreboard = masterScoreboard;
                        }
                        else
                        {
                            scoreboard = await masterScoreboard.Clone().WithFilter(filter.Division, filter.Tier).WithInternalListAsync();
                            cachedScoreboards[filter] = scoreboard;
                        }
                    }
                }

                return scoreboard;
            }
            finally
            {
                scoreboardCacheLock.Release();
            }
        }
        #endregion
    }
}