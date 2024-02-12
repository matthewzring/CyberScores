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
using System.Reflection;
using CyberPatriot.Models.Serialization.ParsingInformation;

namespace CyberPatriot.Models.Serialization
{
    public static class ServiceCategoryExtensions
    {
        private static readonly IDictionary<string, ServiceCategory> CategoriesByCanonicalNames = new Dictionary<string, ServiceCategory>(StringComparer.InvariantCultureIgnoreCase);
        private static readonly IDictionary<string, ServiceCategory> CategoriesByCanonicalNamesCaseSensitive = new Dictionary<string, ServiceCategory>(StringComparer.InvariantCulture);
        private static readonly IDictionary<string, ServiceCategory> CategoriesByAliases = new Dictionary<string, ServiceCategory>(StringComparer.InvariantCultureIgnoreCase);
        private static readonly IDictionary<string, ServiceCategory> CategoriesByAliasesCaseSensitive = new Dictionary<string, ServiceCategory>(StringComparer.InvariantCulture);
        private static readonly IDictionary<ServiceCategory, string> CanonicalNamesByCategory = new Dictionary<ServiceCategory, string>();
        private static readonly IDictionary<ServiceCategory, string> PreferredAbbreviationsByCategory = new Dictionary<ServiceCategory, string>();

        static ServiceCategoryExtensions()
        {
            var type = typeof(ServiceCategory);
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                ServiceCategory value = (ServiceCategory)field.GetValue(null);
                string canonicalName = field.GetCustomAttribute<CanonicalNameAttribute>().CanonicalName;
                CategoriesByCanonicalNames.Add(canonicalName, value);
                CategoriesByCanonicalNamesCaseSensitive.Add(canonicalName, value);
                CanonicalNamesByCategory.Add(value, canonicalName);

                var shorthands = field.GetCustomAttribute<ShorthandsAttribute>();
                if (shorthands != null)
                {
                    foreach (var noncanonicalname in shorthands.PermittedShorthands)
                    {
                        CategoriesByAliases.Add(noncanonicalname, value);
                        CategoriesByAliasesCaseSensitive.Add(noncanonicalname, value);
                    }
                    PreferredAbbreviationsByCategory.Add(value, shorthands.PreferredAbbreviation ?? shorthands.PermittedShorthands[0]);
                }
                else
                {
                    // fallback for abbreviation
                    PreferredAbbreviationsByCategory.Add(value, canonicalName.Replace(" ", ""));
                }
            }
        }

        public static ServiceCategory ParseCanonicalName(string canonicalName, bool caseSensitive = true) 
            => (caseSensitive ? CategoriesByCanonicalNamesCaseSensitive : CategoriesByCanonicalNames)[canonicalName ?? throw new ArgumentNullException(nameof(canonicalName))];

        public static bool TryParseCanonicalName(string canonicalName, out ServiceCategory value, bool caseSensitive = true)
        {
            if (canonicalName == null)
            {
                value = default(ServiceCategory);
                return false;
            }

            return (caseSensitive ? CategoriesByCanonicalNamesCaseSensitive : CategoriesByCanonicalNames).TryGetValue(canonicalName, out value);
        }

        public static ServiceCategory ParseAliasName(string name, bool caseSensitive = false)
        {
            if(name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!TryParseAliasName(name, out ServiceCategory value, caseSensitive: caseSensitive))
            {
                throw new ArgumentException("The given name is not a valid category.");
            }
            return value;
        }

        public static bool TryParseAliasName(string name, out ServiceCategory value, bool caseSensitive = false)
        {
            if (name == null)
            {
                value = default(ServiceCategory);
                return false;
            }

            if ((caseSensitive ? CategoriesByCanonicalNamesCaseSensitive : CategoriesByCanonicalNames).TryGetValue(name, out value))
            {
                return true;
            }
            if ((caseSensitive ? CategoriesByAliasesCaseSensitive : CategoriesByAliases).TryGetValue(name, out value))
            {
                return true;
            }
            return false;
        }

        public static string ToCanonicalName(this ServiceCategory value) => CanonicalNamesByCategory[value];
        public static string Abbreviate(this ServiceCategory value) => PreferredAbbreviationsByCategory[value];
    }
}
