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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;
using Newtonsoft.Json;

namespace JsonCategoryBackfill;

class Program
{
    static void Main(string[] args)
    {
        string jsonPath;
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Enter path to JSON file:");
            jsonPath = Console.ReadLine();
        }
        else
        {
            jsonPath = args[0];
        }
        Console.Error.WriteLine($"Got {jsonPath} as JSON path");

        string categoryPath;

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Enter path to category map file:");
            categoryPath = Console.ReadLine();
        }
        else
        {
            categoryPath = args[1];
        }
        Console.Error.WriteLine($"Got {categoryPath} as category mapfile path");

        var input = JsonConvert.DeserializeObject<Output>(File.ReadAllText(jsonPath));
        var teamCategoryDictionary = File.ReadAllLines(categoryPath).Select(l => l.Trim().Split(new[] { ':' }, 2)).Where(l => TeamId.TryParse(l[0], out TeamId _)).ToDictionary(l => TeamId.Parse(l[0]), l => ServiceCategoryExtensions.ParseCanonicalName(l[1]));
        // fix summary data
        foreach (var teamSummary in input.summary.TeamList)
        {
            if (teamSummary.Category == null && teamCategoryDictionary.TryGetValue(teamSummary.TeamId, out ServiceCategory newCategory))
            {
                teamSummary.Category = newCategory;
            }
        }
        // fix details
        foreach (var knownCategory in teamCategoryDictionary)
        {
            if (input.teams.TryGetValue(knownCategory.Key, out var teamDetails) && teamDetails.Summary.Category == null)
            {
                teamDetails.Summary.Category = knownCategory.Value;
            }
        }
        Console.Write(JsonConvert.SerializeObject(input));
    }
}

public class Output
{
    public int round { get; set; }
    public CompleteScoreboardSummary summary { get; set; }
    public Dictionary<TeamId, ScoreboardDetails> teams { get; set; }
}
