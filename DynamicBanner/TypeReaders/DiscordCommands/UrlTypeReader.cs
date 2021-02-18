using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace DynamicBanner.TypeReaders.DiscordCommands
{
    public class UrlTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed,
                    $"Can't cast '{input}' to an Uri object"));

            return Task.FromResult(TypeReaderResult.FromSuccess(uri));
        }
    }
}