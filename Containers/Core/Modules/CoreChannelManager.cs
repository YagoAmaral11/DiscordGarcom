using DSharpPlus;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using GarçomDoKitts.GarcModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core.Modules;

public class CoreChannelManager(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : IModule
{

    public string Name => "Core Channel Manager";

    private IPersistance persistance = persistance;
    private IConfigPersistance configPersistance = configPersistance;
    private IScheduler scheduler = scheduler;
    private IServerContext serverContext;

    private ChannelManagerConfig config;
    private ChannelManagerData data;

    private DiscordChannel RootTempChannels;

    bool ready = false;

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        // TODO: Escutar para eventos de exclusão de canais, para verificar se algum canal temporário foi excluído manualmente, e então remover do registro
        throw new NotImplementedException();
    }

    public IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        // TODO: Adicionar comandos para criar canais temporários, listar canais temporários, deletar canais temporários, etc.
        throw new NotImplementedException();
    }

    public List<Type> GetStaticCommands() => [];    


    public async Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {        
        IModule mod = this;

        if (persistance == null)
            throw new Exception(mod.LogName + "IPersistance is not assigned to the module");

        if (configPersistance == null)
            throw new Exception(mod.LogName + "IConfigPersistance is not assigned to the module");
        
        this.serverContext = serverContext;        
        config = new();

        if (await configPersistance.ConfigExists(this))
        {
            // Carrega a configuração existente
            await LoadConfig();
        }
        else
        {
            // Cria uma configuração inicial
            await configPersistance.WriteConfig(this, config);
            throw new Exception(mod.LogName + " config not found. Please modify the standard one.");
        }

        await LoadData();

        return true;
    }    

    public async Task PreStart_0()
    {
        RootTempChannels = await serverContext.BotDiscordClient.GetChannelAsync(config.TempChannelRootCategoryID);
        ready = true;
    }

    public Task Start() => Task.CompletedTask;    



    private async Task LoadConfig()
    {
        ChannelManagerConfig loadedConfig = await configPersistance.LoadConfig(this, typeof(ChannelManagerConfig)) as ChannelManagerConfig;
        config = loadedConfig;
    }

    private async Task LoadData()
    {
        if (await persistance.KeyExists(Name + "Data" + ".json"))
        {
            ChannelManagerData loadedData = await persistance.ReadObject(Name + "Data", typeof(ChannelManagerData)) as ChannelManagerData;
            data = loadedData;
        }            
    }

    public Task<bool> SaveData()
    {
        throw new NotImplementedException();
    }



    public async Task<TempChannelRegistry> NewGeneralTempChannel(DateTimeOffset exclusion, string name = null)
    {
        if (!ready)
            return null;

        name ??= config.GeneralTempChannelNameSecPrefix + config.DefaultGeneralTempChannelName;

        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels);
        var reg = NewRegistry(exclusion, channel.Id);

        // TODO: Adicionar lógica para deletar o canal após o tempo de exclusão, usando o IScheduler

        return reg;
    }

    public async Task<TempChannelRegistry> NewOwnedTempChannel(DateTimeOffset exclusion, ulong ownerID, string name = null)
    {
        if (!ready)
            return null;
        
        DiscordMember owner = await serverContext.BindedDiscordServer.GetMemberAsync(ownerID);

        if (UserCanCreateTempChannel(ownerID) == false)
            return null;

        name ??= config.OwnedTempChannelNameSecPrefix + config.OwnedTempChannelName + owner.DisplayName;

        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels);
        var reg = NewRegistry(exclusion, channel.Id, ownerID);

        // TODO: Adicionar lógica para deletar o canal após o tempo de exclusão, usando o IScheduler

        return reg;
    }

    public async Task<TempChannelRegistry> NewPrivateTempChannel(DateTimeOffset exclusion, ulong ownerID, string name = null)
    {
        if (!ready)
            return null;

        DiscordMember owner = await serverContext.BindedDiscordServer.GetMemberAsync(ownerID);

        if (UserCanCreateTempChannel(ownerID) == false)
            return null;

        name ??= config.PrivateTempChannelNameSecPrefix + config.PrivateTempChannelName + owner.DisplayName;

        DiscordOverwriteBuilder overwriteEveryone = new DiscordOverwriteBuilder(serverContext.BindedDiscordServer.EveryoneRole);        
        overwriteEveryone.Deny(DiscordPermission.SendMessages);
        overwriteEveryone.Deny(DiscordPermission.Connect);

        DiscordOverwriteBuilder overwriteOwner = new DiscordOverwriteBuilder(owner);        
        overwriteOwner.Allow(DiscordPermission.Connect);
        overwriteOwner.Allow(DiscordPermission.MoveMembers);

        IEnumerable<DiscordOverwriteBuilder> overwrites = [overwriteEveryone, overwriteOwner];

        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels, overwrites: overwrites);
        var reg = NewRegistry(exclusion, channel.Id, ownerID);

        // TODO: Adicionar lógica para deletar o canal após o tempo de exclusão, usando o IScheduler

        return reg;
    }


    // TODO: Criar um método para deletar os canais temporários
    // TODO: Criar os métodos para os diferentes comandos de gerenciamento de canais temporários, como listar, deletar, etc.

    private TempChannelRegistry NewRegistry(DateTimeOffset exclusion, ulong channelID, ulong owner = 0)
    {
        TempChannelRegistry registry = new()
        {
            ExclusionTime = exclusion,
            ChannelID = channelID,
        };

        if (owner != 0)
        {
            registry.IsOwned = true;
            registry.OwnerID = owner;
        }
        else
        {
            registry.IsOwned = false;
            registry.OwnerID = 0;
        }

        data.TempChannels.Add(registry);
        return registry;
    }

    public bool UserCanCreateTempChannel(ulong userID) => data.TempChannels.Select(r => r.IsOwned && r.OwnerID == userID).Count() < config.TempChannelCountPerUser;    

}

