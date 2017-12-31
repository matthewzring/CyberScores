using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class Guild
    {
        public ulong Id { get; set; }
        public string Prefix { get; set; }
        public string TimeZone { get; set; }
        public Dictionary<ulong, Channel> ChannelSettings { get; set; } = new Dictionary<ulong, Channel>();

        protected bool Equals(Guild other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Guild) obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static System.Threading.Tasks.Task<Guild> OpenWriteGuildSettingsAsync(
            Services.IDataPersistenceContext<Guild> context, ulong guildId)
            => context.FindOneOrNewAsync(g => g.Id == guildId, () => new Guild() {Id = guildId});
    }
}