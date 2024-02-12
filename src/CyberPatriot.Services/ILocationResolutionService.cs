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

using System;
using System.Threading.Tasks;

namespace CyberPatriot.Services
{
    public interface ILocationResolutionService
    {
        Task InitializeAsync(IServiceProvider provider);

        /// <summary>
        /// Determines whether the given code corresponds to a valid location.
        /// </summary>
        /// <param name="locationCode">A potential location code.</param>
        /// <returns>True if and only if the given location code corresponds to a valid location.</returns>
        bool IsValidLocation(string locationCode);

        /// <summary>
        /// Gets the full name for the location with the given code.
        /// </summary>
        /// <param name="locationCode">The two or three letter location code.</param>
        /// <returns>The full, human-readable name for the given location.</returns>
        /// <exception cref="ArgumentNullException">If the given <paramref name="locationCode"/> is null.</exception>
        /// <exception cref="ArgumentException">If the given location code does not exist.</exception>
        string GetFullName(string locationCode);

        /// <summary>
        /// Gets the abbreviation for the given location.
        /// Matching is exact, but case-insensitive.
        /// </summary>
        /// <param name="locationName">The location name</param>
        /// <returns>The two or three letter location code.</returns>
        /// <exception cref="ArgumentNullException">If the given <paramref name="locationCode"/> is null.</exception>
        /// <exception cref="ArgumentException">If the given location does not exist.</exception>
        string GetAbbreviation(string locationName);

        /// <summary>
        /// Gets a Uri referring to a bitmap image of the flag of the given location.
        /// </summary>
        /// <param name="locationCode">The code of the location whose flag should be retrieved.</param>
        /// <exception cref="ArgumentException">If the given location code does not exist.</exception>
        /// <returns>A Uri referring to the flag image; <code>null</code> if no flag exists.</returns>
        Uri GetFlagUri(string locationCode);
    }

    public class NullIdentityLocationResolutionService : ILocationResolutionService
    {
        public bool IsValidLocation(string locationCode) => locationCode != null;

        public string GetAbbreviation(string locationName) => locationName ?? throw new ArgumentNullException(nameof(locationName));

        public string GetFullName(string locationCode) => locationCode ?? throw new ArgumentNullException(nameof(locationCode));

        public Uri GetFlagUri(string locationCode)
        {
            if (locationCode == null)
            {
                throw new ArgumentNullException(nameof(locationCode));
            }

            return null;
        }

        public Task InitializeAsync(IServiceProvider provider) => Task.CompletedTask;
    }

    public static class LocationResolutionExtensions
    {
        public static string GetFullNameOrNull(this ILocationResolutionService service, string locationCode)
        {
            if (service == null)
            {
                throw new NullReferenceException();
            }

            if (!service.IsValidLocation(locationCode))
            {
                return null;
            }

            return service.GetFullName(locationCode);
        }

        public static Uri GetFlagUriOrNull(this ILocationResolutionService service, string locationCode)
        {
            if (service == null)
            {
                throw new NullReferenceException();
            }

            if (!service.IsValidLocation(locationCode))
            {
                return null;
            }

            return service.GetFlagUri(locationCode);
        }
    }
}