public class ChannelManagerConfig
{
    // Configurações de canais temporários
    public ulong TempChannelRootCategoryID { get; set; } = 0;
    public string TempChannelNamePrefix { get; set; } = "•᲼ "; // Adicionado para todo canal temporário
    public string GeneralTempChannelNameSecPrefix { get; set; } = "🕙 "; // Adicionado para todo canal temporário geral, depois do prefixo do canal temporário
    public string DefaultGeneralTempChannelName { get; set; } = "Canal Temporário"; // Nome padrão para todo canal temporário geral, depois do prefixo do canal temporário
    public string OwnedTempChannelNameSecPrefix { get; set; } = "🕙 "; // Adicionado para todo canal temporário com dono, depois do prefixo do canal temporário    
    public string OwnedTempChannelName { get; set; } = "Canal de "; // Adicionado para todo canal temporário com dono, depois dos prefixos e antes do nome do dono
    public string PrivateTempChannelNameSecPrefix { get; set; } = "🔐 "; // Adicionado para todo canal temporário privado e com dono, depois do prefixo do canal temporário 
    public string PrivateTempChannelName { get; set; } = "Canal de "; // Adicionado para todo canal temporário privado e com dono, depois do prefixo do canal temporário 

    public uint TempChannelCountPerUser { get; set; } = 3;
    public bool DeleteOnlyEmptyTempChannels { get; set; } = true;
    public uint TempChannelMaxOverlifeTimeMs { get; set; } = 300000; // 5 minutes
}

public class ChannelManagerData
{
    [JsonInclude] public List<TempChannelRegistry> TempChannels = [];
}

public class TempChannelRegistry
{
    public DateTimeOffset ExclusionTime { get; set; }
    public ulong ChannelID { get; set; }
    public bool IsOwned { get; set; }
    public ulong OwnerID { get; set; }
}