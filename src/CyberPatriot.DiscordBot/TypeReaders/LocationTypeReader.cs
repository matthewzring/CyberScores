#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

using System;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using CyberPatriot.Services;

namespace CyberPatriot.DiscordBot.TypeReaders
{
    public class LocationTypeReader : TypeReader
    {
        protected string ParseError { get; set; } = "Could not parse the input as a location code. Must be two or three capital letters.";
        protected string InvalidLocationError { get; set; } = "No location corresponding to the given location code exists.";

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
            => Task.FromResult(ReadSync(context, input, services));

        public TypeReaderResult ReadSync(ICommandContext context, string input, IServiceProvider services)
        {
            if (input == null)
            {
                return TypeReaderResult.FromError(CommandError.ParseFailed, ParseError);
            }

            input = input.Trim();
            if (input.Length != 2 && input.Length != 3)
            {
                return TypeReaderResult.FromError(CommandError.ParseFailed, ParseError);
            }
            foreach (char c in input)
            {
                if (!(char.IsUpper(c) && char.IsLetter(c)))
                {
                    return TypeReaderResult.FromError(CommandError.ParseFailed, ParseError);
                }
            }

            var locationResolver = services.GetService<ILocationResolutionService>();
            if (locationResolver != null && !locationResolver.IsValidLocation(input))
            {
                return TypeReaderResult.FromError(CommandError.ObjectNotFound, InvalidLocationError);
            }

            return TypeReaderResult.FromSuccess((LocationCode)input);
        }
    }
}
