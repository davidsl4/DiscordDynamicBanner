using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DynamicBanner.JsonConverters
{
    public class ObjectConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }
            
            writer.WriteStartObject();
            var dic = (IDictionary<dynamic, dynamic>) value;
            foreach (var kvp in dic)
            {
                serializer.Serialize(writer, kvp, typeof(KeyValuePair<,>));
            }
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var dic = Activator.CreateInstance(objectType);
            var addMethod = objectType.GetMethod("Add");
            if (addMethod == null || dic == null) return null;
            if (reader.TokenType != JsonToken.StartObject) return null;
            reader.Read();
            var kvpType = typeof(KeyValuePair<,>).MakeGenericType(objectType.GenericTypeArguments[0],
                objectType.GenericTypeArguments[1]);
            while (reader.TokenType == JsonToken.PropertyName)
            {
                var des = serializer.Deserialize(reader, kvpType);
                addMethod.Invoke(dic, new[] { kvpType.GetProperty("Key")?.GetValue(des),
                    kvpType.GetProperty("Value")?.GetValue(des) });
            }

            return dic;
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType &&
                   (objectType.GetGenericTypeDefinition().GetInterfaces().Any(t => t == typeof(Dictionary<,>)) ||
                    objectType.GetGenericTypeDefinition() == typeof(Nullable<>) &&
                    objectType.GenericTypeArguments[0].IsGenericType &&
                    objectType.GenericTypeArguments[0].GetInterfaces().Any(t => t == typeof(Dictionary<,>)));
        }
    }
}