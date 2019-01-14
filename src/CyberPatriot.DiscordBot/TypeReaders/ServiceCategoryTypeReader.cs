using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using System.Collections.Generic;
using System.Reflection;
using CyberPatriot.Models.Serialization.ParsingInformation;

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