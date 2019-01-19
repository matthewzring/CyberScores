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

    public class FileBackedLocationResolutionService : ILocationResolutionService
    {
        protected Dictionary<string, string> _codesToNames = new Dictionary<string, string>();
        protected Dictionary<string, string> _namesToCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        protected Dictionary<string, Uri> _codesToFlags = new Dictionary<string, Uri>();

        public bool IsValidLocation(string locationCode) => locationCode != null && _codesToNames.ContainsKey(locationCode);

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

        public Uri GetFlagUri(string locationCode)
        {
            if (locationCode == null)
            {
                throw new ArgumentNullException(nameof(locationCode));
            }

            if (_codesToFlags.TryGetValue(locationCode, out Uri flagUri))
            {
                return flagUri;
            }

            if (!_codesToNames.ContainsKey(locationCode))
            {
                throw new ArgumentException("The given location code is invalid: it does not exist.");
            }

            return null;
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
            _codesToFlags.Clear();
            string path = conf.GetValue<string>("locationCodeMapFile", null);
            if (path == null)
            {
                return;
            }
            string[] lines = await System.IO.File.ReadAllLinesAsync(path).ConfigureAwait(false);
            foreach (var line in lines)
            {
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }
                string[] parts = line.Split(new char[] { ':' }, 3);
                _codesToNames.Add(parts[0], parts[1]);
                _namesToCodes.Add(parts[1].Trim(), parts[0]);
                if (parts.Length > 2 && Uri.TryCreate(parts[2], UriKind.Absolute, out Uri flagUri))
                {
                    _codesToFlags.Add(parts[0], flagUri);
                }
            }
        }
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
