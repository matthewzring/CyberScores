using System;
using System.Collections.Generic;

namespace CyberPatriot.Models
{
    public class TeamDetailRankingInformation
    {
        public TeamId TeamId { get; set; }
        public int PeerIndex { get; set; }
        public int PeerCount { get; set; }
        public int DivisionIndex { get; set; }
        public int DivisionCount { get; set; }
        public int TierIndex { get; set; }
        public int TierCount { get; set; }
        public IList<ScoreboardSummaryEntry> Peers { get; set; }
    }
}