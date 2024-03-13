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

namespace CyberPatriot.Models
{
    public class ScoreboardDetails
    {
        [Newtonsoft.Json.JsonIgnore]
        public TeamId TeamId
        {
            get => Summary?.TeamId ?? default(TeamId);
        }

        public ScoreboardSummaryEntry Summary { get; set; }
        public TimeSpan ScoreTime { get; set; }
        public List<ScoreboardImageDetails> Images { get; set; } = new List<ScoreboardImageDetails>();
        // FIXME: multi detail origin (hash property? null Uri?)
        public Uri OriginUri { get; set; }
        public DateTimeOffset SnapshotTimestamp { get; set; }
        public Dictionary<string, SortedDictionary<DateTimeOffset, int?>> ImageScoresOverTime { get; set; } = null;
        public string Comment { get; set; } = null;
    }
}
