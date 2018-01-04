using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CyberPatriot.DiscordBot.Services
{
    /// <summary>
    /// Retrieves scores from a released score spreadsheet.
    /// Note: multiplies scores by 100, TODO render this hack unnecessary.
    /// This class is a nasty hack bolted on top of existing score models.
    /// </summary>
    public class SpreadsheetScoreRetrievalService : IScoreRetrievalService
    {

        public bool IsDynamic => false;
        public string StaticSummaryLine => Round == 0 ? "Official Scores Released" : "Official " + Round.ToStringCamelCaseToSpace() + " Scores";
        public ScoreFormattingOptions FormattingOptions { get; private set; }
        public CompetitionRound Round { get; set; } = 0;


        public SpreadsheetScoreRetrievalService()
        {
            // hacky implementation of a decent idea
            // add decimals at format level to embed creator
            // set format options to display decimals, overriding anything else that may have been set :(
            FormattingOptions = new ScoreFormattingOptions();
            FormattingOptions.FormatScore = rawScore => (rawScore / 100.0m).ToString();
            FormattingOptions.FormatLabeledScoreDifference = rawScore => (rawScore / 100.0m) + " point" + (rawScore == 100 ? string.Empty : "s");
            FormattingOptions.FormatScoreForLeaderboard = rawScore => (rawScore / 100.0m).ToString("0.00");
            FormattingOptions.TimeDisplay = ScoreFormattingOptions.NumberDisplayCriteria.Never;
            FormattingOptions.VulnerabilityDisplay = ScoreFormattingOptions.NumberDisplayCriteria.Never;
        }

        protected Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary> summariesByFilter =
            new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();

        protected Dictionary<TeamId, ScoreboardDetails> teamInfo = new Dictionary<TeamId, ScoreboardDetails>();

        public Task InitializeAsync(IServiceProvider provider)
        {
            // reset
            summariesByFilter = new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();
            teamInfo = new Dictionary<TeamId, ScoreboardDetails>();

            return InitializeFromConfiguredCsvAsync(provider);
        }

        public Task<SpreadsheetScoreRetrievalService> InitializeFromConfiguredCsvAsync(IServiceProvider serviceProvider)
        {
            var conf = serviceProvider.GetRequiredService<IConfiguration>();
            string[] srcList;
            try
            {
                srcList = conf.GetSection("csvSources").Get<string[]>();
            }
            catch
            {
                srcList = null;
            }

            try
            {
                Round = (CompetitionRound)conf.GetValue("csvRound", 0);
            }
            catch
            {
                Round = 0;
            }

            if ((srcList?.Count(s => !string.IsNullOrWhiteSpace(s)) ?? 0) == 0)
            {
                srcList = null;
            }

            return srcList == null ? null : InitializeFromCsvAsync(srcList);
        }

        public async Task<SpreadsheetScoreRetrievalService> InitializeFromCsvAsync(params string[] filenames)
        {
            if (filenames == null)
            {
                throw new ArgumentNullException(nameof(filenames));
            }
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
                int[] maxImagePoints = null;
                int teamIdInd = -1, divInd = -1, locInd = -1, tierInd = -1, catInd = -1;
                Division defaultDiv = 0;

                int lowerDataBound = 0, upperDataBound = 0;

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
                            string[] kvp = coreLine.Split(new char[] { '=' }, 2);
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
                            catInd = Array.IndexOf(headers, "Category");
                            if (catInd == -1)
                            {
                                divInd = Array.IndexOf(headers, "Division");
                                if (divInd == -1)
                                {
                                    divInd = 1;
                                }
                            }
                            else
                            {
                                defaultDiv = Division.AllService;
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

                            maxImagePoints = new int[headers.Length];
                            for (int j = 0; j < maxImagePoints.Length; j++)
                            {
                                maxImagePoints[j] = -1;
                            }
                            for (int j = lowerDataBound; j < upperDataBound; j++)
                            {
                                string[] imageHeaderComponents = headers[j].Split('|');
                                if (imageHeaderComponents.Length > 1)
                                {
                                    headers[j] = imageHeaderComponents[0];
                                    if (decimal.TryParse(imageHeaderComponents[1], out decimal maxScore))
                                    {
                                        maxImagePoints[j] = (int)(maxScore * 100m);
                                    }
                                }
                            }

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
                            if (catInd >= 0)
                            {
                                teamInfo.Summary.Division = defaultDiv;
                                teamInfo.Summary.Category = data[catInd];
                            }
                            else if(Utilities.TryParseEnumSpaceless(data[divInd], out Division division))
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
                                    PointsPossible = maxImagePoints[j],
                                    Penalties = 0,
                                    PlayTime = TimeSpan.Zero,
                                    VulnerabilitiesFound = 0,
                                    VulnerabilitiesRemaining = 0,
                                    Score = (int)(data[j].Trim().Length > 0 ? decimal.Parse(data[j]) * 100m : 0m)
                                };
                                totalScore += image.Score;
                                teamInfo.Images.Add(image);
                            }

                            teamInfo.Summary.TotalScore = totalScore;

                            scoreDetailsOrdered.Add(teamInfo);

                            // add to service
                            this.teamInfo[teamInfo.TeamId] = teamInfo;

                            break;
                    }
                }

                // we've built our team list, now build the summary
                CompleteScoreboardSummary scoreboardSummary = new CompleteScoreboardSummary();
                scoreboardSummary.SnapshotTimestamp = snapshotTimestamp;
                scoreboardSummary.TeamList = scoreDetailsOrdered.Select(details => details.Summary).ToList();
                scoreboardSummary.Filter = new ScoreboardFilterInfo(
                    scoreboardSummary.TeamList.Select(sum => sum.Division).Distinct().Cast<Division?>().SingleIfOne(),
                    scoreboardSummary.TeamList.Select(sum => sum.Tier).Distinct().SingleIfOne());
                scoreboardSummary.OriginUri = originUri;

                if (this.summariesByFilter.TryGetValue(scoreboardSummary.Filter,
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
                    this.summariesByFilter[scoreboardSummary.Filter] = scoreboardSummary;
                }
            }

            if (!this.summariesByFilter.ContainsKey(ScoreboardFilterInfo.NoFilter))
            {
                // create a composite score list from all data we have
                CompleteScoreboardSummary completeSummary = new CompleteScoreboardSummary();
                var existingSummaries = this.summariesByFilter.Values;
                completeSummary.SnapshotTimestamp = existingSummaries.Max(sum => sum.SnapshotTimestamp);
                completeSummary.Filter = ScoreboardFilterInfo.NoFilter;
                completeSummary.TeamList = existingSummaries.SelectMany(sum => sum.TeamList).Distinct()
                    .OrderByDescending(team => team.TotalScore).ToList();
                completeSummary.OriginUri = existingSummaries.Select(sum => sum.OriginUri).Distinct().SingleIfOne();
                this.summariesByFilter[ScoreboardFilterInfo.NoFilter] = completeSummary;
            }

            return this;
        }

        public Task<CompleteScoreboardSummary> GetScoreboardAsync(ScoreboardFilterInfo filter)
        {
            // TODO cache filtered lists?
            if (summariesByFilter.TryGetValue(filter, out CompleteScoreboardSummary summary))
            {
                return Task.FromResult(summary.Clone());
            }
            if (summariesByFilter.TryGetValue(new ScoreboardFilterInfo(filter.Division, null), out summary))
            {
                return Task.FromResult(summary.Clone().WithFilter(filter));
            }
            return Task.FromResult(summariesByFilter[ScoreboardFilterInfo.NoFilter].Clone().WithFilter(filter));
        }

        public Task<ScoreboardDetails> GetDetailsAsync(TeamId team)
        {
            if (teamInfo.TryGetValue(team, out ScoreboardDetails details))
            {
                return Task.FromResult(details);
            }

            return Task.FromException<ScoreboardDetails>(new ArgumentException("The given team does not exist."));
        }
    }
}