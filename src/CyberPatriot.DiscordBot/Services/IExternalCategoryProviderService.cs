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
        string GetCategory(TeamId team);
    }

    public class FileBackedCategoryProviderService : IExternalCategoryProviderService
    {
        private IDictionary<TeamId, string> _allServiceCategoryMap;

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _allServiceCategoryMap = new Dictionary<TeamId, string>();
            string allServiceCategoryMapFile = provider.GetRequiredService<IConfiguration>().GetValue<string>("allServiceCategoryMapFile", null);
            if (!string.IsNullOrWhiteSpace(allServiceCategoryMapFile) && File.Exists(allServiceCategoryMapFile))
            {
                foreach (var line in await File.ReadAllLinesAsync(allServiceCategoryMapFile).ConfigureAwait(false))
                {
                    try
                    {
                        string[] components = line.Split(new[] { ':' }, 2);
                        _allServiceCategoryMap[TeamId.Parse(components[0].Trim())] = components[1].Trim();
                    }
                    catch
                    {
                        // oh well
                    }
                }
            }
        }

        public string GetCategory(TeamId team) => _allServiceCategoryMap.TryGetValue(team, out string category) ? category : null;
    }
}