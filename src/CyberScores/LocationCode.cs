#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

namespace CyberPatriot.DiscordBot;

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
