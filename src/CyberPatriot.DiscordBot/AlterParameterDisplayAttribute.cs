#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

using Discord.Commands;
using System;
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
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services) => Result;
    }
}
