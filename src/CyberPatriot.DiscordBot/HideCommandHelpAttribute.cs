using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot
{
    /// <summary>
    /// A passthrough precondition. Used to hide a command or overload in the help screen.
    /// </summary>
    public class HideCommandHelpAttribute : PreconditionAttribute
    {
        private static readonly Task<PreconditionResult> Result = Task.FromResult(PreconditionResult.FromSuccess());
        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, CommandInfo command, IServiceProvider services) => Result;
    }
}
