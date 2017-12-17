using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class Channel
    {
        public ulong Id { get; set; }
        public List<CyberPatriot.Models.TeamId> MonitoredTeams { get; set; } = new List<CyberPatriot.Models.TeamId>();
    }
}