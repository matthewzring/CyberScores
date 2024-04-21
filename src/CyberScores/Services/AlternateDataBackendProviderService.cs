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

using CyberPatriot.Services;
using CyberPatriot.Services.ScoreRetrieval;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services;

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
