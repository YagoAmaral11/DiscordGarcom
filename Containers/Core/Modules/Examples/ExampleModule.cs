using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules.Examples;

public class ExampleModule(IPersistance persistance, IConfigPersistance config) : BaseModule<ExampleConfig, ExampleData>(persistance, config)
{
    public override string Name => "ExampleModule"; // The name of this Module
    protected override bool ThrowExceptionOnMissingConfig => false; // If this module should break startup if the config file is not present

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;

    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        // Use DSharpPlus CommandBuilder to create your commands for this module
        var cb = CommandBuilder.From(PingCommand);
        return [cb];
    }

    public override List<Type> GetStaticCommands() => [];    

    public override Task Start() => Task.CompletedTask;    

    protected override ExampleConfig InitializeConfig() => new ExampleConfig();
    protected override ExampleData InitializeData() => new ExampleData();

    [Command("Ping")]
    [Description("Responds with pong!")]
    public async Task PingCommand(CommandContext context)
    {        
        if (!await CommandReadyPreCondition(context)) // Verify if the bot is ready to receive commands
            return;

        // Good practice: wrap your commands in a try-catch to avoid exceptions shutting down the bot
        try
        {           
            await context.RespondAsync("Pong!");            
        }
        catch (Exception e)
        {
            await DumpException(e); // You can use BaseModule.DumpException to log Exceptions for further investigations
        }
    }
}

// This is the data used by this module, you can acess it via BaseModule.data; It gets automatically readen on startup and written on shutdown
public class ExampleData
{
    public ulong Id { get; set; }
}

// This is the config used by this module, you can acess it via BaseModule.config; It gets automatically readen on startup
public class ExampleConfig
{
    public bool Foo { get; set; } = false;
}