using System;
using CyberPatriot.Models;
using Newtonsoft.Json;

namespace CyberPatriot
{
    public class TeamIdJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var name = (TeamId)value;
            writer.WriteValue(value?.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            return TeamId.Parse(reader.ReadAsString());
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(TeamId).IsAssignableFrom(objectType);
        }
    }
}