using System;
using System.IO;
using Discord.Commands;
using Discord.WebSocket;
using DynamicBanner.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Threading.Tasks;
using DynamicBanner.DDBProtocol;
using SqlKata.Execution;

namespace DynamicBanner
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            #region initialize logger
            Log.Logger = new LoggerConfiguration()
#if VERBOSE_DEBUG // check for the verbose debugging configuration (to output log messages with level below debug)
                .MinimumLevel.Verbose()
#elif DEBUG // if we are currently on the debug configuration log only debug+ level messages
                .MinimumLevel.Debug()
#else // on the release configuration we do not need (probably) all the debug messages, so we can disable them and output only from the information+ levels
                .MinimumLevel.Information()
#endif
                .WriteTo.Console(theme: SystemConsoleTheme.Colored)
                .CreateLogger();
            #endregion
            
            var serviceCollection = new ServiceCollection()
                // Add bot services
                .AddSingleton<StartupService>()
                .AddSingleton<FontsService>()
                .AddSingleton<DdbProtocolService>()
                .AddSingleton<BackgroundService>()
                // Add config
                .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddJsonFile(Path.GetFullPath(Environment.GetEnvironmentVariable("settings file") ?? "config.json", AppContext.BaseDirectory))
                    .SetFileLoadExceptionHandler(e =>
                    {
                        Log.Fatal(e.Exception, "An exception thrown when tried to load configuration file {Path}", e.Provider.Source.Path);
                        Environment.Exit(1);
                    })
                    .Build()
                )
                // Add Discord client
                .AddSingleton<DiscordSocketClient>()
                // Add command handler
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    CaseSensitiveCommands = false,
                    DefaultRunMode = RunMode.Async,
                    IgnoreExtraArgs = true
                }))
                .AddSingleton<CommandHandler>()
                // Add database
                .AddSingleton(new QueryFactory(null, null));

            // Build the service collection
            var services = serviceCollection.BuildServiceProvider();
            
            await services.GetRequiredService<StartupService>().StartAsync().ConfigureAwait(false);

            await Task.Delay(-1).ConfigureAwait(false);
        }
    }
}