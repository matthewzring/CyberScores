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

using System;
using System.Threading.Tasks;
using Discord.Commands;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.TypeReaders
{
    public class DivisionTypeReader : TypeReader
    {
        protected virtual TypeReaderResult GenerateError() => TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a division. Valid divisions are 'Open,' 'All Service,' 'Middle School,' and some supported shorthands.");

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) => Task.FromResult(Read(context, input, services));

        public TypeReaderResult Read(ICommandContext context, string input, IServiceProvider services)
        {
            // prevent divisions being equated to numbers
            if(TryParseFriendly(input, out Division div))
            {
                return TypeReaderResult.FromSuccess(div);
            }

            return GenerateError();
        }

        public static bool TryParseFriendly(string input, out Division division)
        {
            if (int.TryParse(input, out int _))
            {
                division = default(Division);
                return false;
            }
            
            if (Enum.TryParse(input, true, out division))
            {
                return true;
            }
            else if (Utilities.TryParseEnumSpaceless(input, out division))
            {
                return true;
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                switch (input.ToLower().Trim())
                {
                    case "ms":
                    case "m.s.":
                        division = Division.MiddleSchool;
                        return true;
                    case "as":
                    case "a.s.":
                    case "service":
                        division = Division.AllService;
                        return true;
                    case "open":
                    case "h.s.":
                    case "hs":
                        division = Division.Open;
                        return true;
                }
            }

            return false;
        }
    }
}
