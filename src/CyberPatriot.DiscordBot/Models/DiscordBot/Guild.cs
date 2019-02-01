#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

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