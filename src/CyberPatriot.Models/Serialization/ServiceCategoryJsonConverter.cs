using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

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
