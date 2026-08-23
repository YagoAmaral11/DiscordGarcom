using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordGarçom.Garc_Modules;

public class Utility(IPersistance persistance, IConfigPersistance configPersistance) : BaseModule<UtilityConfig, UtilityData>(persistance, configPersistance)
{
    public override string Name => "Utility";

    protected override bool ThrowExceptionOnMissingConfig => true;

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;    

    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        CommandBuilder utility = new();
        utility.WithName("Utility");

        var deafenMention = CommandBuilder.From(DeafenMention).WithParent(utility);
        var userCount = CommandBuilder.From(UserCount).WithParent(utility);
        var userMove = CommandBuilder.From(UserMove).WithParent(utility);

        utility.WithSubcommands([deafenMention, userCount, userMove]);

        return [utility];
    }

    public override List<Type> GetStaticCommands() => [];    

    public override Task Start() => Task.CompletedTask;    

    protected override UtilityConfig InitializeConfig() => new();   
    protected override UtilityData InitializeData() => new();


    [Command("mencionardeafen")]
    [Description("Menciona todos os usuários que estão com o audio desativado")]
    // TODO: Adicionar um cooldown para esse comando, para evitar spam
    public async Task DeafenMention(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {            
            DiscordMember pedinte = ctx.Member;
            DiscordChannel canalDeVoz;                        

            if (ctx.Member.VoiceState != null && ctx.Member.VoiceState.ChannelId != null)
            {
                canalDeVoz = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
            }
            else
            {
                await ctx.RespondAsync("Você deve estar em um canal de voz para usar esse comando");
                return;
            }


            string mentions = string.Empty;

            foreach (var user in canalDeVoz.Users)
            {
                if (user.VoiceState.IsSelfDeafened)
                {
                    mentions += $"{user.Mention} ";
                }
            }

            if (mentions != string.Empty)
            {
                await ctx.RespondAsync("Mencionando usuários...");
                await ctx.Channel.SendMessageAsync(mentions);
            }
            else
            {
                await ctx.RespondAsync("Nenhum usuário elegível");
            }            
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("contar")]
    [Description("Conta os usuários em uma call ou chat")]
    public async Task UserCount(CommandContext ctx, [Description("Canal para contar usuários")] DiscordChannel canal = null)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            if (canal == null)
            {
                if (ctx.Member.VoiceState != null && ctx.Member.VoiceState.ChannelId != null)
                {
                    canal = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
                }
                else
                {
                    await ctx.RespondAsync("Você deve estar em um canal de voz ou especificar um canal para usar esse comando");
                    return;
                }
            }

            int userCount = 0;
            int botCount = 0;
            int totalCount = 0;

            foreach (var user in canal.Users)
            {
                if (user.IsBot)
                {
                    botCount++;
                }
                else
                {
                    userCount++;
                }

                totalCount++;
            }

            await ctx.RespondAsync($"Usuários: {userCount}, Bots: {botCount}, Total: {totalCount}");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("mover")]
    [Description("Move todos os usuários de uma call para outra")]
    public async Task UserMove(CommandContext ctx, [Description("Call de destino")] DiscordChannel destino, [Description("Move os usuários dessa call")] DiscordChannel origem = null)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            await ctx.DeferResponseAsync();

            if (destino.Type != DiscordChannelType.Voice)
            {
                await ctx.RespondAsync("O destino deve ser um canal de voz");
                return;
            }

            if (origem == null)
            {
                if (ctx.Member.VoiceState != null && ctx.Member.VoiceState.ChannelId != null)
                {
                    origem = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
                }
                else
                {
                    await ctx.RespondAsync("Você deve estar em um canal de voz ou especificar um canal de origem para usar esse comando");
                    return;
                }
            }

            foreach (var user in origem.Users)
            {                
                await destino.PlaceMemberAsync(user);
            }
            await ctx.RespondAsync($"Todos os usuários de {origem.Mention} foram movidos para {destino.Mention}");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }
    
    [Command("shake")]
    [Description("Move um usuário repetidamente entre duas calls por um tempo")]
    public async Task UserShake(CommandContext ctx, [Description("Usuário para ser movido")] DiscordMember usuario)
    {
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            if (await CommandVerifyMemberVoiceState(ctx) == false)
            {                
                return;
            }

            if (usuario.VoiceState != null && usuario.VoiceState.ChannelId != null)
            {
                var firstChannel = await serverContext.BindedDiscordServer.GetChannelAsync(usuario.VoiceState.ChannelId.Value);
                var otherChannel = await serverContext.BindedDiscordServer.GetChannelAsync(config.IntermediaryChannel);

                for (int i = 0; i < config.ShakeMoveTimes; i++)
                {
                    await otherChannel.PlaceMemberAsync(usuario);
                    await Task.Delay(200);
                    await firstChannel.PlaceMemberAsync(usuario);
                }
            }
            else
            {
                await CommandErrorResponse(ctx, $"O usuário {usuario.Username} não está em call");
                return;
            }

            await ctx.RespondAsync("Usuário chacoalhado com sucesso");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

}

public class UtilityConfig
{
    public ulong IntermediaryChannel { get; set; } = 0;
    public uint ShakeMoveTimes { get; set; } = 5;
}

public class UtilityData
{
}