using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Services
{
    /// <summary>
    /// Retrieves scores from a released score spreadsheet.
    /// Note: multiplies scores by 100, TODO render this hack unnecessary.
    /// This class is a nasty hack bolted on top of existing score models.
    /// </summary>
    public class SpreadsheetScoreRetrievalService : IScoreRetrievalService
    {
        private SpreadsheetScoreRetrievalService()
        {
        }

        protected Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary> summariesByFilter =
            new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();

        protected Dictionary<TeamId, ScoreboardDetails> teamInfo = new Dictionary<TeamId, ScoreboardDetails>();

        public static async Task<SpreadsheetScoreRetrievalService> FromCsvAsync(params string[] filenames)
        {
            if (filenames == null)
            {
                throw new ArgumentNullException(nameof(filenames));
            }

            var newService = new SpreadsheetScoreRetrievalService();

            List<Task<string[]>> fileReadTasks =
                filenames.Select(filename => File.ReadAllLinesAsync(filename)).ToList();

            while (fileReadTasks.Count > 0)
            {
                Task<string[]> completed = await Task.WhenAny(fileReadTasks);
                fileReadTasks.Remove(completed);
                // shouldn't switch tasks because completed is already completed
                // will either immediately return or error out in the normal asyncy way
                string[] lines = await completed;
                int state = 0;

                string[] headers = null;
                int teamIdInd, divInd, locInd, tierInd;

                int lowerDataBound, upperDataBound;

                List<ScoreboardDetails> scoreDetailsOrdered = new List<ScoreboardDetails>();

                // default to file write time
                // FIXME can't easily pass filename in
                //DateTimeOffset snapshotTimestamp = File.GetLastWriteTimeUtc(filename);
                DateTimeOffset snapshotTimestamp = DateTimeOffset.UtcNow;

                Uri originUri = null;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    switch (state)
                    {
                        case 0:
                            // initial comment parsing
                            // these "comments" are assumed to contain metadata
                            if (!line.StartsWith('#'))
                            {
                                state = 1;

                                // reparse this line
                                i--;
                                continue;
                            }
                            string coreLine = line.Substring(1).TrimStart();
                            string[] kvp = coreLine.Split(new char[]{'='}, 2);
                            switch (kvp[0].TrimEnd())
                            {
                                case "timestamp":
                                    if (long.TryParse(kvp[1].Trim(), out long unixTimestamp))
                                    {
                                        snapshotTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp);
                                    }
                                    else
                                    {
                                        DateTimeOffset.TryParse(kvp[1].Trim(), out snapshotTimestamp);
                                    }
                                    break;
                                case "uri":
                                    originUri = new Uri(kvp[1].Trim());
                                    break;
                            }

                            break;
                        case 1:
                            // headers
                            headers = line.Split(',');
                            teamIdInd = Array.IndexOf(headers, "Team #");
                            if (teamIdInd == -1)
                            {
                                teamIdInd = 0;
                            }
                            divInd = Array.IndexOf(headers, "Division");
                            if (divInd == -1)
                            {
                                teamIdInd = 1;
                            }
                            locInd = Array.IndexOf(headers, "Location");
                            if (locInd == -1)
                            {
                                locInd = 2;
                            }
                            // may be -1
                            tierInd = Array.IndexOf(headers, "Tier");

                            // figure out bounds for data entries
                            // assume data are consecutive, tier is at the end
                            // +1 for an inclusive index
                            lowerDataBound = Utilities.Max(-1, teamIdInd, divInd, locInd) + 1;
                            // hack to deal with R2/R(3/4) spreadsheet discreptancies
                            // tiers at the end on R2 (that's the tier calculation round), but beginning on the rest
                            if (lowerDataBound == tierInd)
                            {
                                lowerDataBound++;
                            }
                            // exclusive index
                            int cumuScoreInd = Array.IndexOf(headers, "Cumulative Score");
                            upperDataBound = Utilities.Min(headers.Length,
                                cumuScoreInd == -1 ? int.MaxValue : cumuScoreInd);

                            // now in data-parsing mode
                            state = 2;
                            break;
                        case 2:
                            // data
                            string[] data = line.Split(',');

                            ScoreboardDetails teamInfo = new ScoreboardDetails();
                            teamInfo.Summary = new ScoreboardSummaryEntry();
                            teamInfo.Summary.ImageCount = upperDataBound - lowerDataBound;
                            teamInfo.Summary.Location = data[locInd];
                            teamInfo.Summary.PlayTime = TimeSpan.Zero;
                            teamInfo.Summary.Tier = tierInd == -1 ? null : data[tierInd];
                            if (Utilities.TryParseEnumSpaceless(data[divInd], out Division division))
                            {
                                teamInfo.Summary.Division = division;
                            }
                            teamInfo.Summary.TeamId = TeamId.Parse(data[teamIdInd]);
                            teamInfo.ScoreTime = TimeSpan.Zero;
                            teamInfo.SnapshotTimestamp = snapshotTimestamp;
                            teamInfo.Images = new List<ScoreboardImageDetails>();
                            teamInfo.OriginUri = originUri;

                            int totalScore = 0;

                            for (int j = lowerDataBound; j < upperDataBound; j++)
                            {
                                // we hack this into our system by multiplying everything by 100 so they're integers
                                ScoreboardImageDetails image = new ScoreboardImageDetails
                                {
                                    ImageName = headers[j],
                                    Penalties = 0,
                                    PlayTime = TimeSpan.Zero,
                                    VulnerabilitiesFound = 0,
                                    VulnerabilitiesRemaining = 0,
                                    Score = (int) (decimal.Parse(data[j]) * 100m)
                                };
                                totalScore += image.Score;
                                teamInfo.Images.Add(image);
                            }

                            teamInfo.Summary.TotalScore = totalScore;

                            scoreDetailsOrdered.Add(teamInfo);

                            // add to service
                            newService.teamInfo[teamInfo.TeamId] = teamInfo;

                            break;
                    }
                }

                // we've built our team list, now build the summary
                CompleteScoreboardSummary scoreboardSummary = new CompleteScoreboardSummary();
                scoreboardSummary.SnapshotTimestamp = snapshotTimestamp;
                scoreboardSummary.TeamList = scoreDetailsOrdered.Select(details => details.Summary).ToList();
                scoreboardSummary.Filter = new ScoreboardFilterInfo(
                    scoreboardSummary.TeamList.Select(sum => sum.Division).Cast<Division?>().SingleOrDefault(),
                    scoreboardSummary.TeamList.Select(sum => sum.Tier).SingleOrDefault());

                if (newService.summariesByFilter.TryGetValue(scoreboardSummary.Filter,
                    out CompleteScoreboardSummary existingSummary))
                {
                    // take newer timestamp for composite data
                    existingSummary.SnapshotTimestamp = Utilities.Max(existingSummary.SnapshotTimestamp,
                        scoreboardSummary.SnapshotTimestamp);
                    // union of team list
                    existingSummary.TeamList = existingSummary.TeamList.Union(scoreboardSummary.TeamList)
                        .OrderByDescending(team => team.TotalScore).ToList();
                }
                else
                {
                    newService.summariesByFilter[scoreboardSummary.Filter] = scoreboardSummary;
                }
            }

            if (!newService.summariesByFilter.ContainsKey(ScoreboardFilterInfo.NoFilter))
            {
                // create a composite score list from all data we have
                CompleteScoreboardSummary completeSummary = new CompleteScoreboardSummary();
                var existingSummaries = newService.summariesByFilter.Values;
                completeSummary.SnapshotTimestamp = existingSummaries.Max(sum => sum.SnapshotTimestamp);
                completeSummary.Filter = ScoreboardFilterInfo.NoFilter;
                completeSummary.TeamList = existingSummaries.SelectMany(sum => sum.TeamList).Distinct()
                    .OrderByDescending(team => team.TotalScore).ToList();
                newService.summariesByFilter[ScoreboardFilterInfo.NoFilter] = completeSummary;
            }

            return newService;
        }

        public Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            // TODO cache filtered lists?
            if (summariesByFilter.TryGetValue(filter, out CompleteScoreboardSummary summary))
            {
                return Task.FromResult(summary);
            }
            if (summariesByFilter.TryGetValue(new ScoreboardFilterInfo(filter.Division, null), out summary))
            {
                return Task.FromResult(summary.WithFilter(filter));
            }
            return Task.FromResult(summariesByFilter[ScoreboardFilterInfo.NoFilter].WithFilter(filter));
        }

        public Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            if (teamInfo.TryGetValue(team, out ScoreboardDetails details))
            {
                return Task.FromResult(details);
            }

            return Task.FromException<ScoreboardDetails>(new KeyNotFoundException());
        }
    }
}