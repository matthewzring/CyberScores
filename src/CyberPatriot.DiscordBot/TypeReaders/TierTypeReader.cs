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
    public class TierTypeReader : TypeReader
    {
        protected virtual TypeReaderResult GenerateError() => TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a tier. Valid tiers are 'Platinum,' 'Gold,' 'Silver,' and some supported shorthands.");

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            // prevent tiers being equated to numbers
            if (int.TryParse(input, out int _))
            {
                return Task.FromResult(GenerateError());
            }

            Tier result;
            if (Enum.TryParse(input, true, out result))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(result));
            }
            else if (Utilities.TryParseEnumSpaceless(input, out result))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(result));
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                switch (input.ToLower().Trim())
                {
                    case "plat":
                        result = Tier.Platinum;
                        return Task.FromResult(TypeReaderResult.FromSuccess(result));
                }
            }

            return Task.FromResult(GenerateError());
        }
    }
}
