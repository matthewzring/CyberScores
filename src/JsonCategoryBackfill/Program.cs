using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CyberPatriot.Models;
using Newtonsoft.Json;

namespace JsonCategoryBackfill
{
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
            var teamCategoryDictionary = File.ReadAllLines(categoryPath).Select(l => l.Trim().Split(new[] { ':' }, 2)).Where(l => TeamId.TryParse(l[0], out TeamId _)).ToDictionary(l => TeamId.Parse(l[0]), l => l[1]);
            // fix summary data
            foreach (var teamSummary in input.summary.TeamList)
            {
                if (teamSummary.Category == null && teamCategoryDictionary.TryGetValue(teamSummary.TeamId, out string newCategory) && newCategory != null)
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
}
