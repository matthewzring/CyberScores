using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot
{
    public class DivisionTypeReader : TypeReader
    {
        protected virtual TypeReaderResult GenerateError() => TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a division. Valid divisions are 'Open,' 'All Service,' 'Middle School,' and some supported shorthands.");

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            // prevent divisions being equated to numbers
            if (int.TryParse(input, out int _))
            {
                return Task.FromResult(GenerateError());
            }

            Division result;
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
                    case "ms":
                    case "m.s.":
                        result = Division.MiddleSchool;
                        return Task.FromResult(TypeReaderResult.FromSuccess(result));
                    case "as":
                    case "a.s.":
                    case "service":
                        result = Division.AllService;
                        return Task.FromResult(TypeReaderResult.FromSuccess(result));
                    case "open":
                    case "h.s.":
                    case "hs":
                        result = Division.Open;
                        return Task.FromResult(TypeReaderResult.FromSuccess(result));

                }
            }

            return Task.FromResult(GenerateError());
        }
    }
}