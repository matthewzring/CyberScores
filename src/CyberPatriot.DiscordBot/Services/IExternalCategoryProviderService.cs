using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    public interface IExternalCategoryProviderService
    {
        Task InitializeAsync(IServiceProvider provider);

        /// <summary>
        /// Attempts to obtain the category for the given team. If a category is not available, returns null.
        /// </summary>
        ServiceCategory? GetCategory(TeamId team);
    }

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