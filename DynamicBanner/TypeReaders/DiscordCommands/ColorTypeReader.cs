using System;
using System.Globalization;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DynamicBanner.TypeReaders.DiscordCommands
{
    public class ColorTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (input.StartsWith("#"))
                input = input.Substring(1);

            TypeReaderResult result;

            if (!uint.TryParse(input, NumberStyles.HexNumber, null, out var rawValue))
                result = TypeReaderResult.FromError(CommandError.ParseFailed, $"Unable to parse '{input}' to color.");
            else if (rawValue > 0xffffff)
                result = TypeReaderResult.FromError(CommandError.ParseFailed, $"{rawValue:X6} isn't a valid color.");
            else
                result = TypeReaderResult.FromSuccess(new Color(rawValue));

            return Task.FromResult(result);
        }
    }
}