using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot
{
    public class LocationTypeReader : TypeReader
    {
        protected string ParseError { get; set; } = "Could not parse the input as a location code. Must be two or three capital letters.";

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
            => Task.FromResult(ReadSync(context, input, services));

        public TypeReaderResult ReadSync(ICommandContext context, string input, IServiceProvider services)
        {
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
            return TypeReaderResult.FromSuccess((LocationCode)input);
        }
    }
}