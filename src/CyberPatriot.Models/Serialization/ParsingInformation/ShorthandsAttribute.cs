using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.Models.Serialization.ParsingInformation
{
    [System.AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class ShorthandsAttribute : Attribute
    {
        // See the attribute guidelines at 
        //  http://go.microsoft.com/fwlink/?LinkId=85236
        readonly string[] permittedShorthands;

        // This is a positional argument
        public ShorthandsAttribute(params string[] permittedShorthands)
        {
            this.permittedShorthands = permittedShorthands;

        }

        public string[] PermittedShorthands
        {
            get { return permittedShorthands; }
        }
        
        public string PreferredAbbreviation { get; set; }
    }
}
