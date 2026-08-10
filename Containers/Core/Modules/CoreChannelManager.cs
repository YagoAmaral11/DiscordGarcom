using DiscordGarçom.GarcModules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

public class CoreChannelManager(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : IModule
{
    public string Name => "CoreChannelManager";

    private IPersistance persistance = persistance;
    private IConfigPersistance configPersistance = configPersistance;
    private IScheduler scheduler = scheduler;
    private IServerContext serverContext;

    private ChannelManagerConfig config;
    private ChannelManagerData data = new();

    private DiscordChannel RootTempChannels;

    bool ready = false;

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        ehb.HandleChannelDeleted((_, deletionArgs) =>
            {
                if (deletionArgs.Guild == serverContext.BindedDiscordServer && data.TempChannels.Any(reg => reg.ChannelID == deletionArgs.Channel.Id))
                {
                    data.TempChannels.RemoveAll(reg => reg.ChannelID == deletionArgs.Channel.Id);
                }

                return Task.CompletedTask;
            }
        );

        return Task.CompletedTask;
    }

    public IEnumerable<CommandBuilder> GetDynamicCommands()
    {                
        CommandBuilder canaisTempCB = new();
        canaisTempCB.WithName("canaltemp");

        var createTempChannelCmd = CommandBuilder.From(CreateTemporaryChannelCmd).WithDescription("Cria um canal temporário com tempo de vida passado").WithParent(canaisTempCB);
        createTempChannelCmd.Parameters[0].Description = "Duração. Pode ser expresso nos formatos XXhYYmZZs ou XX:YY:ZZ";
        createTempChannelCmd.Parameters[1].Description = "Se sim, apenas o dono pode, mas poderá puxar outros membros";
        createTempChannelCmd.Parameters[2].Description = "O nome para o canal";        

        var listTempChannelsCmd = CommandBuilder.From(ListTemporaryChannelsCmd).WithDescription("Lista os canais temporários que você possui").WithParent(canaisTempCB);        

        var deleteTempChannelCmd = CommandBuilder.From(DeleteTemporaryChannelCmd).WithDescription("Deleta um canal temporário que você possui").WithParent(canaisTempCB);
        deleteTempChannelCmd.Parameters[0].Description = "ID, link ou menção do canal. Canais de voz podem ser mencionados com #!";

        canaisTempCB.WithSubcommands([createTempChannelCmd, listTempChannelsCmd, deleteTempChannelCmd]);

        return [canaisTempCB];
    }

    public List<Type> GetStaticCommands() => [];    


    public async Task<bool> Initialize(IServerContext serverContext)
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

    public Task ReceiveServices(IServiceProvider serviceProvider) => Task.CompletedTask;

    public async Task PreStart_0()
    {
        RootTempChannels = await serverContext.BotDiscordClient.GetChannelAsync(config.TempChannelRootCategoryID);
        ready = true;
    }

    public async Task Start()
    {
        // Remove qualquer registro de canal temporário que não exista mais no servidor
        var channels = await serverContext.BindedDiscordServer.GetChannelsAsync();
        var channelDic = channels.ToDictionary(c => c.Id, c => c);

        foreach (var reg in data.TempChannels.ToList())
        {
            if (channelDic.ContainsKey(reg.ChannelID) == false)
            {
                data.TempChannels.Remove(reg);
            }
        }
    }


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

    public async Task<bool> SaveData()
    {
        await persistance.WriteObject(data, typeof(ChannelManagerData), Name + "Data");
        return true;
    }



    public async Task<(TempChannelRegistry reg, DiscordChannel channel)> NewGeneralTempChannel(DateTimeOffset exclusion, string name = null)
    {
        if (!ready)
            return (null, null);

        name ??= config.GeneralTempChannelNameSecPrefix + config.DefaultGeneralTempChannelName;

        string reason = "Creating new temp channel " + name;
        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels, reason: reason);
        var reg = NewRegistry(exclusion, channel.Id);

        scheduler.ScheduleCallback(DeleteTempChannel, [(ulong) channel.Id], (ulong) DateTimeOffset.Now.Ticks, exclusion, false);

        return (reg, channel);
    }

    public async Task<(TempChannelRegistry reg, DiscordChannel channel)> NewOwnedTempChannel(DateTimeOffset exclusion, ulong ownerID, string name = null)
    {
        if (!ready)
            return (null, null);

        DiscordMember owner = await serverContext.BindedDiscordServer.GetMemberAsync(ownerID);

        if (UserCanCreateTempChannel(ownerID) == false)
            return (null, null);

        name ??= config.OwnedTempChannelNameSecPrefix + config.OwnedTempChannelName + owner.DisplayName;

        string reason = "Creating temp channel for user " + owner.DisplayName + " (User ID: " + owner.Id + ")";
        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels, reason: reason);
        var reg = NewRegistry(exclusion, channel.Id, ownerID);

        scheduler.ScheduleCallback(DeleteTempChannel, [channel.Id], (ulong) DateTimeOffset.Now.Ticks, exclusion, false);

        return (reg, channel);
    }

    public async Task<(TempChannelRegistry reg, DiscordChannel channel)> NewPrivateTempChannel(DateTimeOffset exclusion, ulong ownerID, string name = null)
    {
        if (!ready)
            return (null, null);

        DiscordMember owner = await serverContext.BindedDiscordServer.GetMemberAsync(ownerID);

        if (UserCanCreateTempChannel(ownerID) == false)
            return (null, null);

        name ??= config.PrivateTempChannelNameSecPrefix + config.PrivateTempChannelName + owner.DisplayName;

        DiscordOverwriteBuilder overwriteEveryone = new DiscordOverwriteBuilder(serverContext.BindedDiscordServer.EveryoneRole);
        overwriteEveryone.Deny(DiscordPermission.SendMessages);
        overwriteEveryone.Deny(DiscordPermission.Connect);

        DiscordOverwriteBuilder overwriteOwner = new DiscordOverwriteBuilder(owner);
        overwriteOwner.Allow(DiscordPermission.Connect);
        overwriteOwner.Allow(DiscordPermission.MoveMembers);

        IEnumerable<DiscordOverwriteBuilder> overwrites = [overwriteEveryone, overwriteOwner];

        string reason = "Creating private temp channel for user " + owner.DisplayName + " (User ID: " + owner.Id + ")";
        DiscordChannel channel = await serverContext.BindedDiscordServer.CreateVoiceChannelAsync(config.TempChannelNamePrefix + name, parent: RootTempChannels, overwrites: overwrites, reason: reason);
        var reg = NewRegistry(exclusion, channel.Id, ownerID);

        scheduler.ScheduleCallback(DeleteTempChannel, [channel.Id], (ulong) DateTimeOffset.Now.Ticks, exclusion, false);

        return (reg, channel);
    }


    public async Task DeleteTempChannel(ulong channelID)
    {
        // Verifica se o canal existe no registro
        if (data.TempChannels.Where(reg => reg.ChannelID == channelID).Any())
        {
            data.TempChannels.RemoveAll(reg => reg.ChannelID == channelID);
        }

        // Deleta o canal do Discord
        DiscordChannel channel = null; 

        try
        {
            channel = await serverContext.BindedDiscordServer.GetChannelAsync(channelID);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Could not find channel : {channelID}, " + e.Message);
        }

        if (channel != null)
        {
            if (channel.Users.Count == 0)
            {
                await channel.DeleteAsync(Name + " deleting temp channel " + channel.Name + " (ID: " + channel.Id + ")");                
            }
            else
            {
                // Tenta deletar o canal novamente após o tempo de exclusão máximo, caso ainda haja usuários no canal
                scheduler.ScheduleCallback(DeleteTempChannel, [channelID], (ulong) DateTimeOffset.Now.Ticks, DateTimeOffset.Now.AddMilliseconds(config.TempChannelMaxOverlifeTimeMs), false);
            }
        }
    }


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

    public bool UserCanCreateTempChannel(ulong userID) => data.TempChannels.Where(r => r.IsOwned && r.OwnerID == userID).Count() < config.TempChannelCountPerUser;

    
    [Command("criar")]    
    public async Task CreateTemporaryChannelCmd(CommandContext ctx, TimeSpan duration, bool isPrivate = false, string name = null)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            await ctx.DeferResponseAsync();

            DateTimeOffset exclusionTime = DateTimeOffset.Now.Add(duration);

            if (UserCanCreateTempChannel(ctx.Member.Id))
            {
                if (isPrivate == false)
                {
                    var resp = await NewOwnedTempChannel(exclusionTime, ctx.Member.Id, name);
                    await ctx.RespondAsync($"Canal temporário {resp.Item2.Mention} criado com sucesso!");
                }
                else
                {
                    var resp = await NewPrivateTempChannel(exclusionTime, ctx.Member.Id, name);
                    await ctx.RespondAsync($"Canal privado {resp.Item2.Mention} criado com sucesso!");
                }
            }
            else
            {
                await ctx.RespondAsync($"Desculpe, mas você já atingiu o limite de canais temporários que pode criar ({config.TempChannelCountPerUser}). Delete um antigo antes de criar outro.");
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }                       
    }

    [Command("listar")]
    public async Task ListTemporaryChannelsCmd(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            await ctx.DeferResponseAsync();

            var userChannels = data.TempChannels.Where(r => r.IsOwned && r.OwnerID == ctx.Member.Id).ToList();

            if (userChannels.Count == 0)
            {
                await ctx.RespondAsync("Você não possui canais temporários ativos.");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Seus canais temporários ativos:");
            foreach (var channel in userChannels)
            {
                var discordChannel = await serverContext.BindedDiscordServer.GetChannelAsync(channel.ChannelID);
                sb.AppendLine($"* {discordChannel.Mention} (Exclusão: {channel.ExclusionTime})");
            }

            await ctx.RespondAsync(sb.ToString());
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }        
    }

    [Command("deletar")]
    public async Task DeleteTemporaryChannelCmd(CommandContext ctx, DiscordChannel channel)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            await ctx.DeferResponseAsync();

            var registry = data.TempChannels.FirstOrDefault(r => r.ChannelID == channel.Id && r.IsOwned && r.OwnerID == ctx.Member.Id);

            if (registry != null)
            {
                string tmpName = channel.Name;
                await DeleteTempChannel(channel.Id);
                await ctx.RespondAsync($"Canal temporário {tmpName} deletado com sucesso!");
            }
            else
            {
                await ctx.RespondAsync("Você não possui permissão para deletar este canal temporário.");
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }                        
    }

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