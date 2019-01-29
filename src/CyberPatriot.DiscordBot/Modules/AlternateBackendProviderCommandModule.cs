using CyberPatriot.DiscordBot.Models;
using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Models;
using CyberPatriot.Services;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot.Modules
{
    public class AlternateBackendProviderCommandModule : ModuleBase
    {
        public CommandService CommandService { get; set; }
        public AlternateDataBackendProviderService AlternateBackendProvider { get; set; }
        public IServiceProvider Services { get; set; }

        [Command("rounddata")]
        [Alias("backend", "datasource", "archive")]
        [Summary("Executes the given command using scores provided by the given data source. The available data sources can be seen via the `listdatasources` command.")]
        public async Task ExecuteWrappedCommandAsync(
            [Summary("The identifier of the data source from which scores should be provided to the command.")] string dataSourceId,
            [Remainder, Summary("The command (without any prefix) that should be executed.")] string command)
        {
            if (!AlternateBackendProvider.TryGetAlternateDataBackend(dataSourceId, out IScoreRetrievalService newBackend))
            {
                throw new ArgumentException("The given alternate data source ID is invalid. Valid data sources can be listed with the `listdatasources` command.");
            }

            IResult result = await CommandService.ExecuteAsync(Context, command, Services.Overlay(newBackend)).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                throw new Exception("Error executing datasource-wrapped command: " + (result.Error?.ToStringCamelCaseToSpace() ?? "") + ": " + (result.ErrorReason ?? "[Unknown Details]"));
            }
        }

        [Command("listdatasources"), Alias("listalternatedatasources", "getdatasources", "getalternatedatasources", "listarchives")]
        [Summary("Lists the names of the available loaded alternate data sources.")]
        public async Task ListDataSourcesCommandAsync()
        {
            var messageBuilder = new StringBuilder();
            int count = 0;
            foreach (var dataSource in AlternateBackendProvider.GetBackendNames())
            {
                count++;
                messageBuilder.AppendLine(string.Join(" / ", dataSource));
            }

            messageBuilder.AppendLine();
            messageBuilder.Append(Utilities.Pluralize("alternate data source", count)).AppendLine(" loaded");

            await ReplyAsync(messageBuilder.ToString()).ConfigureAwait(false);
        }
    }
}
