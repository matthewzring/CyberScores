using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace CyberPatriot.DiscordBot.Services
{
    public class FlagProviderService
    {
        public HttpClient Client { get; } = new HttpClient();

        public IConfiguration Config { get; set; }

        public string GetFlagUri(string locationCode)
        {
            if (locationCode == null)
            {
                throw new ArgumentNullException(nameof(locationCode));
            }

            try
            {
                // assume 3 letter code = country, 2 letter code = state
                string flagUrl = null;
                if (locationCode.Length == 3)
                {
                    // there aren't that many non-US things we need to support
                    switch (locationCode.ToUpperInvariant())
                    {
                        case "CAN":
                            flagUrl = "http://www.geonames.org/flags/x/ca.gif";
                            break;
                        case "AUS":
                            flagUrl = "http://www.geonames.org/flags/x/au.gif";
                            break;
                    }
                }
                else if (locationCode.Length == 2)
                {
                    return "http://usa.flagpedia.net/data/flags/normal/" + locationCode.ToLower() + ".png";
                }

                if (flagUrl != null && flagUrl.ToLowerInvariant().EndsWith(".svg"))
                {
                    // TODO SVG -> PNG
                    return null;
                }

                return flagUrl;

            }
            catch
            {
                return null;
            }
        }
    }
}