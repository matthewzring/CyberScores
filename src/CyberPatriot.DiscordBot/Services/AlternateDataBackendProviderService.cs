using CyberPatriot.Services;
using CyberPatriot.Services.ScoreRetrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public class AlternateDataBackendProviderService
    {
        private struct ScoreBackendInitializerWrapper
        {
            public string Name;
            public Func<IConfigurationSection, Func<IServiceProvider, Task<IScoreRetrievalService>>> InitializationTask;
        }

        public static readonly IReadOnlyDictionary<string, Func<IConfigurationSection, Func<IServiceProvider, Task<IScoreRetrievalService>>>> ScoreBackendInitializerProvidersByName =
            new ScoreBackendInitializerWrapper[] {
                new ScoreBackendInitializerWrapper { Name = "http", InitializationTask = conf => async innerProv =>
                    {
                        var serv = new HttpScoreboardScoreRetrievalService();
                        await serv.InitializeAsync(innerProv, conf).ConfigureAwait(false);
                        return serv;
                    }
                },
                new ScoreBackendInitializerWrapper { Name = "json", InitializationTask = conf => async innerProv =>
                    {
                        var serv = new JsonScoreRetrievalService();
                        await serv.InitializeAsync(innerProv, conf).ConfigureAwait(false);
                        return serv;
                    }
                },
                new ScoreBackendInitializerWrapper { Name = "csv", InitializationTask = conf => async innerProv =>
                    {
                        var serv = new SpreadsheetScoreRetrievalService();
                        await serv.InitializeAsync(innerProv, conf).ConfigureAwait(false);
                        return serv;
                    }
                }
            }.ToDictionary(x => x.Name, x => x.InitializationTask);

        private IDictionary<string, IScoreRetrievalService> _backendsByName = new Dictionary<string, IScoreRetrievalService>(StringComparer.OrdinalIgnoreCase);

        public async Task InitializeAsync(IServiceProvider services)
        {
            _backendsByName.Clear();
            var config = services.GetRequiredService<IConfiguration>();
            foreach (IConfigurationSection serviceConfig in config.GetSection("alternateBackends").GetChildren())
            {
                try
                {
                    var scoreProvider = await ScoreBackendInitializerProvidersByName[serviceConfig["type"]](serviceConfig)(services).ConfigureAwait(false);
                    _backendsByName.Add(serviceConfig.Key, scoreProvider);
                    foreach (string alias in serviceConfig.GetSection("aliases").GetChildren().Select(x => x.Value))
                    {
                        _backendsByName.Add(alias, scoreProvider);
                    }
                }
                catch
                {
                    // TODO log
                    // ignore, try next one
                }
            }
        }

        public bool TryGetAlternateDataBackend(string identifier, out IScoreRetrievalService backend) => _backendsByName.TryGetValue(identifier, out backend);

        public IEnumerable<IGrouping<IScoreRetrievalService, string>> GetBackendNames() => _backendsByName.GroupBy(x => x.Value, x => x.Key);
    }
}
