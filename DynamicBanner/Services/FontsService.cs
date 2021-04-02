using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
        private readonly ConcurrentDictionary<(string, GoogleFont.FontVariants), FontFamily> _loadedFonts = new();
        private readonly PrivateFontCollection _privateFontCollection = new();
        private readonly object _locker = new();
        
        
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
        
        public async Task<FontFamily> GetOrDownloadFontAsync(string fontName, GoogleFont.FontVariants fontStyle, WebClient webClient)
        {
            var fontId = (fontName, fontStyle);
            FontFamily fontFamily;

            lock (_locker)
            {
                if (_loadedFonts.TryGetValue(fontId, out fontFamily)) return fontFamily;
            }
            
            var googleFont = FindFont(fontName);
            if (!googleFont.HasValue) return null;
            if (!googleFont.Value.Files.TryGetValue(fontStyle, out var fontUrl)) return null;

            var fontBytes = await webClient.DownloadDataTaskAsync(fontUrl).ConfigureAwait(false);
            lock (_locker)
            {
                var fontPtr = Marshal.AllocCoTaskMem(fontBytes.Length);
                try
                {
                    Marshal.Copy(fontBytes, 0, fontPtr, fontBytes.Length);
                    _privateFontCollection.AddMemoryFont(fontPtr, fontBytes.Length);
                    fontFamily = _privateFontCollection.Families[^1];
                    _loadedFonts.TryAdd(fontId, fontFamily);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(fontPtr);
                }
            }

            return fontFamily;
        }

        public void ClearCachedFonts()
        {
            lock (_locker)
            {
                foreach (var font in _loadedFonts)
                    font.Value?.Dispose();
                _loadedFonts.Clear();
            }
        }
    }
}