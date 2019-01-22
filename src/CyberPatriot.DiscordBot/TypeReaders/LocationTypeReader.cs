using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;
using CyberPatriot.DiscordBot.Services;
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