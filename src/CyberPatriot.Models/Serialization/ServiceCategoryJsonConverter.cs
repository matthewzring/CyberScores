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

using Newtonsoft.Json;
using System;

namespace CyberPatriot.Models.Serialization
{
    public class ServiceCategoryJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue((int)((ServiceCategory)value));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (objectType == typeof(ServiceCategory?) && (reader.Value == null || string.Equals("null", reader.Value)))
            {
                return null;
            }
            else if (reader.Value is string val)
            {
                if (Enum.TryParse(val, out ServiceCategory retVal))
                {
                    return retVal;
                }
                else if (ServiceCategoryExtensions.TryParseCanonicalName(val, out retVal))
                {
                    return retVal;
                }
            }
            else if (reader.Value is Int64 l)
            {
                return (ServiceCategory)l;
            }
            else if (reader.Value is Int32 i)
            {
                return (ServiceCategory)i;
            }
            else if (reader.Value is Int16 s)
            {
                return (ServiceCategory)s;
            }

            throw new JsonReaderException("Error parsing ServiceCategory.");
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(ServiceCategory).IsAssignableFrom(objectType) || typeof(ServiceCategory?).IsAssignableFrom(objectType);
        }
    }
}
