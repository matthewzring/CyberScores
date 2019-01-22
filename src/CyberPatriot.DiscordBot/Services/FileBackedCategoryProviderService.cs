using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CyberPatriot.Services;

namespace CyberPatriot.DiscordBot.Services
{
    public class FileBackedCategoryProviderService : IExternalCategoryProviderService
    {
        private IDictionary<TeamId, ServiceCategory?> _allServiceCategoryMap;

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _allServiceCategoryMap = new Dictionary<TeamId, ServiceCategory?>();
            string allServiceCategoryMapFile = provider.GetRequiredService<IConfiguration>().GetValue<string>("allServiceCategoryMapFile", null);
            if (!string.IsNullOrWhiteSpace(allServiceCategoryMapFile) && File.Exists(allServiceCategoryMapFile))
            {
                foreach (var line in await File.ReadAllLinesAsync(allServiceCategoryMapFile).ConfigureAwait(false))
                {
                    try
                    {
                        string[] components = line.Split(new[] { ':' }, 2);
                        if (CyberPatriot.Models.Serialization.ServiceCategoryExtensions.TryParseAliasName(components[1].Trim(), out ServiceCategory value))
                        {
                            _allServiceCategoryMap[TeamId.Parse(components[0].Trim())] = value;
                        }
                    }
                    catch
                    {
                        // oh well
                    }
                }
            }
        }

        public ServiceCategory? GetCategory(TeamId team) => _allServiceCategoryMap.TryGetValue(team, out ServiceCategory? category) ? category : null;
    }
}