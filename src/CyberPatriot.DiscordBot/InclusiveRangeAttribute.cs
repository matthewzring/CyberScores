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

        public override Task<PreconditionResult> CheckPermissions(ICommandContext context, ParameterInfo parameter, object value, IServiceProvider services)
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