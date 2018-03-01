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