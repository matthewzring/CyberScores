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
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot.TypeReaders
{
    public class ServiceCategoryTypeReader : TypeReader
    {
        protected TypeReaderResult ErrorReturn { get; } = TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as an all service branch or category.");

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) => Task.FromResult(Read(context, input, services));
        public TypeReaderResult Read(ICommandContext context, string input, IServiceProvider services)
        {
            // prevent categories being equated to numbers
            if (int.TryParse(input, out int _))
            {
                return ErrorReturn;
            }

            ServiceCategory result;
            if (Enum.TryParse(input, true, out result) || CyberPatriot.Models.Serialization.ServiceCategoryExtensions.TryParseAliasName(input.Trim(), out result))
            {
                return TypeReaderResult.FromSuccess(result);
            }

            return ErrorReturn;
        }
    }
}
