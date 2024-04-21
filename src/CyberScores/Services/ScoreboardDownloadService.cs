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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CyberPatriot.Models;
using CyberPatriot.Services;
using CyberPatriot.Services.ScoreRetrieval;

namespace CyberPatriot.DiscordBot.Services;

public class ScoreboardDownloadService
{
    public IScoreRetrievalService ScoreService { get; protected set; }
    public LogService Logger { get; protected set; }
    protected IServiceProvider Provider { get; set; }

    /// <summary>
    /// The minimum difference in time between two team downloads to consider the older one eligible to be considered as cacheably identical to the newer one.
    /// </summary>
    /// <remarks>
    /// The CCS details pages only update every few minutes. If [this field] amount of time has not passed, 
    /// it is not likely enough that an old download and a new download are identical due to lack of upstream updates; it is too likely that it is merely due to CCS's update interval.
    /// </remarks>
    private static readonly TimeSpan MinimumCacheableInterval = TimeSpan.FromMinutes(15);

    public ScoreboardDownloadService(IScoreRetrievalService scoreRetriever, LogService logger)
    {
        ScoreService = scoreRetriever;
        Logger = logger;
    }

    public Task InitializeAsync(IServiceProvider provider)
    {
        Provider = provider;
        return Task.CompletedTask;
    }

    public StateWrapper.State GetState()
    {
        currentDownloadLock.Wait();
        try
        {
            if (currentDownload == null)
            {
                throw new InvalidOperationException("A scoreboard download is not currently in progress.");
            }

            return currentDownload.GetState();
        }
        finally
        {
            currentDownloadLock.Release();
        }
    }

    public class ReturnedStateInfoWrapper
    {
        public int TeamDetailCount { get; internal set; }
        public CompleteScoreboardSummary Summary { get; internal set; }
        public Task<ScoreboardDetails>[] DownloadTasks { get; internal set; }
    }

    public class StateWrapper
    {
        public StateWrapper()
        {
            state = new State(ListLock, DateTimeOffset.UtcNow);
        }

        public struct State
        {
            public readonly SemaphoreSlim ListLock;
            public readonly DateTimeOffset StartTime;
            internal State(SemaphoreSlim listLock, DateTimeOffset startTime)
            {
                ListLock = listLock;
                DetailsTaskList = null;
                OriginalTaskList = null;
                OriginalDownloadList = null;
                StartTime = startTime;
            }

            public List<Task<ScoreboardDetails>> DetailsTaskList;
            public int DetailsTaskListCount
            {
                get
                {
                    ListLock.Wait();
                    try
                    {
                        return DetailsTaskList.Count;
                    }
                    finally
                    {
                        ListLock.Release();
                    }
                }
            }

            public Task<ScoreboardDetails>[] OriginalTaskList;

            public TeamId[] OriginalDownloadList;
        }

        public readonly object StateLock = new object();
        public readonly SemaphoreSlim ListLock = new SemaphoreSlim(1);
        private State state;

        public List<Task<ScoreboardDetails>> DetailsTaskList
        {
            get => state.DetailsTaskList;
            set => state.DetailsTaskList = value;
        }

        public Task<ScoreboardDetails>[] OriginalTaskList
        {
            get => state.OriginalTaskList;
            set => state.OriginalTaskList = value;
        }

        public TeamId[] OriginalDownloadList
        {
            get => state.OriginalDownloadList;
            set => state.OriginalDownloadList = value;
        }

        // clone state (value type), so observers can have a consistent object
        // we expose StateLock so the writer can ensure consistency
        public State GetState()
        {
            lock (StateLock)
            {
                return state;
            }
        }
    }

    protected readonly SemaphoreSlim currentDownloadLock = new SemaphoreSlim(1);
    protected volatile StateWrapper currentDownload = null;

    /// <summary>
    /// Returns an open MemoryStream which, when read, will contain the GZipped export.
    /// </summary>
    public async Task<MemoryStream> DownloadFullScoreboardGzippedAsync(ReturnedStateInfoWrapper retState = null, IReadOnlyDictionary<TeamId, ScoreboardDetails> previousScoreStatus = null)
    {
        var rawStr = new MemoryStream();
        using (var compressStr = new GZipStream(rawStr, CompressionMode.Compress, true))
        {
            await DownloadFullScoreboardStringAsync(new StreamWriter(compressStr), retState, previousScoreStatus, true).ConfigureAwait(false);
        }
        rawStr.Position = 0;
        return rawStr;
    }

