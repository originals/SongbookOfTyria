using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SongbookOfTyria.Serialization
{
    public class StringOrArrayConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<string>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            
            if (token.Type == JTokenType.Array)
            {
                return token.ToObject<List<string>>();
            }
            
            if (token.Type == JTokenType.String)
            {
                return new List<string> { token.ToString() };
            }
            
            if (token.Type == JTokenType.Null)
            {
                return new List<string>();
            }
            
            return new List<string>();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}
