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
            var val = (string)reader.Value;
            if (Enum.TryParse(val, out ServiceCategory retVal))
            {
                return retVal;
            }
            else if (ServiceCategoryExtensions.TryParseCanonicalName(val, out retVal))
            {
                return retVal;
            }
            else if (objectType == typeof(ServiceCategory?) && (val == null || val == "null"))
            {
                return null;
            }

            throw new JsonReaderException("Error parsing ServiceCategory.");
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(ServiceCategory).IsAssignableFrom(objectType) || typeof(ServiceCategory?).IsAssignableFrom(objectType);
        }

    }
}
