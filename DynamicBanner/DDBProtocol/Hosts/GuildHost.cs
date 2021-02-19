using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using DynamicBanner.DDBProtocol.Attributes;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DynamicBanner.DDBProtocol.Hosts
{
    [Host("guild")]
    [SuppressMessage("ReSharper", "UnusedType.Global")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [UsedImplicitly]
    public class GuildHost
    {
        [Route("members")]
        [UsedImplicitly]
        public class MembersRoute
        {
            private readonly struct OnlineStatsStruct
            {
                [JsonProperty("all")]
                public uint All { get; }
                [JsonProperty("online")]
                public uint Online { get; }
                [JsonProperty("away")]
                public uint Away { get; }
                [JsonProperty("dnd")]
                public uint DoNotDisturb { get; }
                [JsonProperty("offline")]
                public uint Offline { get; }
                [JsonProperty("not_offline")]
                public uint NotOffline => All - Offline;
                
                private OnlineStatsStruct(uint all, uint online, uint away, uint dnd, uint offline)
                {
                    All = all;
                    Online = online;
                    Away = away;
                    DoNotDisturb = dnd;
                    Offline = offline;
                }

                public static implicit operator OnlineStatsStruct(IGuildUser[] users) => new(
                    (uint)users.Length,
                    (uint)users.Count(h => h.Status == UserStatus.Online),
                    (uint)users.Count(h => h.Status == UserStatus.Idle || h.Status == UserStatus.AFK),
                    (uint)users.Count(h => h.Status == UserStatus.DoNotDisturb),
                    (uint)users.Count(h => h.Status == UserStatus.Offline || h.Status == UserStatus.Invisible));
            }
            
            [Route("count")]
            public static async Task<JObject> CountRoute(IGuild guild)
            {
                var users = await guild.GetUsersAsync().ConfigureAwait(false);

                return JObject.FromObject(new
                {
                    all = users.Count,
                    humans = (OnlineStatsStruct)users.Where(u => !u.IsBot).ToArray(),
                    bots = (OnlineStatsStruct)users.Where(u => u.IsBot).ToArray()
                });
            }
        }
    }
}