using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using CyberPatriot.Models;

namespace CyberPatriot.DiscordBot
{
    public class TeamIdTypeReader : TypeReader
    {
        public const int DefaultSeason = 10;

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
        {
            TeamId result;
            if (TeamId.TryParse(input, out result))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(result));
            }
            else if (input != null && input.Length == 4 && int.TryParse(input, out int teamNumber))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(new TeamId(DefaultSeason, teamNumber)));
            }

            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a team ID."));
        }
    }
}