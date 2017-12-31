using System;
using Discord.Commands;
using System.Threading.Tasks;
using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;

namespace CyberPatriot.DiscordBot
{

    public class TeamIdTypeReader : TypeReader
    {

        public override Task<TypeReaderResult> Read(ICommandContext context, string input, IServiceProvider services)
        {
            TeamId result;
            if (TeamId.TryParse(input, out result))
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(result));
            }
            else if (input != null && input.Length == 4 && int.TryParse(input, out int teamNumber) && teamNumber >= 0)
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(new TeamId(TeamIdTypeConverter.DefaultSeason, teamNumber)));
            }

            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed as a team ID."));
        }
    }
}