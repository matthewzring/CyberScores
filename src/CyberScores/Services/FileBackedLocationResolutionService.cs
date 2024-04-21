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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services;

public class FileBackedLocationResolutionService : ILocationResolutionService
{
    protected Dictionary<string, string> _codesToNames = new Dictionary<string, string>();
    protected Dictionary<string, string> _namesToCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    protected Dictionary<string, Uri> _codesToFlags = new Dictionary<string, Uri>();

    public bool IsValidLocation(string locationCode) => locationCode != null && _codesToNames.ContainsKey(locationCode);

    public string GetAbbreviation(string locationName)
    {
        if (locationName == null)
        {
            throw new ArgumentNullException(nameof(locationName));
        }

        if (!_namesToCodes.TryGetValue(locationName, out string code))
        {
            throw new ArgumentException("The given location name is invalid: it does not exist. Make sure it exactly matches a canonical location name.");
        }

        return code;
    }

    public Uri GetFlagUri(string locationCode)
    {
        if (locationCode == null)
        {
            throw new ArgumentNullException(nameof(locationCode));
        }

        if (_codesToFlags.TryGetValue(locationCode, out Uri flagUri))
        {
            return flagUri;
        }

        if (!_codesToNames.ContainsKey(locationCode))
        {
            throw new ArgumentException("The given location code is invalid: it does not exist.");
        }

        return null;
    }

    public string GetFullName(string locationCode)
    {
        if (locationCode == null)
        {
            throw new ArgumentNullException(nameof(locationCode));
        }

        if (!_codesToNames.TryGetValue(locationCode, out string fullName))
        {
            throw new ArgumentException("The given location code is invalid: it does not exist.");
        }

        return fullName;
    }

    public async Task InitializeAsync(IServiceProvider provider)
    {
        var conf = provider.GetRequiredService<IConfiguration>();
        _codesToNames.Clear();
        _namesToCodes.Clear();
        _codesToFlags.Clear();
        string path = conf.GetValue<string>("locationCodeMapFile", null);
        if (path == null)
        {
            return;
        }
        string[] lines = await System.IO.File.ReadAllLinesAsync(path).ConfigureAwait(false);
        foreach (var line in lines)
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            string[] parts = line.Split(new char[] { ':' }, 3);
            _codesToNames.Add(parts[0], parts[1]);
            _namesToCodes.Add(parts[1].Trim(), parts[0]);
            if (parts.Length > 2 && Uri.TryCreate(parts[2], UriKind.Absolute, out Uri flagUri))
            {
                _codesToFlags.Add(parts[0], flagUri);
            }
        }
    }
}
