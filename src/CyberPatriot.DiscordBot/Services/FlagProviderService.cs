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
                    // there aren't that many international things we need to support, we can just hardcode it
                    switch (locationCode.ToUpperInvariant())
                    {
                        case "CAN":
                            // Canada
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cf/Flag_of_Canada.svg/320px-Flag_of_Canada.svg.png";
                            break;
                        case "AUS":
                            // Australia
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b9/Flag_of_Australia.svg/320px-Flag_of_Australia.svg.png";
                            break;
                        case "ARE":
                            // United Arab Emirates
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/Flag_of_the_United_Arab_Emirates.svg/320px-Flag_of_the_United_Arab_Emirates.svg.png";
                            break;
                        case "DEU":
                            // Germany
                            flagUrl = "https://upload.wikimedia.org/wikipedia/en/thumb/b/ba/Flag_of_Germany.svg/320px-Flag_of_Germany.svg.png";
                            break;
                        case "HUN":
                            // Hungary
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c1/Flag_of_Hungary.svg/320px-Flag_of_Hungary.svg.png";
                            break;
                        case "ROU":
                            // Romania
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/7/73/Flag_of_Romania.svg/320px-Flag_of_Romania.svg.png";
                            break;
                        case "EST":
                            // Estonia
                            flagUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8f/Flag_of_Estonia.svg/320px-Flag_of_Estonia.svg.png";
                            break;
                    }
                }
                else if (locationCode.Length == 2)
                {
                    // there are a few two-letter non-state postal codes, e.g. DC and the Armed Forces abroad mail codes
                    switch (locationCode.ToUpperInvariant())
                    {
                        case "DC":
                        case "AA":
                        case "AE":
                        case "AP":
                            // TODO distinct flags?
                            flagUrl = null;
                            break;
                        default:
                            flagUrl = "http://usa.flagpedia.net/data/flags/normal/" + locationCode.ToLower() + ".png";
                            break;
                    }
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