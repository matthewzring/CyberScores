using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.DiscordBot
{
    /// <summary>
    /// A passthrough precondition. Used to indicate a mandatory parameter as optional and potentially subordinate to another parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
    public class AlterParameterDisplayAttribute : ParameterPreconditionAttribute
    {
        public string SubordinateTo { get; set; }
        /// <summary>
        /// Gets or sets a property indicating whether the native parameter's status will be overridden to display it as manual.
        /// </summary>
        public bool DisplayAsOptional { get; set; }
        /// <summary>
        /// Gets or sets a property indicating whether the native parameter's status will be overridden to display it as mandatory.
        /// </summary>
        public bool DisplayAsMandatory { get; set; }
        
        private static readonly Task<PreconditionResult> Result = Task.FromResult(PreconditionResult.FromSuccess());
        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services) => Result;
    }
}
