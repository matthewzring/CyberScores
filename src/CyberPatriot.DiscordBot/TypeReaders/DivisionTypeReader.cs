using System;
using System.Threading.Tasks;
using Discord;
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