using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.DiscordBot.Models;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.Modules
{
    [Group("user")]
    public class UserPreferenceCommandModule : ModuleBase
    {
        [Group("timezone"), Alias("tz")]
        public class TimezoneModule : ModuleBase
        {
            public PreferenceProviderService PreferenceService { get; set; }

            [Command("set")]
            [Summary("Sets your default timezone.")]
            public async Task SetTimezoneAsync([Summary("The timezone in which times will be displayed by default.")] string newTimezone)
            {
                TimeZoneInfo newTz;
                try
                {
                    if ((newTz = TimeZoneInfo.FindSystemTimeZoneById(newTimezone)) == null)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    // FIXME inconsistent timezone IDs between platforms -_-
                    string tzType = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? "Windows" : "IANA";
                    await ReplyAsync($"That timezone is not recognized. Please make sure you are passing a valid {tzType} timezone identifier.").ConfigureAwait(false);
                    return;
                }
                await PreferenceService.SetTimeZoneAsync(Context.User, newTz).ConfigureAwait(false);
                await ReplyAsync($"Updated personal timezone preference to {TimeZoneNames.TZNames.GetNamesForTimeZone(newTimezone, "en-US").Generic}.").ConfigureAwait(false);
            }

            [Command("remove"), Alias("delete", "unset")]
            [Summary("Removes your designated timezone, reverting the default to individual guild preferences, or UTC if none specified.")]
            public async Task RemoveTimezone()
            {
                await PreferenceService.SetTimeZoneAsync(Context.User, null).ConfigureAwait(false);
                await ReplyAsync("Removed personal timezone preference.").ConfigureAwait(false);
            }
        }
    }
}