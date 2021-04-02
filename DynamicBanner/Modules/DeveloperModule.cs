using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord.Commands;
using DynamicBanner.Services;

namespace DynamicBanner.Modules
{
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Group("dev")]
    [RequireOwner]
    public class DeveloperModule : ModuleBase<DDBCommandContext>
    {
        private readonly FontsService _fontsService;

        public DeveloperModule(FontsService fontsService)
        {
            _fontsService = fontsService;
        }

        [Command("fontvar")]
        public async Task GetFontVariants([Remainder] string fontName = null)
        {
            if (fontName == null)
            {
                await ReplyAsync("**Usage:** `dev fontvar [Font Name]`").ConfigureAwait(false);
                return;
            }

            var font = _fontsService.FindFont(fontName);
            if (!font.HasValue)
            {
                await ReplyAsync("Can't find font with this name.").ConfigureAwait(false);
                return;
            }

            await ReplyAsync($"Here is a list of variants for `{font.Value.Family}`:\n```\n" +
                             font.Value.HumanReadableVariants.Aggregate(((string msg, int index)) (null, 0),
                                 (current, variant) => (
                                     (current.msg == null ? "" : $"{current.msg}\n") + (current.index + 1) + ". " +
                                     variant, current.index + 1)).msg +
                             "```")
                .ConfigureAwait(false);
        }

        [Command("fontload")]
        public async Task LoadGoogleFont()
        {
            await ReplyAsync("**Usage:** `dev fontload [Font Style Index] [Font Name]`").ConfigureAwait(false);
        }
        [Command("fontload")]
        [Priority(1)]
        public async Task LoadGoogleFont(int styleIndex, [Remainder] string fontName)
        {
            var font = _fontsService.FindFont(fontName);
            if (!font.HasValue)
            {
                await ReplyAsync("Can't find font with this name.").ConfigureAwait(false);
                return;
            }

            var fontVariant = font.Value.Variants.ElementAtOrDefault(styleIndex - 1);
            if (fontVariant.Equals(default))
            {
                await ReplyAsync("Can't find this font style.").ConfigureAwait(false);
                return;
            }

            using var webClient = new WebClient();
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var fontFamily = await _fontsService.GetOrDownloadFontAsync(fontName, fontVariant, webClient)
                .ConfigureAwait(false);
            await ReplyAsync($"This font style successfully loaded to memory.\nFont family name: `{fontFamily.Name}`")
                .ConfigureAwait(false);
        }
    }
}