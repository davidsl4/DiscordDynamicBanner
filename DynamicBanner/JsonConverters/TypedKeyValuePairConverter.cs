using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DynamicBanner.JsonConverters
{
    public class TypedKeyValuePairConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            const string KeyName = "Key";
            const string ValueName = "Value";
            
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var kvpObject = (KeyValuePair<dynamic, dynamic>) value;

            var resolver = serializer.ContractResolver as DefaultContractResolver;

            writer.WriteStartObject();
            writer.WritePropertyName((resolver != null) ? resolver.GetResolvedPropertyName(KeyName) : KeyName);
            serializer.Serialize(writer, kvpObject.Key, kvpObject.Key.GetType());
            writer.WritePropertyName((resolver != null) ? resolver.GetResolvedPropertyName(ValueName) : ValueName);
            serializer.Serialize(writer, kvpObject.Value, kvpObject.Value.GetType());
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (!CanConvert(objectType)) throw new JsonSerializationException("Cannot convert this type.");
            
            if (reader.TokenType == JsonToken.Null)
            {
                if (objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    throw new JsonSerializationException("Cannot convert null value to KeyValuePair.");
                }

                return null;
            }

            var t = objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? Nullable.GetUnderlyingType(objectType)
                : objectType;

            Debug.Assert(t != null, nameof(t) + " != null");
            
            Type keyType = t.GenericTypeArguments[0],
                valueType = t.GenericTypeArguments[1];

            object key = null, value = null;

            JsonContract keyContract = serializer.ContractResolver.ResolveContract(keyType),
                valueContract = serializer.ContractResolver.ResolveContract(valueType);
            
            if (reader.TokenType == JsonToken.PropertyName)
            {
                key = serializer.Deserialize(reader, keyContract.UnderlyingType);
                reader.Read();
                value = serializer.Deserialize(reader, valueContract.UnderlyingType);
                reader.Read();
            }

            var generic =
                typeof(KeyValuePair<,>).MakeGenericType(keyContract.UnderlyingType, valueContract.UnderlyingType);
            return Activator.CreateInstance(generic, key, value);
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>);
        }
    }
}