using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot
{
    public class TierTypeReader : TypeReader
    {
        protected virtual TypeReaderResult GenerateError() => TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a tier. Valid tiers are 'Platinum,' 'Gold,' 'Silver,' and some supported shorthands.");

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
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