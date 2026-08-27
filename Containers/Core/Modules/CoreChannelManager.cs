using DiscordGarçom.GarcModules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

public class CoreChannelManager(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : BaseModule<ChannelManagerConfig, ChannelManagerData>(persistance, configPersistance)
{
    public override string Name => "CoreChannelManager";
    protected override bool ThrowExceptionOnMissingConfig => true;
    
    private IScheduler scheduler = scheduler;    
    private DiscordChannel RootTempChannels;
    bool ready = false;

    protected override ChannelManagerData InitializeData() => new();
    protected override ChannelManagerConfig InitializeConfig() => new();   


    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb)
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

    public override List<Type> GetStaticCommands() => [];
    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {                
        CommandBuilder canaisTempCB = new();
        canaisTempCB.WithName("canaltemp");

        var createTempChannelCmd = CommandBuilder.From(CreateTemporaryChannelCmd).WithParent(canaisTempCB);                        
        var listTempChannelsCmd = CommandBuilder.From(ListTemporaryChannelsCmd).WithParent(canaisTempCB);        
        var deleteTempChannelCmd = CommandBuilder.From(DeleteTemporaryChannelCmd).WithParent(canaisTempCB);        

        canaisTempCB.WithSubcommands([createTempChannelCmd, listTempChannelsCmd, deleteTempChannelCmd]);

        return [canaisTempCB];
    }

            

    public override async Task PreStart_0()
    {
        RootTempChannels = await serverContext.BotDiscordClient.GetChannelAsync(config.TempChannelRootCategoryID);
        ready = true;
    }

    public override async Task Start()
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
    

    // COMANDOS
    [Command("criar")]
    [Description("Cria um canal temporário com tempo de vida passado")]
    public async Task CreateTemporaryChannelCmd(CommandContext ctx, [Description("Duração. Pode ser expresso nos formatos XXhYYmZZs ou XX:YY:ZZ")] TimeSpan duration
        , [Description("Se verdadeiro, só o dono consegue entrar, mas poderá puxar outros membros")] bool isPrivate = false, 
        [Description("O nome para o canal")] string name = null)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {            
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
                await CommandErrorResponse(ctx, $"Desculpe, mas você já atingiu o limite de canais temporários que pode criar ({config.TempChannelCountPerUser}). Apague um antes de criar outro.");                
            }
        }
        catch (Exception e)
        {
            await DumpException(e);
        }                       
    }

    [Command("listar")]
    [Description("Lista os canais temporários que você possui")]
    public async Task ListTemporaryChannelsCmd(CommandContext ctx)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {            
            var userChannels = data.TempChannels.Where(r => r.IsOwned && r.OwnerID == ctx.Member.Id).ToList();

            if (userChannels.Count == 0)
            {
                await CommandErrorResponse(ctx, "Você não possui canais temporários ativos.");                
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Seus canais temporários ativos:");
            foreach (var channel in userChannels)
            {
                var discordChannel = await serverContext.BindedDiscordServer.GetChannelAsync(channel.ChannelID);
                sb.AppendLine($"* {discordChannel.Mention} (Exclusão: {PrintDiscordRelativeTime(channel.ExclusionTime)} em {PrintDiscordTime(channel.ExclusionTime, 'f')})");
            }

            await ctx.RespondAsync(sb.ToString());
        }
        catch (Exception e)
        {
            await DumpException(e);
        }        
    }

    [Command("deletar")]
    [Description("Deleta um canal temporário que você possui")]
    public async Task DeleteTemporaryChannelCmd(CommandContext ctx, [Description("ID, link ou menção do canal.")] DiscordChannel channel)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {            
            var registry = data.TempChannels.FirstOrDefault(r => r.ChannelID == channel.Id && r.IsOwned && r.OwnerID == ctx.Member.Id);

            if (registry != null)
            {
                string tmpName = channel.Name;
                await DeleteTempChannel(channel.Id);
                await ctx.RespondAsync($"Canal temporário {tmpName} deletado com sucesso!");
            }
            else
            {
                await CommandErrorResponse(ctx, "Você não possui permissão para deletar este canal temporário");                
            }
        }
        catch (Exception e)
        {
            await DumpException(e);
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