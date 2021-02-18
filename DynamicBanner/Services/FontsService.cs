using System;
using System.IO;
using System.Linq;
using DynamicBanner.JsonConverters;
using DynamicBanner.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace DynamicBanner.Services
{
    public class FontsService
    {
        private readonly GoogleFont[] _fonts;
        
        public FontsService(IConfiguration config)
        {
            var fontsJsonFile = Path.GetFullPath(config["fonts_json"], AppContext.BaseDirectory);
            if (!File.Exists(fontsJsonFile))
            {
                Log.Error("Unable to load JSON file with google fonts, file not found");
                return;
            }

            try
            {
                var jObject = JObject.Parse(File.ReadAllText(fontsJsonFile));
                if (jObject["items"] is JArray items)
                {
                    _fonts = items.ToObject<GoogleFont[]>(JsonSerializer.CreateDefault(new JsonSerializerSettings()
                    {
                        ContractResolver = new CustomResolver()
                    }));
                }
                else
                {
                    throw new JsonReaderException("items property wasn't found");
                }
            }
            catch (JsonReaderException)
            {
                Log.Error("Unable to load JSON file with google fonts, invalid JSON");
            }
        }

        public GoogleFont? FindFont(string name)
        {
            try
            {
                return _fonts?.First(f => f.Family.Equals(name, StringComparison.OrdinalIgnoreCase));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}