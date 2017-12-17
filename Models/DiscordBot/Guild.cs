using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class Guild
    {
        public ulong Id { get; set; }
        public string Prefix { get; set; }
        public string TimeZone { get; set; }
        public List<Channel> ChannelSettings { get; set; } = new List<Channel>();
    }
}