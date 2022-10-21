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
