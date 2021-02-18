using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Serilog;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace DynamicBanner.Services
{
    public class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly IConfiguration _config;

        public StartupService(IServiceProvider services)
        {
            _config = services.GetRequiredService<IConfiguration>();
            _discord = services.GetRequiredService<DiscordSocketClient>();

            var (_, installedMethods, installedCommands) = services.GetRequiredService<CommandHandler>().InstallCommandsAsync().GetAwaiter()
                .GetResult();
            Log.Debug("Installed {MethodsCount} command methods ({CommandCount} unique commands)", installedMethods, installedCommands);
            
            InitializeDbQueryFactory(services.GetRequiredService<QueryFactory>(), _config.GetSection("db"));
        }

        private static void InitializeDbQueryFactory(QueryFactory queryFactory, IConfiguration config)
        {
            var queryBuilder = new MySqlConnectionStringBuilder
            {
                Server = config["host"],
                Port = uint.TryParse(config["port"], out var port) ? port : 3306,
                UserID = config["user"],
                Password = config["pass"],
                Database = config["db"]
            };
            queryFactory.Connection = new MySqlConnection(queryBuilder.ConnectionString);
            queryFactory.Compiler = new MySqlCompiler();
        }

        public async Task StartAsync()
        {
            var discordToken = _config["bot_token"];
            if (string.IsNullOrWhiteSpace(discordToken))
            {
                Log.Fatal("You have to provide a token for your bot");
                Environment.Exit(0);
                return;
            }

            await _discord.LoginAsync(TokenType.Bot, discordToken).ConfigureAwait(false);
            await _discord.StartAsync().ConfigureAwait(false);
        }
    }
}