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

using CyberPatriot.Models.Serialization;
using CyberPatriot.Models.Serialization.ParsingInformation;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.Models
{
    [Newtonsoft.Json.JsonConverter(typeof(ServiceCategoryJsonConverter))]
    public enum ServiceCategory
    {
        [CanonicalName("Air Force JROTC")]
        [Shorthands("Air Force", "airforce", "AF", "AFJROTC", PreferredAbbreviation = "AF")]
        AirForce,
        [CanonicalName("Army JROTC")]
        [Shorthands("Army", PreferredAbbreviation = "Army")]
        Army,
        [CanonicalName("Civil Air Patrol")]
        [Shorthands("CAP", PreferredAbbreviation = "CAP")]
        CivilAirPatrol,
        [CanonicalName("Marine Corps JROTC")]
        [Shorthands("MCJROTC", "Marines", "Marine Corps", "marinecorps", PreferredAbbreviation = "Marines")]
        MarineCorps,
        [CanonicalName("Naval Sea Cadet Corps")]
        [Shorthands("naval", "Naval Sea Cadets", "navalseacadets", "Sea Cadets", "seacadets", "seacadetcorps", PreferredAbbreviation = "Naval")]
        NavalSeaCadets,
        [CanonicalName("Navy JROTC")]
        [Shorthands("Navy", "NJROTC", PreferredAbbreviation = "Navy")]
        Navy
    }
}
