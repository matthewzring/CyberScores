using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CyberPatriot.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    public class FallbackScoreRetrievalService : IScoreRetrievalService, IComposingService<IScoreRetrievalService>
    {
        private List<Func<IServiceProvider, Task<IScoreRetrievalService>>> _backendOptions;
        private Action<CachingScoreRetrievalService> _cacheConfigurator;
        private int _selectedBackendIndex;
        private IServiceProvider _provider;
        private LogService _log;
        private DateTimeOffset _lastBackendRefresh;
        private readonly SemaphoreSlim _backendResolutionLock = new SemaphoreSlim(1);

        public TimeSpan BackendLifespan { get; set; } = TimeSpan.FromMinutes(10);
        protected CachingScoreRetrievalService Backend { get; private set; }
        IScoreRetrievalService IComposingService<IScoreRetrievalService>.Backend => Backend;

        // Deliberately, these change when the backend changes
        public CompetitionRound Round => Backend.Round;
        public Models.IScoreRetrieverMetadata Metadata => Backend?.Metadata;

        /// <summary>
        /// Initializes the FallbackScoreRetrievalService with an array of possible backends.
        /// Null backends in the parameter array are ignored.
        /// ONLY the selected backend will have its InitializeAsync method called, so each parameter must be minimally initialized.
        /// The selected backend will be wrapped in a CachingScoreRetrievalService.
        /// </summary>
        public FallbackScoreRetrievalService(IServiceProvider provider, Action<CachingScoreRetrievalService> cacheConfigurator, params IScoreRetrievalService[] backends) : this(provider, cacheConfigurator, backends.Where(b => b != null).Select<IScoreRetrievalService, Func<IServiceProvider, Task<IScoreRetrievalService>>>(b => _ => Task.FromResult(b)).ToArray())
        {

        }

        public FallbackScoreRetrievalService(IServiceProvider provider, params IScoreRetrievalService[] backends) : this(provider, csrs => { }, backends)
        {
        }

        public FallbackScoreRetrievalService(IServiceProvider provider, Action<CachingScoreRetrievalService> cacheConfigurator, params Func<IServiceProvider, Task<IScoreRetrievalService>>[] backends) : this(provider, cacheConfigurator, (IEnumerable<Func<IServiceProvider, Task<IScoreRetrievalService>>>)backends)
        {

        }

        public FallbackScoreRetrievalService(IServiceProvider provider, Action<CachingScoreRetrievalService> cacheConfigurator, IEnumerable<Func<IServiceProvider, Task<IScoreRetrievalService>>> backends)
        {
            _provider = provider;
            _backendOptions = backends.Where(t => t != null).ToList();
            _cacheConfigurator = cacheConfigurator;
            _log = provider.GetRequiredService<LogService>();
        }

        public FallbackScoreRetrievalService(IServiceProvider provider, params Func<IServiceProvider, Task<IScoreRetrievalService>>[] backends) : this(provider, crsr => { }, backends)
        {
        }

        protected bool IsSummaryValid(CompleteScoreboardSummary returnedSummary)
        {
            if (returnedSummary?.TeamList == null)
            {
                return false;
            }

            if (returnedSummary.Filter == ScoreboardFilterInfo.NoFilter)
            {
                // NoFilter responses should have at least one team
                if (returnedSummary.TeamList.Count < 1)
                {
                    return false;
                }

                if (returnedSummary.TeamList.First().TeamId == default(TeamId))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task ResolveBackendAsync(IServiceProvider provider = null, int upperSearchBound = -1)
        {
            await _backendResolutionLock.WaitAsync().ConfigureAwait(false);

            var errors = new List<Exception>();

            try
            {
                provider = provider ?? _provider;
                IScoreRetrievalService backend = null;
                int selInd = -1;
                for (int i = 0; i < (upperSearchBound == -1 ? _backendOptions.Count : upperSearchBound); i++)
                {
                    var candidateBackendTaskFactory = _backendOptions[i];
                    try
                    {
                        var candidateBackend = await candidateBackendTaskFactory(provider).ConfigureAwait(false);
                        if (candidateBackend == null)
                        {
                            errors.Add(new NullReferenceException("Candidate backend task factory returned null."));
                            continue;
                        }
                        // try getting a summary to "test" the backend
                        CompleteScoreboardSummary returnedSummary = await candidateBackend.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false);
                        if (!IsSummaryValid(returnedSummary))
                        {
                            // invalid summary
                            errors.Add(new Exception("Invalid summary."));
                            continue;
                        }
                        else
                        {
                            // this backend is valid
                            selInd = i;
                            backend = candidateBackend;

                            // first valid backend wins
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        // invalid summary, or failed constructor
                        errors.Add(e);
                        continue;
                    }
                }

                if (backend == null)
                {
                    if (upperSearchBound == -1)
                    {
                        // we searched all possible backends
                        await _log.LogApplicationMessageAsync(Discord.LogSeverity.Error, "Could not find an IScoreRetrievalService for fallback, continuing with invalid service.", source: nameof(FallbackScoreRetrievalService)).ConfigureAwait(false);
                        throw new AggregateException("No valid IScoreRetrievalService found.", errors);
                    }
                    else
                    {
                        // we tried and failed to replace the backend with a higher-priority one, so now check lower priority ones
                        // use Backend.Backend to get the cache's backend
                        if (IsSummaryValid(await Backend.Backend.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter).ConfigureAwait(false)))
                        {
                            // update the refresh time to now, keep the existing backend
                            _lastBackendRefresh = DateTimeOffset.UtcNow;
                        }
                        else
                        {
                            // current backend has failed: try again but allow deferring to lower-priority things
                            await ResolveBackendAsync(provider, -1).ConfigureAwait(false);
                        }
                        return;
                    }
                }

                _selectedBackendIndex = selInd;

                // wrap it in a cache
                var csrs = new CachingScoreRetrievalService(backend);
                _cacheConfigurator(csrs);
                Backend = csrs;
                _lastBackendRefresh = DateTimeOffset.UtcNow;

                // initialize the backend properly
                // this initializes the cache, which passes through initialization to the real backend
                await Backend.InitializeAsync(provider).ConfigureAwait(false);
            }
            finally
            {
                _backendResolutionLock.Release();
            }
        }

        public Task InitializeAsync(IServiceProvider provider) => ResolveBackendAsync(provider);

        public Task RefreshBackendIfNeeded()
        {
            // FIXME we might refresh twice in a row if this method is called
            // because two threads could enter ResolveBackendAsync simultaneously, then hit the lock
            // then we'd refresh once, then we'd refresh once more
            // possibly we want to enter the lock here too? in that case the lock needs recursion support        
            if (_lastBackendRefresh + BackendLifespan < DateTimeOffset.UtcNow)
            {
                return ResolveBackendAsync(upperSearchBound: _selectedBackendIndex);
            }

            return Task.CompletedTask;
        }

        public async Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            await RefreshBackendIfNeeded().ConfigureAwait(false);
            var scoreboardDetails = await Backend.GetScoreboardAsync(filter).ConfigureAwait(false);
            if (!IsSummaryValid(scoreboardDetails))
            {
                // well this is awkward
                await _log.LogApplicationMessageAsync(Discord.LogSeverity.Warning, "Returning known bad scoreboard summary!", source: nameof(FallbackScoreRetrievalService)).ConfigureAwait(false);
                // don't block the caller
                // exceptions will NOT be caught (!) but there's a log call in there which should be Good Enough(TM) in problematic cases
#pragma warning disable 4014
                ResolveBackendAsync(upperSearchBound: _selectedBackendIndex);
#pragma warning restore 4014
            }
            return scoreboardDetails;
        }
        public async Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            await RefreshBackendIfNeeded().ConfigureAwait(false);
            return await Backend.GetDetailsAsync(team).ConfigureAwait(false);
        }

    }
}