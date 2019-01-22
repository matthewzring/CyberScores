using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.BitmapProvider;
using System.Linq;
using CyberPatriot.Services.ScoreRetrieval;
using CyberPatriot.Services;

namespace CyberPatriot.DiscordBot
{
    internal class CyberPatriotDiscordBot
    {
        // I don't like big static properties
        public static DateTimeOffset StartupTime { get; private set; }
        public const int RequiredPermissions = 510016;

        static void Main(string[] args)
           => new CyberPatriotDiscordBot().MainAsync().GetAwaiter().GetResult();

        private DiscordSocketClient _client;
        private IConfiguration _config;

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            _config = BuildConfig();

            var services = ConfigureServices();
            services.GetRequiredService<LogService>();
            await Task.WhenAll(
                services.GetRequiredService<CommandHandlingService>().InitializeAsync(services),
                services.GetRequiredService<IDataPersistenceService>().InitializeAsync(services),
                services.GetRequiredService<CyberPatriotEventHandlingService>().InitializeAsync(services),
                services.GetRequiredService<IScoreRetrievalService>().InitializeAsync(services, null),
                services.GetRequiredService<ILocationResolutionService>().InitializeAsync(services),
                services.GetService<AlternateDataBackendProviderService>()?.InitializeAsync(services) ?? Task.CompletedTask,
                services.GetService<IExternalCategoryProviderService>()?.InitializeAsync(services) ?? Task.CompletedTask,
                services.GetService<ScoreboardDownloadService>()?.InitializeAsync(services) ?? Task.CompletedTask
            );

            string enableUpNotificationConfSetting = _config["enableUpNotification"] ?? "false";

            if (bool.TryParse(enableUpNotificationConfSetting, out bool enableUpNotification) && enableUpNotification)
            {
                _client.Ready += async () =>
                {
                    IUser owner = (await _client.GetApplicationInfoAsync().ConfigureAwait(false))?.Owner;
                    var currentTime = DateTimeOffset.UtcNow;
                    TimeZoneInfo ownerTz;
                    if ((ownerTz = await (services.GetService<PreferenceProviderService>()?.GetTimeZoneAsync(user: owner).ConfigureAwait(false))) != null)
                    {
                        currentTime = TimeZoneInfo.ConvertTime(currentTime, ownerTz);
                    }
                    await (await owner?.GetOrCreateDMChannelAsync())?.SendMessageAsync($"[{currentTime.ToString("g")}] Now online!");
                };
            }

            _client.Ready += () =>
            {
                StartupTime = DateTimeOffset.UtcNow;
                return Task.CompletedTask;
            };

            _client.Disconnected += (e) =>
            {
                if (e != null)
                {
                    Console.Error.Write("{Disconnecting due to following error:} ");
                    Console.Error.WriteLine(e.ToString());
                }
                Environment.Exit(e == null ? 0 : 1);
                return Task.CompletedTask;
            };

            await _client.LoginAsync(Discord.TokenType.Bot, _config["token"]);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            var serviceCollection = new ServiceCollection()
                // Base
                .AddSingleton(_client)
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                // Logging
                .AddLogging()
                .AddSingleton<LogService>()
                .AddSingleton<PostLogAsyncHandler>(x => x.GetRequiredService<LogService>().LogApplicationMessageAsync)
                // Extra
                .AddSingleton(_config)
                .AddSingleton<IDataPersistenceService, LiteDbDataPersistenceService>()
                .AddSingleton<PreferenceProviderService>()
                .AddSingleton<IGraphProviderService, BitmapProvider.ImageSharp.ImageSharpGraphProviderService>()
                .AddSingleton<IRateLimitProvider, TimerRateLimitProvider>(prov => new PriorityTimerRateLimitProvider(2000, 1))
                // CyPat
                // Scoreboard trial order: live, JSON archive, CSV released archive
                .AddSingleton<IScoreRetrievalService, FallbackScoreRetrievalService>(prov => new FallbackScoreRetrievalService(
                    prov,
                    _ => { },
                    _config.GetSection("backends").AsEnumerable(true).Where(x => int.TryParse(x.Key, out int _)).OrderBy(x => int.Parse(x.Key)).Select(x =>
                    {
                        var confSection = _config.GetSection("backends:" + x.Key);
                        return AlternateDataBackendProviderService.ScoreBackendInitializerProvidersByName[confSection["type"]](confSection);
                    }).ToArray()
                ))
                .AddSingleton<ICompetitionRoundLogicService, CyberPatriotElevenCompetitionRoundLogicService>()
                .AddSingleton<IExternalCategoryProviderService, FileBackedCategoryProviderService>()
                .AddSingleton<ILocationResolutionService>(prov => prov.GetRequiredService<IConfiguration>().GetValue<string>("locationCodeMapFile", null) != null ?
                    (ILocationResolutionService)new FileBackedLocationResolutionService() :
                    (ILocationResolutionService)new NullIdentityLocationResolutionService())
                .AddSingleton<ScoreboardDownloadService>()
                .AddSingleton<CyberPatriotEventHandlingService>()
                .AddSingleton<AlternateDataBackendProviderService>()
                .AddTransient<ScoreboardMessageBuilderService>();

            var servicesList = serviceCollection.ToIList();
            serviceCollection.AddSingleton<System.Collections.Generic.IList<ServiceDescriptor>>(_ => servicesList);

            return serviceCollection.BuildServiceProvider();
        }

        private IConfiguration BuildConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("config.json")
                .Build();
        }
    }
}
