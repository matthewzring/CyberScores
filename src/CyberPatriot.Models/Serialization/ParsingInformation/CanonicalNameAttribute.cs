using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.Models.Serialization.ParsingInformation
{
    [System.AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class CanonicalNameAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly string canonicalName;

        // This is a positional argument
        public CanonicalNameAttribute(string canonicalName)
        {
            this.canonicalName = canonicalName;
        }

        public string CanonicalName
        {
            get { return canonicalName; }
        }
    }
}
