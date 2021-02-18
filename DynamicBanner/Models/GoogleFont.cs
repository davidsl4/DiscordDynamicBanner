using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace DynamicBanner.Models
{
    [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
    public struct GoogleFont
    {
        public string DebuggerDisplay => Family;
        
        [Flags]
        [JsonConverter(typeof(FontVariantConverter))]
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public enum FontVariants
        {
            Regular = 1 << 0,
            Italic = 1 << 1,
            W100 = 1 << 2,
            W200 = 1 << 3,
            W300 = 1 << 4,
            W500 = 1 << 5,
            W600 = 1 << 6,
            W700 = 1 << 7,
            W800 = 1 << 8,
            W900 = 1 << 9,
            W950 = 1 << 10
        }

        private static readonly ImmutableDictionary<FontVariants, string> HumanReadableWeights =
            new Dictionary<FontVariants, string>
            {
                {FontVariants.W100, "Thin"},
                {FontVariants.W200, "Extra Light"},
                {FontVariants.W300, "Light"},
                {FontVariants.W500, "Medium"},
                {FontVariants.W600, "Semi Bold"},
                {FontVariants.W700, "Bold"},
                {FontVariants.W800, "Extra Bold"},
                {FontVariants.W900, "Black"},
                {FontVariants.W950, "Extra Black"}
            }.ToImmutableDictionary();

        private class FontVariantConverter : JsonConverter<FontVariants>
        {
            public override void WriteJson(JsonWriter writer, FontVariants value, JsonSerializer serializer)
            {
                string val;
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (value)
                {
                    case FontVariants.Regular:
                        val = "regular";
                        break;
                    case FontVariants.Italic:
                        val = "italic";
                        break;
                    default:
                    {
                        var italic = (value & FontVariants.Italic) == FontVariants.Italic;
                        var weight = italic ? value ^ FontVariants.Italic : value;
                        val = weight.ToString().Substring(1);
                        if (italic) val += "italic";
                        break;
                    }
                }

                writer.WriteValue(val);
            }

            public override FontVariants ReadJson(JsonReader reader, Type objectType, FontVariants existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType != JsonToken.String && reader.TokenType != JsonToken.PropertyName)
                    throw new NotSupportedException();
                var value = (string)reader.Value;
                switch (value)
                {
                    case "regular":
                        return FontVariants.Regular;
                    case "italic":
                        return FontVariants.Italic;
                }

                FontVariants parsed = 0;
                var indexOfItalic = value.IndexOf("italic", StringComparison.Ordinal);
                if (indexOfItalic != -1)
                {
                    parsed |= FontVariants.Italic;
                    value = value.Substring(0, indexOfItalic);
                }

                var availableValues = Enum.GetValues<FontVariants>();
                parsed = (from availableValue in availableValues
                    let strVal = availableValue.ToString().Substring(1)
                    where strVal.Equals(value)
                    select availableValue).Aggregate(parsed, (current, availableValue) => current | availableValue);

                return parsed;
            }
        }
        
        [JsonProperty("family")] public string Family { get; set; }
        [JsonProperty("variants")] public HashSet<FontVariants> Variants { get; set; }

        [JsonProperty("files")] public Dictionary<FontVariants, string> Files { get; set; }

        public IEnumerable<string> HumanReadableVariants => VariantsToHumanReadableVariants(Variants);

        public IEnumerable<string> HumanReadableWeightVariants =>
            VariantsToHumanReadableVariants(Variants.Where(v =>
                (v & FontVariants.Italic) == 0));

        private static IEnumerable<string> VariantsToHumanReadableVariants(IEnumerable<FontVariants> variants) =>
            variants.Select(VariantToHumanReadableVariant);

        internal static string VariantToHumanReadableVariant(FontVariants v)
        {
            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
            switch (v)
            {
                case FontVariants.Regular:
                    return "Regular";
                case FontVariants.Italic:
                    return "Italic";
            }

            var isItalic = (v & FontVariants.Italic) == FontVariants.Italic;
            var varWithoutItalic = isItalic ? v ^ FontVariants.Italic : v;
            var numericWeight = varWithoutItalic.ToString().Substring(1);
            if (HumanReadableWeights.TryGetValue(varWithoutItalic, out var variant))
                variant += $" ({numericWeight})";
            else variant = numericWeight;

            if (isItalic) variant += " Italic";

            return variant;
        }
    }
}