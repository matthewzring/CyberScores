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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace CyberPatriot.DiscordBot
{
    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = false, AllowMultiple = true)]
    public class InclusiveRangeAttribute : ParameterPreconditionAttribute
    {
        readonly double lowerBound = double.NegativeInfinity;
        readonly double upperBound = double.PositiveInfinity;

        public InclusiveRangeAttribute(double upper)
        {
            upperBound = upper;
        }

        public InclusiveRangeAttribute(double lower, double upper)
        {
            lowerBound = lower;
            upperBound = upper;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
        {
            double compareVal;
            if (value is double d)
            {
                compareVal = d;
            }
            else if (value is float f)
            {
                compareVal = f;
            }
            else if (value is decimal de)
            {
                compareVal = (double)de;
            }
            else if (value is IConvertible c)
            {
                compareVal = c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                try
                {
                    compareVal = (double)value;
                }
                catch (Exception e)
                {
                    return Task.FromException<PreconditionResult>(e);
                }
            }
            if (lowerBound <= compareVal && compareVal <= upperBound)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError($"The given value was outside of the range of valid values ({lowerBound} to {upperBound} inclusive)."));
        }
    }
}