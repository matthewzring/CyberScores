using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
    public interface ILocationResolutionService
    {
        Task InitializeAsync(IServiceProvider provider);

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
    }

    public class FileBackedLocationResolutionService : ILocationResolutionService
    {
        protected Dictionary<string, string> _codesToNames = new Dictionary<string, string>();
        protected Dictionary<string, string> _namesToCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string GetAbbreviation(string locationName)
        {
            if (locationName == null)
            {
                throw new ArgumentNullException(nameof(locationName));
            }

            if (!_namesToCodes.TryGetValue(locationName, out string code))
            {
                throw new ArgumentException("The given location name is invalid: it does not exist. Make sure it exactly matches a canonical location name.");
            }

            return code;
        }

        public string GetFullName(string locationCode)
        {
            if (locationCode == null)
            {
                throw new ArgumentNullException(nameof(locationCode));
            }

            if (!_codesToNames.TryGetValue(locationCode, out string fullName))
            {
                throw new ArgumentException("The given location code is invalid: it does not exist.");
            }

            return fullName;
        }

        public async Task InitializeAsync(IServiceProvider provider)
        {
            var conf = provider.GetRequiredService<IConfiguration>();
            _codesToNames.Clear();
            _namesToCodes.Clear();
            string path = conf.GetValue<string>("locationCodeMapFile", null);
            if (path == null)
            {
                return;
            }
            string[] lines = await System.IO.File.ReadAllLinesAsync(path).ConfigureAwait(false);
            foreach (var line in lines)
            {
                string[] parts = line.Split(new char[] { ':' }, 2);
                _codesToNames.Add(parts[0], parts[1]);
                _namesToCodes.Add(parts[1], parts[0]);
            }
        }
    }
}
