using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using System.Collections.Generic;
using System.Reflection;
using CyberPatriot.Models.Serialization.ParsingInformation;
using CyberPatriot.DiscordBot.Models;

namespace CyberPatriot.DiscordBot.TypeReaders
{
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
}