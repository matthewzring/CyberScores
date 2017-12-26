using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Discord;

namespace CyberPatriot.DiscordBot.Services
{
    public class PreferenceProviderService
    {
        public IDataPersistenceService Database { get; set; }

        public PreferenceProviderService(IDataPersistenceService database)
        {
            Database = database;
        }

        #region Timezone
        public async Task<TimeZoneInfo> GetTimeZoneAsync(IGuild guild = null, IUser user = null)
        {
            // Bot default
            TimeZoneInfo val = TimeZoneInfo.Utc;
            if (guild != null)
            {
                string tzStringGuild = (await Database.FindOneAsync<Models.Guild>(g => g.Id == guild.Id))?.TimeZone;
                if (tzStringGuild != null)
                {
                    try
                    {
                        val = TimeZoneInfo.FindSystemTimeZoneById(tzStringGuild);
                    }
                    catch
                    {

                    }
                }
            }
            if (user != null)
            {
                string tzStringUser = (await Database.FindOneAsync<Models.User>(u => u.Id == user.Id))?.TimeZone;
                if (tzStringUser != null)
                {
                    try
                    {
                        val = TimeZoneInfo.FindSystemTimeZoneById(tzStringUser);
                    }
                    catch
                    {

                    }
                }
            }
            return val;
        }

        public async Task SetTimeZoneAsync(IGuild guild, TimeZoneInfo tz)
        {
            using (var context = Database.OpenContext<Models.Guild>(true))
            {
                var guildSettings = await context.FindOneOrNewAsync(g => g.Id == guild.Id, () => new Models.Guild() { Id = guild.Id });
                guildSettings.TimeZone = tz?.Id;
                await context.SaveAsync(guildSettings);
                await context.WriteAsync();
            }
        }

        public async Task SetTimeZoneAsync(IUser user, TimeZoneInfo tz)
        {
            using (var context = Database.OpenContext<Models.User>(true))
            {
                var userSettings = await context.FindOneOrNewAsync(u => u.Id == user.Id, () => new Models.User() { Id = user.Id });
                userSettings.TimeZone = tz?.Id;
                await context.SaveAsync(userSettings);
                await context.WriteAsync();
            }
        }
    }
    #endregion
}