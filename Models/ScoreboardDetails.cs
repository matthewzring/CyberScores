using System;
using System.Collections.Generic;

namespace CyberPatriot.Models
{
    public class ScoreboardDetails
    {
        public TeamId TeamId
        {
            get => Summary?.TeamId;
        }

        public ScoreboardSummary Summary { get; set; }
        public TimeSpan ScoreTime { get; set; }
        public List<ScoreboardImageDetails> Images { get; set; } = new List<ScoreboardImageDetails>();
        // FIXME: multi detail origin (hash property? null Uri?)
        public Uri OriginUri { get; set; }
    }
}