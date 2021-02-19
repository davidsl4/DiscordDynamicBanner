using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DynamicBanner.Models;
using DynamicBanner.Utils;
using Microsoft.Extensions.Configuration;
using SqlKata;
using SqlKata.Execution;
using System;
using System.IO;
using DynamicBanner.Services;
using Humanizer;
using Image = System.Drawing.Image;

namespace DynamicBanner.Modules
{
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Summary("Settings")]
    public class SettingsModule : ModuleBase<DDBCommandContext>
    {
        private readonly QueryFactory _queryFactory;
        private readonly Color _embedColor;
        private readonly FontsService _fontsService;

        private delegate Task<int> UpdateDatabaseDelegate(Query query, object data, IDbTransaction dbTransaction = null,
            int? timeout = null);

        public SettingsModule(QueryFactory queryFactory, IConfiguration config, FontsService fontsService)
        {
            _queryFactory = queryFactory;
            _fontsService = fontsService;
            _embedColor = uint.TryParse(config["embed_color"], out var rawColor) ? new Color(rawColor) : Color.Default;
        }

        [Command("tgstatus")]
        [Summary("Toggle the run status for this server")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task ToggleStatusCommand()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                    Status = true
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                guild.Status = !guild.Status;
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);

            Embed embed = null;
            var message = $"You've {(guild.Status ? "enabled" : "disabled")} the dynamic banner for this server";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setbase")]
        [Summary("Change the base image to be used to update the banner")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task UploadImageCommand()
        {
            Embed embed = null;
            string message = null;
            
            var attachment = Context.Message.Attachments.FirstOrDefault();
            if (attachment == null)
            {
                message =
                    "To change the base image (background) click on the plus button to select a file from " +
                    "your PC, choose the image you want to upload (make sure it has no values that should be " +
                    "updated dynamically), and just before clicking the \"Send\" button on the popup, enter the " +
                    "command in the text area, then click on send so the command will be sent with the image attached " +
                    "to it.\n*P.S Currently we do only support images with a **.png** extension.*";

                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid command usage")
                        .WithDescription(message);
                    embed = builder.Build();
                    message = null;
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
            }
            else if (!attachment.Filename.EndsWith(".png"))
            {
                message = "We're accepting images only with a **.png** extension.";
                
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid command usage")
                        .WithDescription(message);
                    embed = builder.Build();
                    message = null;
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);

                using (var httpClient = new HttpClient())
                await using (var ms = new MemoryStream())
                {
                    await using (var stream = await httpClient.GetStreamAsync(attachment.Url).ConfigureAwait(false))
                    {
                        await stream.CopyToAsync(ms).ConfigureAwait(false);
                    }
                    
                    var downloadedContent = new byte[ms.Length];
                    ms.Position = 0;
                    await ms.ReadAsync(downloadedContent.AsMemory(0, downloadedContent.Length)).ConfigureAwait(false);
                    if (downloadedContent.GetFileTypeFromHeader() != FileExtensions.FileTypes.Png)
                    {
                        message = "This file isn't a valid PNG image.";
                
                        if (Context.CanSendEmbeds)
                        {
                            var builder = new EmbedBuilder()
                                .WithColor(_embedColor)
                                .WithTitle("Invalid command usage")
                                .WithDescription(message);
                            embed = builder.Build();
                            message = null;
                        }
                        await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                        return;
                    }
                    
                    int width, height;
                    if (attachment.Width.HasValue && attachment.Height.HasValue)
                        (width, height) = (attachment.Width.Value, attachment.Height.Value);
                    else
                    {
                        ms.Position = 0;
                        using var image = Image.FromStream(ms);
                        width = image.Width;
                        height = image.Height;
                    }

                    if (width < 960 || height < 540)
                    {
                        message = "Discord accepts banners that's at least 960x540 pixels, " +
                                  "and the base image should be a banner.";
                
                        if (Context.CanSendEmbeds)
                        {
                            var builder = new EmbedBuilder()
                                .WithColor(_embedColor)
                                .WithTitle("Invalid banner")
                                .WithDescription(message);
                            embed = builder.Build();
                            message = null;
                        }
                        await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                        return;
                    }

                    var gdc = width.GreatestCommonDivisor(height);
                    if (!(width / gdc == 16 && height / gdc == 9))
                    {
                        message = "Discord accepts only 16:9 banners, and the base image should be a banner.";
                
                        if (Context.CanSendEmbeds)
                        {
                            var builder = new EmbedBuilder()
                                .WithColor(_embedColor)
                                .WithTitle("Invalid banner")
                                .WithDescription(message);
                            embed = builder.Build();
                            message = null;
                        }
                        await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                        return;
                    }
                }
                
                var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                    .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

                UpdateDatabaseDelegate replaceDelegate;
                if (guild == null)
                {
                    guild = new GuildProps
                    {
                        Id = Context.Guild.Id,
                    };
                    replaceDelegate = QueryExtensions.InsertAsync;
                }
                else
                    replaceDelegate = QueryExtensions.UpdateAsync;

                guild.Status = false;
                guild.BaseImageUrl = attachment.Url;
                await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);

                const string body =
                    "You've changed the base image. This image will be used as a background to the dynamic values.\n" +
                    "Because different images can have different fonts, colors, and render positions for the " +
                    "dynamic values, we've temporarily turned off the generating and uploading of them.\n" +
                    "Use the `tgstatus` command to turn it back on.";

                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithDescription(body)
                        .WithImageUrl(attachment.Url)
                        .WithColor(_embedColor);
                    embed = builder.Build();
                }
                else
                {
                    message = $"{body}\n\n||{attachment.Url}||";
                }

                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
            }
        }
        
        [Command("setendpoint")]
        [Summary("Change the endpoint with the updated values to output")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [Priority(1)]
        public async Task SetEndpointCommand(Uri url)
        {
            Embed embed = null;
            string message = null;
            
            if (url.Scheme == "ddb")
            {
                if (url.Host != "guild" || url.LocalPath != "/members/count")
                {
                    var description = $"Protocol **{url.Scheme}://** doesn't have this endpoint.\n" +
                                      "Check the support server for info about the bot's local protocol.";
                    
                    if (Context.CanSendEmbeds)
                    {
                        var builder = new EmbedBuilder()
                            .WithColor(_embedColor)
                            .WithTitle("Missing endpoint")
                            .WithDescription(description);
                        embed = builder.Build();
                    }
                    else
                    {
                        message = description;
                    }
                    await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                    return;
                }
            }
            else if (!(url.Scheme == "http" || url.Scheme == "https"))
            {
                var description = $"Protocol **{url.Scheme}://** isn't supported yet.\n" +
                                  "Check the support server for a list of supported protocols.";
                
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Unsupported protocol")
                        .WithDescription(description);
                    embed = builder.Build();
                }
                else
                {
                    message = description;
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
                replaceDelegate = QueryExtensions.UpdateAsync;
            guild.Status = false;
            guild.EndpointUrl = url.AbsoluteUri;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);

            message = $"Endpoint URL have been updated to `{guild.EndpointUrl}`.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setendpoint")]
        [Summary("Change the endpoint with the updated values to output")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetEndpointCommand()
        {
            Embed embed = null;
            string message = null;

            const string usage = "`setendpoint [Full URL]`",
                remark = "URL should include the protocol (`http://`, `https://`, `ddb://`)";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Invalid usage")
                    .WithDescription($"Use: {usage}")
                    .AddField("Remark", remark);
                embed = builder.Build();
            }
            else
            {
                message = $"**Usage:** {usage}\n\n{remark}";
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setfont")]
        [Summary("Change the font for the dynamic values")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontCommand([Remainder] string fontName = null)
        {
            Embed embed = null;
            string message = null;
            
            if (fontName == null)
            {
                const string usage = "`setfont [Font name]`";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription($"Use: {usage}")
                        .AddField(":bulb: Did you know?",
                            "Currently we only support fonts from [Google Fonts](https://fonts.google.com)");
                    embed = builder.Build();
                }
                else
                {
                    message = $"**Usage:** {usage}\n\n" +
                              "Currently we only support fonts from https://fonts.google.com";
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var font = _fontsService.FindFont(fontName);
            if (!font.HasValue)
            {
                const string title = "It mostly looks like we aren't supporting this font yet",
                    googleFontListName = "Google's huge font list",
                    googleFontListUrl = "https://fonts.google.com/",
                    descriptionFormat =
                        "We tried our best to find a font with this name on our database, but we didn't find anything." +
                        "\n\nJust for a remark, we support almost every available font from " +
                        "{0}.\nIf you've found your desired " +
                        "font there, you mostly want to join the support server and ask us to update the font database.";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle(title)
                        .WithDescription(string.Format(descriptionFormat, $"[{googleFontListName}]({googleFontListUrl})"));
                    embed = builder.Build();
                }
                else
                {
                    message = $"**{title}**\n" +
                              string.Format(descriptionFormat, $"{googleFontListName} *({googleFontListUrl})*");
                }

                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
                replaceDelegate = QueryExtensions.UpdateAsync;
            guild.Status = false;
            guild.FontName = font.Value.Family;
            guild.FontStyle = GoogleFont.FontVariants.Regular;
            
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);

            var description = $"You've changed the render font to `{font.Value.Family}`.\n\n" +
                              "Since changing the font before reconfiguring all again can make your banner ugly, " +
                              "we've turned off the banner updater for you.\n" +
                              "To turn in back on use the `tgstatus` command.";
            const string changingStyleTitle = "Change the render style";
            var changingStyleBody = "You can make the font italic by using the `tgfontitl` command.\n" +
                                    "Use the `setfontweight` command to change the weight (regular, bold, semi-bold and etc.) " +
                                    "to one of the following: ```\n" +
                                    font.Value.HumanReadableWeightVariants.Aggregate((string) null,
                                        (current, variant) => current == null ? variant : current + $"\n{variant}") +
                                    "```";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(description)
                    .AddField(changingStyleTitle, changingStyleBody);
                embed = builder.Build();
            }
            else
            {
                message = $"{description}\n\n**{changingStyleTitle}**\n{changingStyleBody}";
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("tgfontitl")]
        [Summary("Toggle the italic for the font")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task ToggleFontItalicCommand()
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                    FontStyle = GoogleFont.FontVariants.Italic
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                guild.FontStyle ^= GoogleFont.FontVariants.Italic;
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);

            Embed embed = null;
            var message =
                "You've " + 
                ((guild.FontStyle & GoogleFont.FontVariants.Italic) == GoogleFont.FontVariants.Italic ? "enabled" : "disabled") +
                " the italic for the current font.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setfontweight")]
        [Summary("Set the weight of the font (regular, semi-bold, bold)")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontWeightCommand([Remainder] string weight = null)
        {
            Embed embed = null;
            string message = null;
            
            if (weight == null)
            {
                const string usage = "`setfontweight [Font weight]`",
                    listOfWeightsTitle = "Font weights",
                    listOfWeightsBody = "Based on your font, choose one of the following: `Thin`, `Extra light`, " +
                                        "`Light`, `Regular`, `Medium`, `Semi-bold`, `Bold`, `Extra-bold`, `Black`, " +
                                        "`Extra black`";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription($"Use: {usage}")
                        .AddField(listOfWeightsTitle, listOfWeightsBody);
                    embed = builder.Build();
                }
                else
                {
                    message = $"**Usage:** {usage}\n\n**{listOfWeightsTitle}:**\n{listOfWeightsBody}";
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }
            
            GoogleFont.FontVariants fontVariant;

            switch (weight.ToLowerInvariant())
            {
                case "100":
                case "thin":
                    fontVariant = GoogleFont.FontVariants.W100;
                    break;
                case "200":
                case "extra light":
                case "extra-light":
                case "el":
                    fontVariant = GoogleFont.FontVariants.W200;
                    break;
                case "300":
                case "light":
                    fontVariant = GoogleFont.FontVariants.W300;
                    break;
                case "400":
                case "regular":
                case "reg":
                    fontVariant = GoogleFont.FontVariants.Regular;
                    break;
                case "500":
                case "medium":
                case "med":
                    fontVariant = GoogleFont.FontVariants.W500;
                    break;
                case "600":
                case "semi":
                case "semi bold":
                case "semi-bold":
                    fontVariant = GoogleFont.FontVariants.W600;
                    break;
                case "700":
                case "bold":
                    fontVariant = GoogleFont.FontVariants.W700;
                    break;
                case "800":
                case "extra bold":
                case "extra-bold":
                case "eb":
                    fontVariant = GoogleFont.FontVariants.W800;
                    break;
                case "900":
                case "black":
                case "heavy":
                    fontVariant = GoogleFont.FontVariants.W900;
                    break;
                case "950":
                case "extra black":
                case "extra-black":
                case "ebl":
                case "extra heavy":
                case "extra-heavy":
                case "eh":
                    fontVariant = GoogleFont.FontVariants.W950;
                    break;
                default:
                {
                    message = "You've entered an invalid value.\nWeight should be one of the following: " +
                              "`Thin`, `Extra light`, `Light`, `Regular`, `Medium`, `Semi-bold`, `Bold`, `Extra-bold`, " +
                              "`Black`, `Extra black`";
                    if (Context.CanSendEmbeds)
                    {
                        var builder = new EmbedBuilder()
                            .WithColor(_embedColor)
                            .WithTitle("Invalid usage")
                            .WithDescription(message);
                        embed = builder.Build();
                        message = null;
                    }

                    await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                    return;
                }
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                    FontStyle = fontVariant
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                var useItalic = (guild.FontStyle & GoogleFont.FontVariants.Italic) == GoogleFont.FontVariants.Italic;
                guild.FontStyle = fontVariant;
                if (useItalic) guild.FontStyle |= GoogleFont.FontVariants.Italic;
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            message = $"You've changed the font weight to `{GoogleFont.VariantToHumanReadableVariant(fontVariant)}`.\n" +
                      "If this weight is supported by the selected font, it will be used.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setfontsize")]
        [Summary("Set the font size in pixels")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [Priority(1)]
        public async Task SetFontSize(float size)
        {
            Embed embed = null;
            string message;

            if (size < 1)
            {
                message = "Font size cannot be less than 1.";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription(message);
                    embed = builder.Build();
                    message = null;
                }

                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }

            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            guild.FontSize = size;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            message = $"You've changed the font size to `{size}px`.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }
        
        [Command("setfontsize")]
        [Summary("Set the font size in pixels")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontSize()
        {
            Embed embed = null;
            string message = null;
            
            const string usage = "`setfontsize [Font size (in pixels)]`";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Invalid usage")
                    .WithDescription($"Use: {usage}");
                embed = builder.Build();
            }
            else
            {
                message = $"**Usage:** {usage}";
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setcolor")]
        [Summary("Set the font color")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [Priority(1)]
        public async Task SetFontColorCommand(Color color)
        {
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);

            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            guild.FontColor = color.RawValue;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            var message = $"You've changed the font color to `#{color.RawValue:X6}`.";
            Embed embed = null;
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(color)
                    .WithTitle("Success")
                    .WithDescription(message)
                    .AddField(":bulb: Did you know?", "You can see the set color on the left side of this message.");
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setcolor")]
        [Summary("Set the font color")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontColorCommand()
        {
            Embed embed = null;
            string message = null;
            
            const string usage = "`setcolor [Color HEX code]`";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Invalid usage")
                    .WithDescription($"Use: {usage}");
                embed = builder.Build();
            }
            else
            {
                message = $"**Usage:** {usage}";
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setrot")]
        [Summary("Set the rotation radius for the text")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        [Priority(1)]
        public async Task SetFontRotationCommand(short rotationRadius)
        {
            Embed embed = null;
            string message;

            if (rotationRadius < 0 || rotationRadius > 359)
            {
                message = "Rotation radius cannot be less than 0 or bigger than 359.";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription(message);
                    embed = builder.Build();
                    message = null;
                }

                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }
            
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);
            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            guild.FontRotationRadius = (ushort)rotationRadius;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            message = $"You've changed the rotation radius to `{rotationRadius}°`.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }
        
        [Command("setrot")]
        [Summary("Set the rotation radius for the text")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontRotationCommand()
        {
            Embed embed = null;
            string message = null;
        
            const string usage = "`setrot [Rotation radius]`";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Invalid usage")
                    .WithDescription($"Use: {usage}");
                embed = builder.Build();
            }
            else
            {
                message = $"**Usage:** {usage}";
            }
            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setalign")]
        [Summary("Set the font alignment for the text")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetFontAlignmentCommand(string align = null)
        {
            Embed embed = null;
            string message = null;

            CHECK_ALIGNMENT: if (align == null)
            {
                const string usage = "`setalign [Font alignment]`",
                    acceptableValuesTitle = "Acceptable values",
                    acceptableValuesBody = "`left`, `right`, and `center`";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription($"Use: {usage}")
                        .AddField(acceptableValuesTitle, acceptableValuesBody);
                    embed = builder.Build();
                }
                else
                {
                    message = $"**Usage:** {usage}\n\n**{acceptableValuesTitle}:**\n{acceptableValuesBody}";
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }

            FontAlignment fontAlignment = align.ToLowerInvariant() switch
            {
                "l" => FontAlignment.Left,
                "left" => FontAlignment.Left,
                "c" => FontAlignment.Center,
                "center" => FontAlignment.Center,
                "r" => FontAlignment.Right,
                "right" => FontAlignment.Right,
                _ => 0
            };
            if (fontAlignment == 0)
            {
                align = null;
                goto CHECK_ALIGNMENT;
            }
            
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);
            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            guild.FontAlignment = fontAlignment;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            message = $"You've changed the font alignment to `{fontAlignment.ToString().ToLowerInvariant()}`.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }

        [Command("setpattern")]
        [Summary("Set the pattern selector for the endpoint")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task SetJsonPathSelector([Remainder] string selector = null)
        {
            Embed embed = null;
            string message = null;

            if (selector == null)
            {
                const string usage = "`setpattern [Pattern]`",
                    remarksTitle = "Remark",
                    remarksBodyTemplate = "We're using {0} to select values from the endpoint.\n\n" +
                                          "If you aren't familiar with {1} you can use {2} to generate one for you.\n" +
                                          "**Note:** Some services may use `x`, `?`, and such placeholders at the start " +
                                          "of the pattern. Make sure to replace them with `$` (dollar sign) until you " +
                                          "understand what you're doing. Mostly like your pattern should start with `$.`\n\n" +
                                          "To check the validity of your pattern, you can use {3}.\n\n" +
                                          "Also, note that we're publishing popular patterns on our support server.",
                    jsonPathSyntaxDocumentationUrl =
                        "https://support.smartbear.com/alertsite/docs/monitors/api/endpoint/jsonpath.html",
                    technologyName = "JSONPath",
                    generatorUrl = "https://jsonpathfinder.com/",
                    evaluatorUrl = "http://jsonpath.com/";
                if (Context.CanSendEmbeds)
                {
                    var builder = new EmbedBuilder()
                        .WithColor(_embedColor)
                        .WithTitle("Invalid usage")
                        .WithDescription($"Use: {usage}")
                        .AddField(remarksTitle,
                            remarksBodyTemplate.FormatWith(technologyName,
                                $"[{technologyName}]({jsonPathSyntaxDocumentationUrl})",
                                $"services such as [JSON path finder]({generatorUrl})",
                                $"services such as [jsonpath online evaluator]({evaluatorUrl})"));
                    embed = builder.Build();
                }
                else
                {
                    message = $"**Usage:** {usage}\n\n**{remarksTitle}:**\n" +
                              remarksBodyTemplate.FormatWith(technologyName,
                                  $"{technologyName} ({jsonPathSyntaxDocumentationUrl})", $"services such as {generatorUrl}", $"services such as {evaluatorUrl}");
                }
                await ReplyAsync(message, embed: embed).ConfigureAwait(false);
                return;
            }
            
            await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
            var guild = await _queryFactory.Query("guild").Where("ID", Context.Guild.Id)
                .FirstOrDefaultAsync<GuildProps>().ConfigureAwait(false);
            UpdateDatabaseDelegate replaceDelegate;
            if (guild == null)
            {
                guild = new GuildProps
                {
                    Id = Context.Guild.Id,
                };
                replaceDelegate = QueryExtensions.InsertAsync;
            }
            else
            {
                replaceDelegate = QueryExtensions.UpdateAsync;
            }

            guild.EndpointPathSelector = selector;
            await replaceDelegate(_queryFactory.Query("guild"), guild).ConfigureAwait(false);
            
            message = "You've changed the pattern selector.\n" +
                      "Hopefully, you've checked that on some online services to make sure it is correct and returns the " +
                      "value you want since we aren't.";
            if (Context.CanSendEmbeds)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor)
                    .WithTitle("Success")
                    .WithDescription(message);
                embed = builder.Build();
                message = null;
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }
    }
}