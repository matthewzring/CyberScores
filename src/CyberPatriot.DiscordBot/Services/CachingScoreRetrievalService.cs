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
    public class CachingScoreRetrievalService : IScoreRetrievalService, IComposingService<IScoreRetrievalService>, IDisposable
    {
        public IScoreRetrievalService Backend { get; set; }
        // Rationale: individual teams queried less often, but more teams are queried
        // Overall scoreboard is a global resource so should be as up-to-date as reasonable
        public TimeSpan MaxTeamLifespan { get; set; } = TimeSpan.FromSeconds(45);
        public TimeSpan MaxCompleteScoreboardLifespan { get; set; } = TimeSpan.FromSeconds(20);
        public int MaxCachedTeamDetails { get; set; } = 20;

        public bool IsDynamic => Backend.IsDynamic;
        public string StaticSummaryLine => Backend.StaticSummaryLine;
        public CompetitionRound Round => Backend.Round;
        public ScoreFormattingOptions FormattingOptions => Backend.FormattingOptions;


        protected Timer PurgeTimer { get; private set; }


        public CachingScoreRetrievalService(IScoreRetrievalService backend)
        {
            Backend = backend;
            PurgeTimer = new Timer(PurgeCacheTimerTick, null, MaxTeamLifespan * 5, MaxTeamLifespan * 5);
        }

        public Task InitializeAsync(IServiceProvider provider) => Backend.InitializeAsync(provider);

        protected class CachedObject<TCachee>
        {
            public TCachee Value { get; }

            public DateTimeOffset Timestamp
            {
                get; protected set;
            }

            public CachedObject(TCachee value) : this(value, DateTimeOffset.UtcNow) { }

            public CachedObject(TCachee value, DateTimeOffset timestamp)
            {
                Value = value;
                Timestamp = timestamp;
            }

            public TimeSpan Age
            {
                get
                {
                    return DateTimeOffset.UtcNow - Timestamp;
                }
            }
        }

        protected class HitTrackingCachedObject<TCachee> : CachedObject<TCachee>
        {
            public volatile int HitCount;

            public HitTrackingCachedObject(TCachee value) : base(value) { }

            public HitTrackingCachedObject(TCachee value, DateTimeOffset timestamp) : base(value, timestamp) { }
        }

        #region Team Details

        protected void PurgeCacheTimerTick(object state)
        {
            // 50ms to wait: if we're in the middle of something which is taking a while another thread is busy with this collection
            // then we shouldn't bother with this cleanup, that thread probably did
            if (teamCacheLock.Wait(50))
            {
                try
                {
                    EnsureTeamCacheCapacity(overrideCountCheck: true);
                }
                finally
                {
                    teamCacheLock.Release();
                }
            }
        }

        protected ConcurrentDictionary<TeamId, HitTrackingCachedObject<ScoreboardDetails>> cachedTeamInformations = new ConcurrentDictionary<TeamId, HitTrackingCachedObject<ScoreboardDetails>>();
        protected readonly SemaphoreSlim teamCacheLock = new SemaphoreSlim(1);

        // does NOT enter the teamCacheLock, caller MUST
        protected void EnsureTeamCacheCapacity(bool overrideCountCheck = false)
        {
            if (overrideCountCheck || cachedTeamInformations.Count >= MaxCachedTeamDetails)
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

            // FIXME potential undefined/unwanted behavior if reordering dictionary in EnsureTeamCacheCapacity
            // while hitting this branch (it reorders based on HitCount while we alter it, and we don't enter the lock)
            // I dont think this will ever do worse than return a just-removed cache item,
            // or slightly screw up removal sorting (hitcount), but I'm not sure
            // Intended optimization is to avoid hitting the lock if we've got a good enough item cache
            if (cachedGoodEnough())
            {
                // cached info good enough
                Interlocked.Increment(ref cachedTeamData.HitCount);
                return cachedTeamData.Value;
            }
            else
            {
                await teamCacheLock.WaitAsync().ConfigureAwait(false);
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
                    ScoreboardDetails teamInfo = await Backend.GetDetailsAsync(team).ConfigureAwait(false);
                    // add to cache
                    cachedTeamData = new HitTrackingCachedObject<ScoreboardDetails>(teamInfo);
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

        protected ConcurrentDictionary<ScoreboardFilterInfo, CachedObject<CompleteScoreboardSummary>> cachedScoreboards = new ConcurrentDictionary<ScoreboardFilterInfo, CachedObject<CompleteScoreboardSummary>>();

        public async Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            // TODO do we need to enter this lock every call? or can we make a similar tweak to the get team call
            // where we enter the lock and check again within the lock?
            await scoreboardCacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!cachedScoreboards.TryGetValue(filter, out CachedObject<CompleteScoreboardSummary> scoreboard) || scoreboard.Age >= MaxCompleteScoreboardLifespan)
                {
                    // need to replace cached for this entry
                    // try querying the "master" cached scoreboard before querying the backend

                    if (filter != ScoreboardFilterInfo.NoFilter && cachedScoreboards.TryGetValue(ScoreboardFilterInfo.NoFilter, out CachedObject<CompleteScoreboardSummary> masterScoreboard) && masterScoreboard.Age < MaxCompleteScoreboardLifespan)
                    {
                        // we have a fresh complete scoreboard, just create the more specialized one
                        scoreboard = new CachedObject<CompleteScoreboardSummary>(masterScoreboard.Value.Clone().WithFilter(filter), masterScoreboard.Timestamp);
                        cachedScoreboards[filter] = scoreboard;
                    }
                    else
                    {
                        // we need a new master scoreboard
                        masterScoreboard = new CachedObject<CompleteScoreboardSummary>(await Backend.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false));
                        cachedScoreboards[ScoreboardFilterInfo.NoFilter] = masterScoreboard;
                        if (filter == ScoreboardFilterInfo.NoFilter)
                        {
                            scoreboard = masterScoreboard;
                        }
                        else
                        {
                            scoreboard = new CachedObject<CompleteScoreboardSummary>(masterScoreboard.Value.Clone().WithFilter(filter), masterScoreboard.Timestamp);
                            cachedScoreboards[filter] = scoreboard;
                        }
                    }
                }

                // don't want client alterations to affect cached copy
                return scoreboard.Value.Clone();
            }
            finally
            {
                scoreboardCacheLock.Release();
            }
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (Backend is IDisposable d)
                    {
                        d.Dispose();
                    }
                    PurgeTimer?.Dispose();
                    scoreboardCacheLock?.Dispose();
                    teamCacheLock?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~CachingScoreRetrievalService() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}