using System;

namespace CyberPatriot.Models
{
    public class ScoreboardSummary
    {
        public TeamID TeamID { get; set; }
        public string Location { get; set; }
        public Division Division { get; set; }
        public string Tier { get; set; }
        public int ImageCount { get; set; }
        public TimeSpan PlayTime { get; set; }
        public int TotalScore { get; set; }
        public ScoreWarnings Warnings { get; set; }
    }
}