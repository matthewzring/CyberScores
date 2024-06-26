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
using CyberScores.Models;

namespace CyberScores.TypeReaders;

public class DivisionWithCategoryTypeReader : TypeReader
{
    protected TypeReaderResult ErrorReturn { get; } = TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a division or a division with category. Allowed divisions are 'Open,' 'All Service,' and 'Middle School,' along with accepted abbreviations. An abbreviated All Service division may be followed by a colon and a service category specification.");

    public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services) => Task.FromResult(Read(context, input, services));
    public TypeReaderResult Read(ICommandContext context, string input, IServiceProvider services)
    {
        DivisionWithCategory result;
        if (DivisionWithCategory.TryParse(input, out result) && (!result.Category.HasValue || result.Division == Division.AllService))
        {
            return TypeReaderResult.FromSuccess(result);
        }

        return ErrorReturn;
    }
}
