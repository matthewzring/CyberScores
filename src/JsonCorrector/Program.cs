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

using CyberPatriot.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace JsonCorrector
{
    public class Program
    {
        enum ErrorState
        {
            None,
            NoPrefix,
            Success,
            Warning,
            Attention,
            Error
        }

        static void WriteLine(string message, int indentLevel = 0, ErrorState error = ErrorState.None)
        {
            var fgColor = Console.ForegroundColor;

            Console.Write(new string(' ', indentLevel * 2));

            if (error != ErrorState.NoPrefix)
            {

                Console.Write('[');
                switch (error)
                {
                    case ErrorState.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write('X');
                        break;
                    case ErrorState.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write('!');
                        break;
                    case ErrorState.Success:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write('√');
                        break;
                    case ErrorState.Attention:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write('*');
                        break;
                    case ErrorState.None:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write('-');
                        break;
                }
                Console.ForegroundColor = fgColor;
                Console.Write("] ");
            }

            Console.WriteLine(message);
        }

        public class InterpretedGlobals
        {
            public CompleteScoreboardSummary Summary;
            public Dictionary<TeamId, ScoreboardDetails> Details;
            public CompetitionRound Round;
        }

        static void Main(string[] args)
        {
            WriteLine("Welcome to the CyberPatriot scoreboard bot JSON data corrector. This program will aid you in identifying and correcting flaws in your data.");
            WriteLine("Corrective actions taken will be marked with an attention prefix, like this message.", error: ErrorState.Attention);
            Console.WriteLine();
            Console.WriteLine("Enter JSON filepath:");
            JObject obj = JObject.Parse(File.ReadAllText(Console.ReadLine()));
            var summary = obj["summary"].ToObject<CompleteScoreboardSummary>();
            var teamDetails = obj["teams"].ToObject<Dictionary<TeamId, ScoreboardDetails>>();
            // preemptive workaround, see #18
            WriteLine("Ensuring snapshot timestamps are UTC", error: ErrorState.Attention);
            summary.SnapshotTimestamp = summary.SnapshotTimestamp.ToUniversalTime();
            foreach (var teamData in teamDetails.Values)
            {
                teamData.SnapshotTimestamp = teamData.SnapshotTimestamp.ToUniversalTime();
            }
            var round = (CompetitionRound)obj["round"].Value<int>();

            WriteLine($"Parsed {summary.TeamList.Count} team(s) from summary for round {round}");
            if (teamDetails.Keys.OrderBy(x => x.TeamNumber).SequenceEqual(summary.TeamList.Select(s => s.TeamId).OrderBy(x => x.TeamNumber)))
            {
                WriteLine("Details teamlist matches summary teamlist", indentLevel: 1, error: ErrorState.Success);
            }
            else
            {
                WriteLine("Details teamlist does not match summary teamlist, printing diff", indentLevel: 1, error: ErrorState.Error);
                WriteLine("Teams with details without summary:", indentLevel: 2);
                foreach (var teamId in teamDetails.Keys.Except(summary.TeamList.Select(s => s.TeamId)))
                {
                    WriteLine(teamId.ToString(), indentLevel: 2, error: ErrorState.NoPrefix);
                }
                WriteLine("Teams in summary without details:", indentLevel: 2);
                foreach (var teamId in summary.TeamList.Select(s => s.TeamId).Except(teamDetails.Keys))
                {
                    WriteLine(teamId.ToString(), indentLevel: 2, error: ErrorState.NoPrefix);
                }
            }

            var completeTeamSet = teamDetails.Keys.Intersect(summary.TeamList.Select(s => s.TeamId)).ToList();
            WriteLine($"There are {completeTeamSet.Count} fully-represented team(s)");
            var summaryProperties = typeof(ScoreboardSummaryEntry).GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.Name != "Category");
            var summariesDisagreeExcludingCategory = completeTeamSet
                .Select(x => new { TeamId = x, SummarySummary = summary.TeamList.Single(y => y.TeamId == x), DetailsSummary = teamDetails[x].Summary })
                .Where(x =>
                 {
                     var sumSumCat0 = x.SummarySummary.Category;
                     var detSumCat0 = x.DetailsSummary.Category;
                     x.DetailsSummary.Category = null;
                     x.SummarySummary.Category = null;
                     bool eqComparison = x.DetailsSummary.Equals(x.SummarySummary);
                     x.DetailsSummary.Category = detSumCat0;
                     x.SummarySummary.Category = sumSumCat0;
                     return !eqComparison;
                 }).ToList();
            if (summariesDisagreeExcludingCategory.Count > 0)
            {
                WriteLine($"Excluding purely ServiceCategory disagreements, there are {summariesDisagreeExcludingCategory.Count} teams whose summaries disagree", indentLevel: 1, error: ErrorState.Error);
                WriteLine("Here they are:", indentLevel: 2);
                foreach (var disagreement in summariesDisagreeExcludingCategory)
                {
                    WriteLine(disagreement.TeamId.ToString(), indentLevel: 2, error: ErrorState.NoPrefix);
                    foreach (var propToCompare in summaryProperties)
                    {
                        var sumVal = propToCompare.GetValue(disagreement.SummarySummary);
                        var detVal = propToCompare.GetValue(disagreement.DetailsSummary);
                        if (!Equals(sumVal, detVal))
                        {
                            WriteLine($"{propToCompare.Name}: s = '{sumVal}', d = '{detVal}'", indentLevel: 3, error: ErrorState.NoPrefix);
                        }
                    }
                }
            }
            else
            {
                WriteLine($"Notwithstanding ServiceCategory (soon to be inspected), all teams agree within their scoreboard and details on their summaries", indentLevel: 1, error: ErrorState.Success);
            }

            var categoryDisagreements = completeTeamSet
                .Select(x => new { TeamId = x, SummaryCategory = summary.TeamList.Single(y => y.TeamId == x).Category, DetailsCategory = teamDetails[x].Summary.Category })
                .Where(x => x.SummaryCategory != x.DetailsCategory).ToList();

            WriteLine($"There are {categoryDisagreements.Count} team(s) whose details-summary and scoreboard-summary category markings disagree, of which {categoryDisagreements.Count(x => x.SummaryCategory.HasValue && x.DetailsCategory.HasValue)} are in direct conflict", error: categoryDisagreements.Count == 0 ? ErrorState.Success : ErrorState.Error);
            if (categoryDisagreements.Count > 0)
            {
                WriteLine("Here they are (with warnings indicating inability to take corrective action):", indentLevel: 1);
                foreach (var disagreement in categoryDisagreements)
                {
                    bool directConflict = disagreement.SummaryCategory.HasValue && disagreement.DetailsCategory.HasValue;
                    WriteLine(disagreement.TeamId.ToString(), indentLevel: 2, error: directConflict ? ErrorState.Warning : ErrorState.Attention);
                    if (!directConflict)
                    {
                        // since these two disagree, and at least one is null, that means this team has a category
                        ServiceCategory cat = disagreement.SummaryCategory ?? disagreement.DetailsCategory ?? (ServiceCategory)(-1);
                        summary.TeamList.Single(y => y.TeamId == disagreement.TeamId).Category = cat;
                        teamDetails[disagreement.TeamId].Summary.Category = cat;
                    }
                }
            }

            var asTeams = completeTeamSet.Select(x => summary.TeamList.Single(y => y.TeamId == x)).Where(x => x.Division == Division.AllService).ToList();
            if (asTeams.Any(x => x.Category.HasValue))
            {
                var asTeamsCategoryless = asTeams.Where(x => !x.Category.HasValue).ToList();
                WriteLine($"There are {asTeamsCategoryless.Count} service team(s) without category data", error: asTeamsCategoryless.Count == 0 ? ErrorState.Success : ErrorState.Error);
                foreach (var team in asTeamsCategoryless)
                {
                    WriteLine(team.TeamId.ToString(), indentLevel: 1, error: ErrorState.NoPrefix);
                }
            }
            else
            {
                WriteLine("Service teams are consistent in not having category data", error: ErrorState.Success);
            }
            var nonAsTeams = completeTeamSet.Select(x => summary.TeamList.Single(y => y.TeamId == x)).Where(x => x.Division != Division.AllService).ToList();
            if (nonAsTeams.Any(x => x.Category.HasValue))
            {
                var nonAsTeamsCategoried = nonAsTeams.Where(x => x.Category.HasValue).ToList();
                WriteLine($"There are {nonAsTeamsCategoried.Count} non-service team(s) with category data", error: ErrorState.Error);
                foreach (var team in nonAsTeamsCategoried)
                {
                    WriteLine(team.TeamId.ToString(), indentLevel: 1, error: ErrorState.NoPrefix);
                }
            }
            else
            {
                WriteLine("Non-service teams are consistent in not having category data", error: ErrorState.Success);
            }

            Console.WriteLine();
            Console.WriteLine("If you would like to write any C# to adjust this state beyond automated corrections, please type non-blank lines. Otherwise, press return.");
            Console.WriteLine("The following globals are available to you:");
            foreach (var field in typeof(InterpretedGlobals).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Console.WriteLine($"{field.Name}\t{field.FieldType}");
            }
            var cSharpScript = new StringBuilder();

            string appendedLine;
            do
            {
                appendedLine = Console.ReadLine();
                cSharpScript.AppendLine(appendedLine);
            } while (!string.IsNullOrWhiteSpace(appendedLine));

            string cSharpCode = cSharpScript.ToString();
            if (!string.IsNullOrWhiteSpace(cSharpCode))
            {
                var globals = new InterpretedGlobals();
                globals.Details = teamDetails;
                globals.Summary = summary;
                globals.Round = round;
                try
                {
                    CSharpScript.RunAsync(cSharpCode,
                    globals: globals,
                    options: ScriptOptions.Default
                    .WithReferences(typeof(TeamId).Assembly)
                    .WithImports("System", "CyberPatriot.Models", "System.Linq", "System.Collections.Generic")).Wait();

                    round = globals.Round;
                    Console.WriteLine("Script executed successfully.");
                }
                catch (Exception e)
                {
                    WriteLine("Error running your script! Oh well, I hope it all worked.", error: ErrorState.Error);
                    Console.WriteLine(e);
                }
            }
            else
            {
                Console.WriteLine("No script entered.");
            }

            Console.WriteLine();
            Console.WriteLine("Where would you like to save the modified file?");
            string targetPath = Console.ReadLine();
            using (JsonWriter jw = new JsonTextWriter(new StreamWriter(File.OpenWrite(targetPath))))
            {
                // write
                jw.WriteStartObject();
                jw.WritePropertyName("summary");
                var serializer = JsonSerializer.CreateDefault();
                // serialize
                serializer.Serialize(jw, summary);
                jw.WritePropertyName("teams");
                serializer.Serialize(jw, teamDetails);
                jw.WritePropertyName("round");
                jw.WriteValue((int)round);
                jw.WriteEndObject();
                jw.Flush();
            }
            Console.WriteLine("Done.");
        }
    }
}
