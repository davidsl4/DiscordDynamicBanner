using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DynamicBanner.Modules.Attributes;
using DynamicBanner.Utils;
using Microsoft.Extensions.Configuration;

namespace DynamicBanner.Modules
{
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Summary("Information"), CommandListPriority(1)]
    public class InfoModule : ModuleBase<DDBCommandContext>
    {
        private readonly Color _embedColor;
        private readonly string _inviteLink, _supportServerInvite, _defaultPrefix;
        private readonly CommandService _commandService;

        public InfoModule(IConfiguration config, CommandService commandService)
        {
            _embedColor = uint.TryParse(config["embed_color"], out var embedColorRaw) ? new Color(embedColorRaw) : Color.Default;
            _inviteLink = config["invite_link"];
            _supportServerInvite = config["support_server_invite"];
            _defaultPrefix = config["default_prefix"];
            _commandService = commandService;
        }
        
        [Command("ping")]
        [Summary("Check the connectivity to Discord API")]
        public async Task PingCommand()
        {
            var stopwatch = new Stopwatch();
            var useEmbed = Context.IsPrivate ||
                        Context.Guild.CurrentUser.GetPermissions((IGuildChannel) Context.Channel).EmbedLinks;

            const string title = "Ping check";
            var body = "Measuring..";
            
            var builder = useEmbed ? new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(body)
                .WithColor(_embedColor) : null;
            var embed = builder?.Build();
            stopwatch.Start();
            var msg = await ReplyAsync(useEmbed ? null : $"**{title}**\n{body}", embed: embed).ConfigureAwait(false);
            stopwatch.Stop();
            await msg.ModifyAsync(mp =>
            {
                body = $":heartbeat: Ping: {stopwatch.ElapsedMilliseconds}ms";
                builder?.WithDescription(body);
                mp.Embed = builder?.Build();
                mp.Content = useEmbed ? null : $"**{title}**\n{body}";
            }).ConfigureAwait(false);
        }

        [Command("info")]
        // ReSharper disable once StringLiteralTypo
        [Alias("about", "botinfo")]
        [Summary("Information about the bot")]
        public async Task InfoCommand()
        {
            if (!(Context.IsPrivate ||
                  Context.Guild.CurrentUser.GetPermissions((IGuildChannel) Context.Channel).EmbedLinks))
            {
                await ReplyAsync("To use this command the bot should have the \"Embed Links\" permission")
                    .ConfigureAwait(false);
                return;
            }

            var builder = new EmbedBuilder()
                .WithTitle("Bot information")
                .WithColor(_embedColor)
                .WithDescription("Discord Dynamic Banner bot is a bot that is used to add dynamically changing values, " +
                                 "such as user count to Discord server banners.\n\n" +
                                 $"Developer: r0den#1157\nLibrary: Discord.Net (C#)\nInvite: [Click here]({_inviteLink})\n" +
                                 $"Support server: [Click here]({_supportServerInvite})")
                .WithFooter("© 2021");
            var embed = builder.Build();
            await ReplyAsync(embed: embed).ConfigureAwait(false);
        }

        [Command("help")]
        [Summary("List all commands")]
        public async Task HelpCommand()
        {
            var useEmbed = Context.IsPrivate ||
                            Context.Guild.CurrentUser.GetPermissions((IGuildChannel) Context.Channel).EmbedLinks;

            var modules = _commandService.Modules
                .Where(mod =>
                    !string.IsNullOrWhiteSpace(mod.Summary) &&
                    mod.Commands.Any(cmd => !string.IsNullOrWhiteSpace(cmd.Summary))).OrderByDescending(mod =>
                {
                    var orderAttribute =
                        (CommandListPriorityAttribute) mod.Attributes.FirstOrDefault(a =>
                            a.GetType() == typeof(CommandListPriorityAttribute));
                    var order = orderAttribute?.Priority ?? 0;
                    return order;
                }).Select<ModuleInfo, (ModuleInfo Key, string Value)>(module => (module,
                        module.Commands.DistinctBy(cmd => (cmd.Name, cmd.Summary)).Aggregate(
                            (string) null, (str, cmd) =>
                            {
                                str = str == null ? "" : str + "\n";
                                str += "`" + _defaultPrefix + cmd.Name + "` - " + cmd.Summary;
                                return str;
                            })
                    ));

            string message = null;
            Embed embed = null;
            if (useEmbed)
            {
                var builder = new EmbedBuilder()
                    .WithColor(_embedColor);
                foreach (var (module, commands) in modules)
                {
                    builder.AddField(module.Summary, commands, true);
                }

                embed = builder.Build();
            }
            else
            {
                message = modules.Aggregate((string) null, (str, moduleKvp) =>
                {
                    str = str == null ? "" : str + "\n\n";
                    var (module, commands) = moduleKvp;
                    str += $"**{module.Summary}**\n{commands}";
                    return str;
                });
            }

            await ReplyAsync(message, embed: embed).ConfigureAwait(false);
        }
    }
}