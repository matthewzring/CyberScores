#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Threading.Tasks;
using Discord;

namespace CyberScores.Services;

public class PreferenceProviderService
{
    public IDataPersistenceService Database { get; set; }

    public PreferenceProviderService(IDataPersistenceService database)
    {
        Database = database;
    }

    #region Timezone

    /// <summary>
    /// Gets a non-null TimeZoneInfo object for the preferred timezone of the given user (if given) in the given guild (if given). 
    /// The timezone of last resort is UTC.
    /// </summary>
    public async Task<TimeZoneInfo> GetTimeZoneAsync(IGuild guild = null, IUser user = null)
    {
        // Bot default
        TimeZoneInfo val = TimeZoneInfo.Utc;
        if (guild != null)
        {
            string tzStringGuild = (await Database.FindOneAsync<Models.Guild>(g => g.Id == guild.Id).ConfigureAwait(false))?.TimeZone;
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
            string tzStringUser = (await Database.FindOneAsync<Models.User>(u => u.Id == user.Id).ConfigureAwait(false))?.TimeZone;
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
            var guildSettings = await context.FindOneOrNewAsync(g => g.Id == guild.Id, () => new Models.Guild() { Id = guild.Id }).ConfigureAwait(false);
            guildSettings.TimeZone = tz?.Id;
            await context.SaveAsync(guildSettings).ConfigureAwait(false);
            await context.WriteAsync().ConfigureAwait(false);
        }
    }

    public async Task SetTimeZoneAsync(IUser user, TimeZoneInfo tz)
    {
        using (var context = Database.OpenContext<Models.User>(true))
        {
            var userSettings = await context.FindOneOrNewAsync(u => u.Id == user.Id, () => new Models.User() { Id = user.Id }).ConfigureAwait(false);
            userSettings.TimeZone = tz?.Id;
            await context.SaveAsync(userSettings).ConfigureAwait(false);
            await context.WriteAsync().ConfigureAwait(false);
        }
    }
}
#endregion
