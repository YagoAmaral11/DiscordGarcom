using DSharpPlus;
using DSharpPlus.Commands.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core.Modules.Examples;

public class ExampleModule(IPersistance persistance, IConfigPersistance config) : BaseModule<ExampleConfig, ExampleData>(persistance, config)
{
    public override string Name => "ExampleModule";
    protected override bool ThrowExceptionOnMissingConfig => false;

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;

    public override IEnumerable<CommandBuilder> GetDynamicCommands() => [];
    public override List<Type> GetStaticCommands() => [];    

    public override Task Start() => Task.CompletedTask;    

    protected override ExampleConfig InitializeConfig() => new ExampleConfig();
    protected override ExampleData InitializeData() => new ExampleData();
}

public class ExampleData
{
    public ulong Id { get; set; }
}

public class ExampleConfig
{
    public bool Foo { get; set; } = false;
}