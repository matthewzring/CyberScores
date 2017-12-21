using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LiteDB;
using CyberPatriot.DiscordBot.Services;

namespace CyberPatriot.DiscordBot
{
    class Program
    {
        static void Main(string[] args)
           => new Program().MainAsync().GetAwaiter().GetResult();

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
                services.GetRequiredService<IScoreRetrievalService>().InitializeAsync(services)
            );

            await _client.LoginAsync(Discord.TokenType.Bot, _config["token"]);
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private IServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                // Base
                .AddSingleton(_client)
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandlingService>()
                // Logging
                .AddLogging()
                .AddSingleton<LogService>()
                // Extra
                .AddSingleton(_config)
                .AddSingleton<IDataPersistenceService, LiteDbDataPersistenceService>(prov => new LiteDbDataPersistenceService(new LiteDatabase(_config["databaseFilename"])))
                // CyPat
                //.AddSingleton<IScoreRetrievalService, CachingScoreRetrievalService>(prov => new CachingScoreRetrievalService(new HttpScoreboardScoreRetrievalService(_config["defaultScoreboardHostname"])))
                .AddSingleton<IScoreRetrievalService, SpreadsheetScoreRetrievalService>()
                .AddSingleton<FlagProviderService>()
                .AddSingleton<CyberPatriotEventHandlingService>()
                .AddSingleton<ScoreboardMessageBuilderService>()
                .BuildServiceProvider();
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
