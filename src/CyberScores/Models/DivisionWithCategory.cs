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

using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;
using System;
using System.Collections.Generic;

namespace CyberPatriot.DiscordBot.Models;

/// <summary>
/// Represents a division and optionally a category within that division.
/// Intended to be used to concisely specify filters.
/// </summary>
public struct DivisionWithCategory : IEquatable<DivisionWithCategory>
{
    public Division Division { get; private set; }
    public ServiceCategory? Category { get; private set; }

    public DivisionWithCategory(Division div, ServiceCategory? cat)
    {
        Division = div;
        Category = cat;
    }

    public override bool Equals(object obj)
    {
        return obj is DivisionWithCategory && Equals((DivisionWithCategory)obj);
    }

    public bool Equals(DivisionWithCategory other)
    {
        return Division == other.Division &&
               EqualityComparer<ServiceCategory?>.Default.Equals(Category, other.Category);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Division, Category);
    }

    public static bool operator ==(DivisionWithCategory category1, DivisionWithCategory category2)
    {
        return category1.Equals(category2);
    }

    public static bool operator !=(DivisionWithCategory category1, DivisionWithCategory category2)
    {
        return !(category1 == category2);
    }

    public override string ToString()
    {
        if (!Category.HasValue)
        {
            return Division.ToConciseString();
        }
        else
        {
            string divPrefix;
            switch (Division)
            {
                case Division.Open:
                    divPrefix = "Open:";
                    break;
                case Division.AllService:
                    divPrefix = "AS:";
                    break;
                case Division.MiddleSchool:
                    divPrefix = "MS:";
                    break;
                default:
                    divPrefix = Division.ToString() + ":";
                    break;
            }

            return divPrefix + Category.Value.Abbreviate();
        }
    }

    public static bool TryParse(string value, out DivisionWithCategory divCat)
    {
        // null strings don't parse
        if (value == null)
        {
            divCat = default(DivisionWithCategory);
            return false;
        }

        // just division
        if (TypeReaders.DivisionTypeReader.TryParseFriendly(value, out Division div))
        {
            divCat = new DivisionWithCategory(div, null);
            return true;
        }

        // division and category, colon-separated
        string[] components = value.Split(':');
        if (components.Length != 2)
        {
            divCat = default(DivisionWithCategory);
            return false;
        }

        if (TypeReaders.DivisionTypeReader.TryParseFriendly(components[0], out div) && ServiceCategoryExtensions.TryParseAliasName(components[1], out ServiceCategory cat))
        {
            divCat = new DivisionWithCategory(div, cat);
            return true;
        }

        divCat = default(DivisionWithCategory);
        return false;
    }
}
