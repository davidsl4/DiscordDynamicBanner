using System;
using System.Collections.Generic;
using Newtonsoft.Json.Serialization;

namespace DynamicBanner.JsonConverters
{
    public class CustomResolver : DefaultContractResolver
    {
        protected override JsonContract CreateContract(Type objectType)
        {
            switch (objectType.IsGenericType)
            {
                case true when objectType.GetGenericTypeDefinition() == typeof(Dictionary<,>):
                {
                    JsonContract contract = base.CreateObjectContract(objectType);
                    contract.Converter = new ObjectConverter();
                    return contract;
                }
                case true when objectType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>):
                {
                    var contract = base.CreateObjectContract(objectType);
                    contract.Converter = new TypedKeyValuePairConverter();
                    return contract;
                }
                default:
                    return base.CreateContract(objectType);
            }
        }

    }
}