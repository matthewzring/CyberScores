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
        public double Score { get; set; }
        public double PointsPossible { get; set; } = -1;
        public ScoreWarnings Warnings { get; set; }
    }
}