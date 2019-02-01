#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

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