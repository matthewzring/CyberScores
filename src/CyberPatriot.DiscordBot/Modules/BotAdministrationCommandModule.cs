using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using CyberPatriot.DiscordBot.Services;

namespace CyberPatriot.DiscordBot.Modules
{
    public class BotAdministrationCommandModule : ModuleBase
    {
        public IScoreRetrievalService ScoreService { get; set; }
        public IDataPersistenceService Database { get; set; }
        
        [Command("ping"), Summary("Pings the bot. Responds with the internal socket client's estimated latency, if available.")]
        public Task PingAsync() => ReplyAsync("Pong!" + (Context.Client is Discord.WebSocket.DiscordSocketClient socketClient ? " " + socketClient.Latency + "ms" : string.Empty));

        [Command("info"), Alias("about"), Summary("Returns information about the bot.")]
        public async Task InfoAsync()
        {
            var appinfo = (await Context.Client.GetApplicationInfoAsync());
            await ReplyAsync(string.Empty, embed: await new EmbedBuilder()
                .WithAuthor(Context.Client.CurrentUser.Username, Context.Client.CurrentUser.GetAvatarUrlOrDefault())
                .WithDescription("**Purpose:** A bot for interaction with CyberPatriot scoreboards.\n"
                + $"**Code:** [On GitHub](https://github.com/glen3b/CyPatScoreboardBot) - [Add me to your server!](https://discordapp.com/oauth2/authorize?client_id={appinfo.Id}&permissions={CyberPatriotDiscordBot.RequiredPermissions}&scope=bot)\n"
                + "**Disclaimer:** This bot is not affiliated with the Air Force Association nor the CyberPatriot program. All scores displayed, even those marked \"official,\" are non-binding unofficial scores and should be treated as such. Official scores can only be found [on the CyberPatriot website](http://www.uscyberpatriot.org/competition/current-competition/scores).")
                .WithFooter("Made by glen3b | Written in C# using Discord.Net", "https://avatars.githubusercontent.com/glen3b")
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Prefix").WithValue((Context.Guild != null ? (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id))?.Prefix?.AppendPrepend("`") : null) ?? Context.Client.CurrentUser.Mention))
                .AddField(fb => fb.WithIsInline(true).WithName("Score Provider").WithValue(ScoreService.StaticSummaryLine))
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Teams").WithValue((await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter)).TeamList.Count))
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Guilds").WithValue((await Context.Client.GetGuildsAsync()).Count))
                .AddField(fb => fb.WithIsInline(true).WithName("Uptime").WithValue(string.Join("\n",
                    (DateTimeOffset.UtcNow - CyberPatriotDiscordBot.StartupTime).ToLongString()
                    .Split(' ')
                    .Select((v, i) => new {Value = v, Index = i}) 
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(y => y.Value)).Select(x => string.Join(" ", x)))))
                .AddFieldAsync(async fb => fb.WithIsInline(true).WithName("Users").WithValue(await Context.Client.GetGuildsAsync().TaskToAsyncEnumerable<IGuild, IReadOnlyCollection<IGuild>>().SumParallelAsync(async g => (await g.GetUsersAsync()).Count)))
                .BuildAsync());
        }

        [Command("kill"), Alias("die"), RequireOwner]
        [Summary("Terminates the bot instance.")]
        public Task KillAsync()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }

        [Command("setavatar"), Alias("seticon"), RequireOwner]
        [Summary("Sets the avatar for the bot.")]
        public async Task SetIconAsync(string iconUrl)
        {
            var iconDownloader = new System.Net.Http.HttpClient();
            string tempFileTargetName = Path.GetTempFileName();
            using (var iconStream = await iconDownloader.GetStreamAsync(iconUrl))
            using (var tempFileTarget = File.OpenWrite(tempFileTargetName))
            {
                await iconStream.CopyToAsync(tempFileTarget);
            }
            await Context.Client.CurrentUser.ModifyAsync(props => props.Avatar = new Image(tempFileTargetName));
            File.Delete(tempFileTargetName);
            await ReplyAsync("Avatar updated!");
        }

        [Command("exportscoreboard"), Alias("savescoreboard", "exportscoreboardjson", "downloadscoreboard")]
        [RequireOwner]
        [Summary("Exports a GZip-compressed JSON scoreboard from the current backend to the current channel.")]
        public async Task DownloadScoreboardAsync()
        {
            await ReplyAsync("Downloading scoreboard...");

            using (Context.Channel.EnterTypingState())
            {
                var scoreSummary = await ScoreService.GetScoreboardAsync(ScoreboardFilterInfo.NoFilter);
                IDictionary<TeamId, ScoreboardDetails> teamDetails =
                (await Task.WhenAll(scoreSummary.TeamList.Select(team => ScoreService.GetDetailsAsync(team.TeamId)))
                ).ToDictionary(entry => entry.TeamId);
                MemoryStream rawWriteStream = null;
                try
                {
                    rawWriteStream = new MemoryStream();

                    // need to close to get GZ tail, but this also closes underlying stream...
                    using (var writeStream = new GZipStream(rawWriteStream, CompressionMode.Compress))
                    {
                        await JsonScoreRetrievalService.SerializeAsync(new StreamWriter(writeStream), scoreSummary,
                            teamDetails, ScoreService.Round);
                    }
                    rawWriteStream = new MemoryStream(rawWriteStream.ToArray());

                    string fileName = $"scoreboard-{scoreSummary.SnapshotTimestamp.ToUnixTimeSeconds()}.json.gz";
                    if (Directory.Exists("scoreboardarchives"))
                    {
                        using (var fileStream = File.Create(Path.Combine("scoreboardarchives", fileName)))
                        {
                            await rawWriteStream.CopyToAsync(fileStream);
                        }
                    }
                    rawWriteStream.Position = 0;
                    string tzId = (await Database.FindOneAsync<Models.Guild>(g => g.Id == Context.Guild.Id))?.TimeZone;
                    TimeZoneInfo tz = null;
                    if (tzId != null)
                    {
                        tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                    }
                    DateTimeOffset timestamp = tz == null
                        ? scoreSummary.SnapshotTimestamp
                        : TimeZoneInfo.ConvertTime(scoreSummary.SnapshotTimestamp, tz);

                    await Context.Channel.SendFileAsync(rawWriteStream, fileName,
                        $"JSON scoreboard snapshot for {timestamp:g} {tz?.GetAbbreviations().Generic ?? "UTC"}");
                }
                finally
                {
                    rawWriteStream?.Dispose();
                }
            }
        }
    }
}