using CyberPatriot.Models.Serialization.ParsingInformation;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.Models
{
    public enum ServiceCategory
    {
        [CanonicalName("Air Force JROTC")]
        [Shorthands("Air Force", "AF", "AFJROTC")]
        AirForce,
        [CanonicalName("Army JROTC")]
        [Shorthands("Army")]
        Army,
        [CanonicalName("Civil Air Patrol")]
        [Shorthands("CAP")]
        CivilAirPatrol,
        [CanonicalName("Marine Corps JROTC")]
        [Shorthands("MCJROTC", "Marines", "Marine Corps", "marinecorps")]
        MarineCorps,
        [CanonicalName("Naval Sea Cadet Corps")]
        [Shorthands("naval", "Naval Sea Cadets", "Sea Cadets", "seacadets")]
        NavalSeaCadets,
        [CanonicalName("Navy JROTC")]
        [Shorthands("Navy", "NJROTC")]
        Navy
    }
}
