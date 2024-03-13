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
using System.Linq;
using System.Threading.Tasks;
using CyberPatriot.Models;
using Microsoft.Extensions.Configuration;

namespace CyberPatriot.Services.ScoreRetrieval
{
    /// <summary>
    /// Retrieves scores from a released score spreadsheet.
    /// </summary>
    public class SpreadsheetScoreRetrievalService : IScoreRetrievalService
    {
        Metadata.IScoreRetrieverMetadata IScoreRetrievalService.Metadata => Metadata;
        protected Metadata.ScoreRetrieverMetadata Metadata { get; set; } = new Metadata.ScoreRetrieverMetadata()
        {
            IsDynamic = false,
            SupportsInexpensiveDetailQueries = true,
            StaticSummaryLine = "Official Scores Released"
        };

        private CompetitionRound _round;
        public CompetitionRound Round
        {
            get => _round;
            set
            {
                _round = value;
                Metadata.StaticSummaryLine = Round == 0 ? "Official Scores Released" : "Official " + Round.ToStringCamelCaseToSpace() + " Scores";
            }
        }

        public SpreadsheetScoreRetrievalService()
        {
            // hacky implementation of a decent idea
            // add decimals at format level to embed creator
            // set format options to display decimals, overriding anything else that may have been set :(
            var formattingOptions = new Metadata.ScoreFormattingOptions
            {
                TimeDisplay = Services.Metadata.ScoreFormattingOptions.NumberDisplayCriteria.Never,
                VulnerabilityDisplay = Services.Metadata.ScoreFormattingOptions.NumberDisplayCriteria.Never,
                FormatScore = i => i.ToString("0.00"),
                FormatLabeledScoreDifference = i => i.ToString("0.00") + " points"
            };
            Metadata.FormattingOptions = formattingOptions;
        }

        protected Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary> summariesByFilter =
            new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();

        protected Dictionary<TeamId, ScoreboardDetails> teamInfo = new Dictionary<TeamId, ScoreboardDetails>();

        public Task InitializeAsync(IServiceProvider provider, IConfigurationSection conf)
        {
            // reset
            summariesByFilter = new Dictionary<ScoreboardFilterInfo, CompleteScoreboardSummary>();
            teamInfo = new Dictionary<TeamId, ScoreboardDetails>();

            string[] srcList;
            try
            {
                srcList = conf.GetSection("sources").Get<string[]>();
            }
            catch
            {
                srcList = null;
            }

            try
            {
                Round = (CompetitionRound)conf.GetValue("round", 0);
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
                filenames.Select(filename => Utilities.ReadAllLinesAsync(filename)).ToList();

            while (fileReadTasks.Count > 0)
            {
                Task<string[]> completed = await Task.WhenAny(fileReadTasks).ConfigureAwait(false);
                fileReadTasks.Remove(completed);
                // shouldn't switch tasks because completed is already completed
                // will either immediately return or error out in the normal asyncy way
                string[] lines = await completed;
                int state = 0;

                string[] headers = null;
                double[] maxImagePoints = null;
                int teamIdInd = -1, divInd = -1, locInd = -1, tierInd = -1, catInd = -1, advancementInd = -1, commentInd = -1;
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
                            if (!line.StartsWith("#"))
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
                                case "providerName":
                                    this.Metadata.StaticSummaryLine = kvp[1].Trim();
                                    break;
                            }

                            break;
                        case 1:
                            // headers
                            headers = line.Split(',');
                            teamIdInd = headers.IndexOfWhere(s => s == "Team #" || s == "Team");
                            if (teamIdInd == -1)
                            {
                                teamIdInd = 0;
                            }
                            catInd = Array.IndexOf(headers, "Category");
                            divInd = Array.IndexOf(headers, "Division");
                            if (divInd == -1)
                            {
                                if (catInd == -1)
                                {
                                    divInd = 1;
                                }
                                else
                                {
                                    defaultDiv = Division.AllService;
                                }
                            }
                            locInd = Array.IndexOf(headers, "Location");
                            if (locInd == -1)
                            {
                                locInd = 2;
                            }
                            // may be -1
                            tierInd = Array.IndexOf(headers, "Tier");

                            // only optional field (not on every row)
                            // is at end
                            commentInd = Array.IndexOf(headers, "Comment");

                            // figure out bounds for data entries
                            // assume data are consecutive, tier is at the end
                            // +1 for an inclusive index
                            lowerDataBound = Utilities.Max(-1, teamIdInd, divInd, catInd, locInd) + 1;
                            // hack to deal with R2/R(3/4) spreadsheet discreptancies
                            // tiers at the end on R2 (that's the tier calculation round), but beginning on the rest
                            if (lowerDataBound == tierInd)
                            {
                                lowerDataBound++;
                            }
                            // exclusive index
                            int cumuScoreInd = headers.IndexOfWhere(s => s == "Cumulative" || s == "Cumulative Score");
                            advancementInd = headers.IndexOfWhere(s => s == "Advancement" || s == "Advances");

                            upperDataBound = Utilities.Min(headers.Length,
                                cumuScoreInd == -1 ? int.MaxValue : cumuScoreInd,
                                advancementInd == -1 ? int.MaxValue : advancementInd,
                                commentInd == -1 ? int.MaxValue : commentInd);

                            maxImagePoints = new double[headers.Length];
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
                                    if (double.TryParse(imageHeaderComponents[1], out double maxScore))
                                    {
                                        maxImagePoints[j] = maxScore;
                                    }
                                }
                            }

