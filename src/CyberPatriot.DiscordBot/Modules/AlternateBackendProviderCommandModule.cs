#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using CyberPatriot.DiscordBot.Services;
using CyberPatriot.Services;
using Discord.Commands;
using System;
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
