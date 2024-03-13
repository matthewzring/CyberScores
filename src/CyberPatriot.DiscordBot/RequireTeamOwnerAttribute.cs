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

using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot;

/// <summary>
/// As a precondition, require that the command is invoked by the bot owner, or the owner of the bot's team if the bot is team-owned.
/// Based heavily on RequireOwnerAttribute source code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireTeamOwnerAttribute : PreconditionAttribute
{
    /// <inheritdoc />
    public override string ErrorMessage { get; set; } = "Command can only be run by the owner of the bot.";

    /// <inheritdoc />
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        switch (context.Client.TokenType)
        {
            case TokenType.Bot:
                var application = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

                // user-owned bots have a simple check
                if (application.Team == null)
                    return context.User.Id == application.Owner.Id ? PreconditionResult.FromSuccess() : PreconditionResult.FromError(ErrorMessage);

                // team-owned bots: require team owner
                // in the future, perhaps add flags to control what team permission is required
                return context.User.Id == application.Team.OwnerUserId ? PreconditionResult.FromSuccess() : PreconditionResult.FromError(ErrorMessage);
            default:
                return PreconditionResult.FromError($"{nameof(RequireTeamOwnerAttribute)} is not supported by this {nameof(TokenType)}.");
        }
    }
}
