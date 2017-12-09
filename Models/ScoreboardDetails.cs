using System;
using System.Collections.Generic;

namespace CyberPatriot.Models
{
    public class ScoreboardDetails
    {
        public TeamID TeamID
        {
            get => Summary?.TeamID;
        }

        public ScoreboardSummary Summary { get; set; }
        public TimeSpan ScoreTime { get; set; }
        public List<ScoreboardImageDetails> Images { get; set; } = new List<ScoreboardImageDetails>();
    }
}