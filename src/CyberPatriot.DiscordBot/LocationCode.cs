using System;

namespace CyberPatriot.DiscordBot
{
    /// <summary>
    /// This class is a NASTY WORKAROUND - see issue #43.
    /// Represents an UNVALIDATED location code; the type reader handles validation.
    /// </summary>
    public struct LocationCode
    {
        public string Value { get; }

        public LocationCode(string val)
        {
            Value = val;
        }

        public override bool Equals(object obj)
        {
            return obj is LocationCode other && Equals(other);
        }

        public bool Equals(LocationCode other) => Value == other.Value;

        public override int GetHashCode()
        {
            return Value?.GetHashCode() ?? 0;
        }

        public static bool operator ==(LocationCode a, LocationCode b) => a.Value == b.Value;
        public static bool operator !=(LocationCode a, LocationCode b) => a.Value != b.Value;
        public static implicit operator string(LocationCode loc) => loc.Value;
        public static explicit operator LocationCode(string locVal) => new LocationCode(locVal);
    }
}