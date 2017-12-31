using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class Channel
    {
        public ulong Id { get; set; }
        public List<CyberPatriot.Models.TeamId> MonitoredTeams { get; set; } = new List<CyberPatriot.Models.TeamId>();

        protected bool Equals(Channel other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Channel) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}