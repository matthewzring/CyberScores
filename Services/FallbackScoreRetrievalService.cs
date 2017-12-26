using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    public class FallbackScoreRetrievalService : IScoreRetrievalService
    {
        private List<Func<IServiceProvider, Task<IScoreRetrievalService>>> _backendOptions;
        private Action<CachingScoreRetrievalService> _cacheConfigurator;

        protected IScoreRetrievalService Backend { get; private set; }

        /// <summary>
        /// Initializes the FallbackScoreRetrievalService with an array of possible backends.
        /// Null backends in the parameter array are ignored.
        /// ONLY the selected backend will have its InitializeAsync method called, so each parameter must be minimally initialized.
        /// The selected backend will be wrapped in a CachingScoreRetrievalService.
        /// </summary>
        public FallbackScoreRetrievalService(Action<CachingScoreRetrievalService> cacheConfigurator, params IScoreRetrievalService[] backends) : this(cacheConfigurator, backends.Where(b => b != null).Select<IScoreRetrievalService, Func<IServiceProvider, Task<IScoreRetrievalService>>>(b => _ => Task.FromResult(b)).ToArray())
        {

        }

        public FallbackScoreRetrievalService(params IScoreRetrievalService[] backends) : this(csrs => { }, backends)
        {
        }

        public FallbackScoreRetrievalService(Action<CachingScoreRetrievalService> cacheConfigurator, params Func<IServiceProvider, Task<IScoreRetrievalService>>[] backends)
        {
            _backendOptions = backends.Where(t => t != null).ToList();
            _cacheConfigurator = cacheConfigurator;
        }

        public FallbackScoreRetrievalService(params Func<IServiceProvider, Task<IScoreRetrievalService>>[] backends) : this(crsr => { }, backends)
        {
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            foreach (var candidateBackendTaskFactory in _backendOptions)
            {
                var candidateBackend = await candidateBackendTaskFactory(provider);
                if (candidateBackend == null)
                {
                    continue;
                }
                try
                {
                    // try getting a summary to "test" the backend
                    CompleteScoreboardSummary returnedSummary = await candidateBackend.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
                    if (returnedSummary == null || returnedSummary.TeamList == null || returnedSummary.TeamList.Count == 0 || returnedSummary.TeamList.First().TeamId.SeasonId == 0)
                    {
                        // invalid summary
                        continue;
                    }
                    else
                    {
                        // this backend is valid
                        Backend = candidateBackend;

                        // first valid backend wins
                        break;
                    }
                }
                catch
                {
                    // invalid summary
                    continue;
                }
            }

            if (Backend == null)
            {
                throw new InvalidOperationException("No valid IScoreRetrievalService found.");
            }

            // wrap it in a cache
            var csrs = new CachingScoreRetrievalService(Backend);
            _cacheConfigurator(csrs);
            Backend = csrs;

            // initialize the backend properly
            // this initializes the cache, which passes through initialization to the real backend
            await Backend.InitializeAsync(provider);

            // get rid of temporary initialization variables
            _backendOptions = null;
            _cacheConfigurator = null;
        }

        public Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter) => Backend.GetScoreboardAsync(filter);


        public Task<ScoreboardDetails> GetDetailsAsync(TeamId team) => Backend.GetDetailsAsync(team);

    }
}