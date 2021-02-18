using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using DynamicBanner.Modules;
using DynamicBanner.TypeReaders.DiscordCommands;
using DynamicBanner.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DynamicBanner.Services
{
    public class CommandHandler
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly string _defaultPrefix;

        public CommandHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _client = serviceProvider.GetRequiredService<DiscordSocketClient>();
            _commands = serviceProvider.GetRequiredService<CommandService>();
            _defaultPrefix = serviceProvider.GetRequiredService<IConfiguration>()["default_prefix"];
            
            _commands.AddTypeReader<Uri>(new UrlTypeReader());
        }
        
        public async Task<(int modules, int methods, int commands)> InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Here we discover all of the command modules in the entry 
            // assembly and load them. Starting from Discord.NET 2.0, a
            // service provider is required to be passed into the
            // module registration method to inject the 
            // required dependencies.
            //
            // If you do not use Dependency Injection, pass null.
            // See Dependency Injection guide for more information.
            var modules = await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _serviceProvider);
            var moduleInfos = modules as ModuleInfo[] ?? modules.ToArray();
            var commands = moduleInfos.SelectMany(m => m.Commands);
            var commandInfos = commands as CommandInfo[] ?? commands.ToArray();
            return (moduleInfos.Length, commandInfos.Length, commandInfos.DistinctBy(cmd => (cmd.Name, cmd.Summary)).Count());
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (!(messageParam is SocketUserMessage message)) return;
            
            // Create a number to track where the prefix ends and the command begins
            var argPos = 0;
            
            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasStringPrefix(_defaultPrefix, ref argPos) || 
                  message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new DDBCommandContext(_client, message);

            // Execute the command with the command context we just
            // created, along with the service provider for precondition checks.
            await _commands.ExecuteAsync(
                context, 
                argPos,
                _serviceProvider);
        }
    }
}