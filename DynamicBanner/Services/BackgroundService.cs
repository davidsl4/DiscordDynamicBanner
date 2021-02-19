using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Dapper;
using Discord.WebSocket;
using DynamicBanner.DDBProtocol;
using DynamicBanner.Models;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using SqlKata.Execution;
using Timer = System.Timers.Timer;

namespace DynamicBanner.Services
{
    public class BackgroundService
    {
        private readonly QueryFactory _queryFactory;
        private readonly DiscordSocketClient _discordClient;
        private readonly byte _simultaneousThreadsCount;
        private readonly DdbProtocolService _ddbProtocol;
        private readonly Timer _timer = new(1000 * 60);

        public BackgroundService(QueryFactory queryFactory, DiscordSocketClient discordClient, IConfiguration config,
            DdbProtocolService ddbProtocol)
        {
            _queryFactory = queryFactory;
            _discordClient = discordClient;
            _ddbProtocol = ddbProtocol;
            if (!byte.TryParse(config["simultaneous_threads"], out _simultaneousThreadsCount))
                _simultaneousThreadsCount = 5;
            _timer.Elapsed += OnTimerElapsed;
        }

        public void Start() => _timer.Start();
        
        private async void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            await using var connection = new MySqlConnection(_queryFactory.Connection.ConnectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            var guildsToUpdate = (await connection
                .QueryAsync<GuildProps>("SELECT * FROM `guild` WHERE `NextUpdate` <= UTC_TIMESTAMP AND `Status` = TRUE")
                .ConfigureAwait(false)).ToArray();
            await connection.QueryAsync(
                "UPDATE `guild` SET `NextUpdate` = ADDDATE(UTC_TIMESTAMP, INTERVAL `UpdateInterval` MINUTE) WHERE `ID` IN @ids",
                new {ids = guildsToUpdate.Select(g => g.Id)}).ConfigureAwait(false);
            await connection.CloseAsync().ConfigureAwait(false);

            var guildsByGroups = guildsToUpdate.GroupBy(g => g.EndpointUrl);
            var taskList = new Task[_simultaneousThreadsCount];
            var installedThreads = 0;
            ConcurrentStack<ulong> invalidDiscordServerIds = new ConcurrentStack<ulong>();
            var locker = new object();

            void PlanToStartOnNewThreadWithArgs(Action<object> action, Task[] tasks, object state)
            {
                void InstallOnSlot(int slot)
                {
                    lock (locker)
                    {
                        tasks[slot] = Task.Run(() =>
                        {
                            action(state);
                            lock (locker)
                            {
                                tasks[slot] = null;
                            }
                        });
                    }
                }
                if (installedThreads < taskList.Length)
                {
                    InstallOnSlot(installedThreads++);
                }

                lock (locker)
                {
                    CHECK_FOR_FREE_SLOT: for (var i = 0; i < tasks.Length; i++)
                    {
                        if (tasks[i] != null) continue;
                        InstallOnSlot(i);
                        return;
                    }

                    Task.WaitAny(tasks);
                    goto CHECK_FOR_FREE_SLOT;
                }
            }
            void PlanToStartOnNewThread(Action action, Task[] tasks) => PlanToStartOnNewThreadWithArgs(_ => action(), tasks, null);
            foreach (var guildsByGroup in guildsByGroups)
            {
                PlanToStartOnNewThread(async () =>
                {
                    Func<GuildProps, Task<JObject>> getJObject;

                    var uri = new Uri(guildsByGroup.Key);
                    if (uri.Scheme == "ddb")
                    {
                        if (_ddbProtocol.GetRouteAsync(uri, out var routeMethod) != ExecuteResult.Success)
                            return;

                        getJObject = async gp =>
                        {
                            var guild = _discordClient.GetGuild(gp.Id);
                            return (await _ddbProtocol.ExecuteRouteAsync(routeMethod, guild)).returnValue;
                        };
                    }
                    else
                    {
                        using var httpClient = new HttpClient();
                        var downloadedString = await httpClient.GetStringAsync(guildsByGroup.Key).ConfigureAwait(false);
                        getJObject = _ => Task.FromResult(JObject.Parse(downloadedString));
                    }
                    // ReSharper disable once VariableHidesOuterVariable
                    var taskList = new Task[_simultaneousThreadsCount];
                    foreach (var guildProps in guildsByGroup)
                    {
                        PlanToStartOnNewThreadWithArgs(async state =>
                        {
                            // ReSharper disable once VariableHidesOuterVariable
                            var guildProps = (GuildProps)state;
                            try
                            {
                                var socketGuild = _discordClient.GetGuild(guildProps.Id);
                                if (socketGuild == null || !socketGuild.Features.Contains("BANNER") ||
                                    !socketGuild.CurrentUser.GuildPermissions.ManageGuild)
                                {
                                    invalidDiscordServerIds.Push(guildProps.Id);
                                    return;
                                }

                                if (!((await getJObject(guildProps)).SelectToken(guildProps.EndpointPathSelector, true) is JValue token))
                                    throw new Exception("Returned token by the provided path isn't a valid JSON value");

                                if (token.Type != JTokenType.Integer)
                                    throw new NotSupportedException("Invalid value type");

                                using var image = await GetUpdatedDiscordBanner(guildProps, (int) token.Value)
                                    .ConfigureAwait(false);
                                var ms = new MemoryStream();
                                image.Save(ms, ImageFormat.Png);
                                ms.Position = 0;
                                await socketGuild.ModifyAsync(gp =>
                                {
                                    gp.Banner = new Discord.Image(ms);
                                    ms.Dispose();
                                }).ConfigureAwait(false);
                            }
                            catch
                            {
                                // ignored
                            }
                        }, taskList, guildProps);
                    }
                }, taskList);
            }

            Task.WaitAll(taskList);
        }

        public async Task<Image> GetUpdatedDiscordBanner(GuildProps guildProps, int value)
        {
            var guild = _discordClient.GetGuild(guildProps.Id);
            if (guild == null || !guild.Features.Contains("BANNER")) return null;

            throw new NotImplementedException();
        }
    }
}