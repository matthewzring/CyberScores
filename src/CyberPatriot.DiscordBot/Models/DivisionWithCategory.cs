using CyberPatriot.Models;
using CyberPatriot.Models.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.DiscordBot.Models
{
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
}