                            // now in data-parsing mode
                            state = 2;
                            break;
                        case 2:
                            // data
                            string[] data = line.Split(',');

                            ScoreboardDetails teamInfo = new ScoreboardDetails
                            {
                                Summary = new ScoreboardSummaryEntry
                                {
                                    ImageCount = upperDataBound - lowerDataBound,
                                    Location = data[locInd],
                                    PlayTime = TimeSpan.Zero,
                                    Tier = tierInd == -1 || !Enum.TryParse<Tier>(data[tierInd], true, out Tier tier) ? null : (Tier?)tier
                                }
                            };
                            if (catInd >= 0 && !string.IsNullOrWhiteSpace(data[catInd]))
                            {
                                teamInfo.Summary.Category = CyberPatriot.Models.Serialization.ServiceCategoryExtensions.ParseAliasName(data[catInd]);
                            }
                            if (divInd >= 0 && Utilities.TryParseEnumSpaceless(data[divInd], out Division division))
                            {
                                teamInfo.Summary.Division = division;
                            }
                            else if (catInd >= 0)
                            {
                                teamInfo.Summary.Division = defaultDiv;
                            }
                            if (advancementInd >= 0)
                            {
                                // squishy parse
                                string advStr = data[advancementInd].ToLower().Trim();
                                if (advStr == "does not advance")
                                {
                                    teamInfo.Summary.Advancement = Advancement.Eliminated;
                                }
                                else if (advStr.Contains("wildcard"))
                                {
                                    teamInfo.Summary.Advancement = Advancement.Wildcard;
                                }
                                else if (advStr.Contains("advances"))
                                {
                                    teamInfo.Summary.Advancement = Advancement.Advances;
                                }
                            }
                            teamInfo.Summary.TeamId = TeamId.Parse(data[teamIdInd]);
                            teamInfo.ScoreTime = TimeSpan.Zero;
                            teamInfo.SnapshotTimestamp = snapshotTimestamp;
                            teamInfo.Images = new List<ScoreboardImageDetails>();
                            teamInfo.OriginUri = originUri;

                            if (commentInd >= 0 && commentInd < data.Length)
                            {
                                teamInfo.Comment = data[commentInd];
                            }

                            double totalScore = 0;

                            for (int j = lowerDataBound; j < upperDataBound; j++)
                            {
                                ScoreboardImageDetails image = new ScoreboardImageDetails
                                {
                                    ImageName = headers[j],
                                    PointsPossible = maxImagePoints[j],
                                    Penalties = 0,
                                    PlayTime = TimeSpan.Zero,
                                    VulnerabilitiesFound = 0,
                                    VulnerabilitiesRemaining = 0,
                                    Score = data[j].Trim().Length > 0 ? double.Parse(data[j]) : 0.0
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
                CompleteScoreboardSummary scoreboardSummary = new CompleteScoreboardSummary
                {
                    SnapshotTimestamp = snapshotTimestamp,
                    TeamList = scoreDetailsOrdered.Select(details => details.Summary).ToIList()
                };
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
                        .OrderByDescending(team => team.TotalScore).ToIList();
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
                    .OrderByDescending(team => team.TotalScore).ToIList();
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
