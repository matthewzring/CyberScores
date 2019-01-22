using CyberPatriot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Services
{
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

}
