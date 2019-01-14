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
        [Shorthands("Air Force", "AF", "AFJROTC", PreferredAbbreviation = "AF")]
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
        [Shorthands("naval", "Naval Sea Cadets", "Sea Cadets", "seacadets", PreferredAbbreviation = "Naval")]
        NavalSeaCadets,
        [CanonicalName("Navy JROTC")]
        [Shorthands("Navy", "NJROTC", PreferredAbbreviation = "Navy")]
        Navy
    }
}
