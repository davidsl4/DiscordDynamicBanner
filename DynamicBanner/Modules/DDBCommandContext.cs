using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DynamicBanner.Modules
{
    public class DDBCommandContext : SocketCommandContext
    {
        public bool CanSendEmbeds { get; }
        
        public DDBCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
            CanSendEmbeds = IsPrivate || Guild.CurrentUser.GetPermissions((IGuildChannel) Channel).EmbedLinks;
        }
    }
}