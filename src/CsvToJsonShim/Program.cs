using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;
using Newtonsoft.Json;

namespace CsvToJsonShim
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Enter path to CSV file:");
            string path = Console.ReadLine();
            Console.WriteLine("Enter timestamp:");
            string timestamp = Console.ReadLine();
            Console.WriteLine("Enter path to all service category map file (or empty string):");
            string servicePath = Console.ReadLine();
            Console.WriteLine("Enter round number:");
            int roundNumber = int.Parse(Console.ReadLine());
            Console.WriteLine("Enter origin URI:");
            string originUri = Console.ReadLine();

            var categories = new Dictionary<TeamId, ServiceCategory>();

            if (servicePath != "")
            {
                categories = File.ReadAllLines(servicePath).Select(x => x.Split(':')).Where(x => TeamId.TryParse(x[0], out TeamId _)).ToDictionary(x => TeamId.Parse(x[0]), x => ServiceCategoryExtensions.ParseCanonicalName(x[1]));
            }

            var lines = File.ReadAllLines(path);

            CompleteScoreboardSummary summary = new CompleteScoreboardSummary();
            summary.TeamList = new List<ScoreboardSummaryEntry>();
            summary.SnapshotTimestamp = DateTimeOffset.Parse(timestamp);
            summary.OriginUri = string.IsNullOrEmpty(originUri) ? null : new Uri(originUri);

            Console.WriteLine("Loading score data");

            foreach (string[] data in lines.Skip(1).Select(line => line.Split(',')))
            {
                ScoreboardSummaryEntry entry = new ScoreboardSummaryEntry
                {
                    TeamId = TeamId.Parse(data[0]),
                    Division = Enum.Parse<Division>(data[1].Replace(" ", ""), true),
                    Category = string.IsNullOrEmpty(data[2]) ? categories.TryGetValue(TeamId.Parse(data[0]), out ServiceCategory c) ? (ServiceCategory?)c : null : ServiceCategoryExtensions.ParseAliasName(data[2].Trim()),
                    Location = data[3],
                    Tier = Enum.TryParse<Tier>(data[4],true,out Tier t) ? t : (Tier?)null,
                    ImageCount = int.Parse(data[5]),
                    PlayTime = ParseTimeSpan(data[6]),
                    TotalScore = double.Parse(data[7]),
                    Warnings = (data[8].Contains("M") ? ScoreWarnings.MultiImage : 0) | (data[8].Contains("T") ? ScoreWarnings.TimeOver : 0)
                };
                summary.TeamList.Add(entry);
            }

            Console.WriteLine("Generating output data");

            var o = new Output
            {
                round = roundNumber,
                summary = summary,
                teams = summary.TeamList.Select(x => new ScoreboardDetails
                {
                    Images = new List<ScoreboardImageDetails>
                        {
                            new ScoreboardImageDetails
                            {
                                ImageName = "All Points",
                                Penalties = 0,
                                PlayTime = x.PlayTime,
                                PointsPossible = x.ImageCount * 100,
                                Score = x.TotalScore,
                                VulnerabilitiesFound = 0,
                                VulnerabilitiesRemaining = 0,
                                Warnings = x.Warnings
                            }
                        },
                    ImageScoresOverTime = null,
                    OriginUri = null,
                    ScoreTime = x.PlayTime,
                    SnapshotTimestamp = DateTimeOffset.Parse(timestamp),
                    Summary = x
                }).ToDictionary(x => x.TeamId, x => x)
            };

            File.WriteAllText("scores.json", JsonConvert.SerializeObject(o));
            Console.WriteLine("Done");

            Console.ReadKey();
        }

        private static TimeSpan ParseTimeSpan(string s)
        {
            var parts = s.Split(':');
            return new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), 0);
        }
    }

    public class Output
    {
        public int round { get; set; }
        public CompleteScoreboardSummary summary { get; set; }
        public Dictionary<TeamId, ScoreboardDetails> teams { get; set; }
    }
}
