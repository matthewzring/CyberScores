using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models
{
    public class User
    {
        public ulong Id { get; set; }
        public string TimeZone { get; set; }

        protected bool Equals(User other)
        {
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((User)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static System.Threading.Tasks.Task<User> OpenWriteGuildSettingsAsync(
            Services.IDataPersistenceContext<User> context, ulong userId)
            => context.FindOneOrNewAsync(u => u.Id == userId, () => new User() { Id = userId });
    }
}