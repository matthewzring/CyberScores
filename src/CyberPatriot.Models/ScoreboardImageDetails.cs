using System;

namespace CyberPatriot.Models
{
    public class ScoreboardImageDetails
    {
        public string ImageName { get; set; }
        public TimeSpan PlayTime { get; set; }
        public int VulnerabilitiesFound { get; set; }
        public int VulnerabilitiesRemaining { get; set; }
        public int Penalties { get; set; }
        public int Score { get; set; }
        public int PointsPossible { get; set; } = -1;
        public ScoreWarnings Warnings { get; set; }
    }
}