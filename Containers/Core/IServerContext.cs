using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;

namespace GarçomDoKitts.Containers.Core;

public interface IServerContext
{
    // Discord Info
    public DiscordGuild BindedDiscordServer { get; } // O Server do Discord que esse shell está vinculado
    public DiscordClient BotDiscordClient { get; } // O Discord Client do bot
    public DiscordUser BotDiscordUser { get; } // O User do Discord do Bot    

    // Module Providers
    public T GetModule<T>() where T : IModule; 
    public bool TryGetModule<T>(out T Module) where T : IModule;
    public object GetModule(Type moduleType);
    public IEnumerable<IModule> GetAllModules();
}