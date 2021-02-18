using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
                             font.Value.HumanReadableVariants.Aggregate((string) null,
                                 (current, variant) => current == null ? variant : current + $"\n{variant}") +
                             "```")
                .ConfigureAwait(false);
        }
    }
}