    private Microsoft.Extensions.Configuration.IConfigurationSection GetHttpProviderConfig(HttpScoreboardScoreRetrievalService provider)
    {
        var field = typeof(HttpScoreboardScoreRetrievalService).GetField("_httpConfiguration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Microsoft.Extensions.Configuration.IConfigurationSection)field.GetValue(provider);
    }

    /// <summary>
    /// If a partial previous archive is available, it is used to reduce the number of detail queries performed.
    /// </summary>
    protected virtual async Task DownloadFullScoreboardStringAsync(TextWriter target, ReturnedStateInfoWrapper retState = null, IReadOnlyDictionary<TeamId, ScoreboardDetails> previousScoreStatus = null, bool writeState = true)
    {
        if (writeState)
        {
            await currentDownloadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (currentDownload != null)
                {
                    throw new InvalidOperationException("A scoreboard export is already running. Please wait for the current download to complete, or cancel it.");
                }
                currentDownload = new StateWrapper();
            }
            finally
            {
                currentDownloadLock.Release();
            }
        }

        if (retState == null)
        {
            retState = new ReturnedStateInfoWrapper();
        }

        try
        {
            // we should be the only writer task, but others might be reading stuff (like count)
            var teamDetails = new ConcurrentDictionary<TeamId, ScoreboardDetails>();

            var scoreSummary = await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);

            var teamDetailRetrieveTasksBuilder = scoreSummary.TeamList.Select(team => team.TeamId);

            if (previousScoreStatus != null)
            {
                // be lazy, don't redownload if we don't have to

                // populate the details dictionary with existing teams that haven't been updated in a while
                foreach (var teamDetail in previousScoreStatus.Values)
                {
                    if (teamDetail == null)
                    {
                        continue;
                    }

                    if (scoreSummary.SnapshotTimestamp - teamDetail.SnapshotTimestamp <= TimeSpan.Zero)
                    {
                        // the provided snapshot for this team is MORE recent than the one we just downloaded, OR is identical
                        // use it unconditionally
                        teamDetails[teamDetail.TeamId] = teamDetail;
                        continue;
                    }
                    else if (scoreSummary.SnapshotTimestamp - teamDetail.SnapshotTimestamp < MinimumCacheableInterval)
                    {
                        // if we're below minCacheableInterval, then we don't trust the CCS scoreboard to update in time
                        // we'll use a new download to be sure
                        continue;
                    }

                    // known: our scoreboard is more recent than the provided data by an appreciable amount of time
                    // if the team play time has not updated AND the score is the same, then we DON'T have to redownload
                    // note: just because same score doesn't indicate no change, consider a team that got +5 vuln points but a penalty in the same interval

                    // find the summary info from the scoreboard
                    ScoreboardSummaryEntry summary = scoreSummary.TeamList.SingleOrDefault(sumInf => sumInf.TeamId == teamDetail.TeamId);
                    if (summary == null)
                    {
                        // our scoreboard doesn't have this team
                        // go ahead and add it from the archive
                        teamDetails[teamDetail.TeamId] = teamDetail;
                        continue;
                    }

                    if (summary.PlayTime == teamDetail.Summary.PlayTime && summary.TotalScore == teamDetail.Summary.TotalScore)
                    {
                        // this team has not changed over this interval - use the archived details
                        teamDetails[teamDetail.TeamId] = teamDetail;
                    }
                }

                // the where will be evaluated in the bag constructor, so we don't have to worry about our download affecting it
                teamDetailRetrieveTasksBuilder = teamDetailRetrieveTasksBuilder.Where(tId => !teamDetails.ContainsKey(tId));
            }

            // for downloading from CCS, we can make some optimizations
            IScoreRetrievalService scoreRetriever = ScoreService;
            HttpScoreboardScoreRetrievalService underlyingHttp = scoreRetriever.GetFirstFromChain(t => t is HttpScoreboardScoreRetrievalService) as HttpScoreboardScoreRetrievalService;
            if (underlyingHttp != null)
            {
                var newHttp = new HttpScoreboardScoreRetrievalService(underlyingHttp.Hostname);
                // optimization: our service only makes low-priority requests
                var newRateLimit = underlyingHttp.RateLimiter is PriorityTimerRateLimitProvider ? (underlyingHttp.RateLimiter as PriorityTimerRateLimitProvider).LowPriorityRateLimiter : underlyingHttp.RateLimiter;
                // TODO cases when this might differ from the original service set outside our intent?
                await newHttp.InitializeAsync(Provider.Overlay<IRateLimitProvider>(newRateLimit), GetHttpProviderConfig(underlyingHttp)).ConfigureAwait(false);
                scoreRetriever = newHttp;
            }

            teamDetailRetrieveTasksBuilder = teamDetailRetrieveTasksBuilder.ToIList();

            // use the optimized score retriever
            List<Task<ScoreboardDetails>> teamDetailRetrieveTasks =
                teamDetailRetrieveTasksBuilder.Select(tId => scoreRetriever.GetDetailsAsync(tId)).ToList();
            Task<ScoreboardDetails>[] originalTaskList = teamDetailRetrieveTasks.ToArray();

            lock (currentDownload.StateLock)
            {
                currentDownload.DetailsTaskList = teamDetailRetrieveTasks;
                currentDownload.OriginalTaskList = originalTaskList;
                currentDownload.OriginalDownloadList = teamDetailRetrieveTasksBuilder.ToArray();
            }

            retState.DownloadTasks = originalTaskList;

            // this is necessary because other threads might add to the list
            // we can't just use a bag because we need to be able to remove, which needs a lock
            async Task<int> GetTeamDetailRetrieveTaskListCount()
            {
                await currentDownload.ListLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    return teamDetailRetrieveTasks.Count;
                }
                finally
                {
                    currentDownload.ListLock.Release();
                }
            }

