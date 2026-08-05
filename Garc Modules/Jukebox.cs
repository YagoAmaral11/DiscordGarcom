using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands.Trees;
using Lavalink4NET.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordGarçom.GarcModules;

public class Jukebox(IPersistance persistance, IConfigPersistance configPersistance) : BaseModule<JukeboxConfig, JukeboxData>(persistance, configPersistance)
{
    public override string Name => "Jukebox";
    protected override bool ThrowExceptionOnMissingConfig => false;

    public Task ConfigureServices(IServiceCollection services)
    {
        services.AddLavalink();
        return Task.CompletedTask;
    }

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        throw new NotImplementedException();
    }

    public override List<Type> GetStaticCommands()
    {
        throw new NotImplementedException();
    }

    public override Task Start()
    {
        throw new NotImplementedException();
    }

    protected override JukeboxConfig InitializeConfig()
    {
        throw new NotImplementedException();
    }

    protected override JukeboxData InitializeData()
    {
        throw new NotImplementedException();
    }
}

public class JukeboxConfig
{
}

public class JukeboxData
{
}