            do
            {
                try
                {
                    await Task.WhenAll(teamDetailRetrieveTasks).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await Logger.LogApplicationMessageAsync(Discord.LogSeverity.Error, "Error while retrieving some teams in full score export, excluding them silently!", e).ConfigureAwait(false);
                    // TODO handle this in a more logical place
                    // assume a rate limit issue, treat this task as lost and cool down
                    // here's a hack if I ever saw one
                    var rateLimiter = (ScoreService.GetFirstFromChain<IScoreRetrievalService>(s => s is HttpScoreboardScoreRetrievalService) as HttpScoreboardScoreRetrievalService)?.RateLimiter;
                    var delayTask = Task.Delay(15000);
                    if (rateLimiter != null)
                    {
                        rateLimiter.AddPrerequisite(delayTask);
                    }

                    await delayTask.ConfigureAwait(false);
                }

                // completed and faulted
                var completedTasks = teamDetailRetrieveTasks.Where(t => t.IsCompleted).ToArray();

                // add all completed results to the dictionary
                foreach (var retrieveTask in completedTasks.Where(t => t.IsCompletedSuccessfully))
                {
                    // successful completion
                    ScoreboardDetails details = retrieveTask.Result;
                    teamDetails[details.TeamId] = details;
                }

                // use a lock so observer threads reading count are safe
                // this remove shouldn't take too long (~5k teams at most), but we use a semaphore anyway
                await currentDownload.ListLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // remove all completed and faulted tasks
                    foreach (var task in completedTasks)
                    {
                        teamDetailRetrieveTasks.Remove(task);
                    }
                }
                finally
                {
                    currentDownload.ListLock.Release();
                }

                // we need the await here because other threads might want to enqueue download tasks
            } while (await GetTeamDetailRetrieveTaskListCount().ConfigureAwait(false) > 0);

            // write the result
            // the serialization might take time too
            // this will be a big string
            await JsonScoreRetrievalService.SerializeAsync(target, scoreSummary, teamDetails, ScoreService.Round).ConfigureAwait(false);

            retState.TeamDetailCount = teamDetails.Count;
            retState.Summary = scoreSummary;
        }
        finally
        {
            if (writeState)
            {
                await currentDownloadLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    currentDownload = null;
                }
                finally
                {
                    currentDownloadLock.Release();
                }
            }
        }
    }
}
