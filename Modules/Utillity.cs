using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace DiscordGarçom.Garc_Modules;

public class Utility(IPersistance persistance, IConfigPersistance configPersistance) : BaseModule<UtilityConfig, UtilityData>(persistance, configPersistance)
{
    public override string Name => "Utility";

    protected override bool ThrowExceptionOnMissingConfig => true;

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;    

    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        var deafenMention = CommandBuilder.From(DeafenMention);
        var userCount = CommandBuilder.From(UserCount);
        var userMove = CommandBuilder.From(UserMove);
        var userShake = CommandBuilder.From(UserShake);
        
        return [deafenMention, userCount, userMove, userShake];
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
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {            
            DiscordMember pedinte = ctx.Member;
            DiscordChannel canalDeVoz;

            if (!await CommandVerifyMemberVoiceState(ctx))
                return;

            canalDeVoz = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);

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
                await CommandErrorResponse(ctx, "Nenhum usuário elegível");                
            }            
        }
        catch (Exception e)
        {
            await DumpException(e);
        }
    }

    [Command("contar")]
    [Description("Conta os usuários em uma call ou chat")]
    public async Task UserCount(CommandContext ctx, [Description("Canal para contar usuários")] DiscordChannel canal = null)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {
            if (canal == null)
            {
                if (!await CommandVerifyMemberVoiceState(ctx))
                {                    
                    return;
                }

                canal = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
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
            await DumpException(e);
        }
    }

    [Command("mover")]
    [Description("Move todos os usuários de uma call para outra")]
    public async Task UserMove(CommandContext ctx, [Description("Call de destino")] DiscordChannel destino, [Description("Move os usuários dessa call")] DiscordChannel origem = null)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {                        
            if (destino.Type != DiscordChannelType.Voice)
            {
                await CommandErrorResponse(ctx, "O destino deve ser um canal de voz");                
                return;
            }            

            if (origem == null)
            {
                if (!await CommandVerifyMemberVoiceState(ctx))
                    return;

                origem = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
            }

            if ((await CommandVerifyMovePermission(ctx, ctx.Member, origem) && await CommandVerifyMovePermission(ctx, ctx.Member, destino)) == false)
                return;

            await ctx.DeferResponseAsync();
            foreach (var user in origem.Users)
            {                
                await destino.PlaceMemberAsync(user);
            }
            await ctx.RespondAsync($"Todos os usuários de {origem.Mention} foram movidos para {destino.Mention}");
        }
        catch (Exception e)
        {
            await DumpException(e);
        }
    }
    
    [Command("shake")]
    [Description("Move um usuário repetidamente entre duas calls por um tempo")]
    public async Task UserShake(CommandContext ctx, [Description("Usuário para ser movido")] DiscordMember usuario)
    {
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {
            if (await CommandVerifyMemberVoiceState(ctx) == false)
            {                
                return;
            }

            if (!await CommandVerifyMovePermission(ctx, ctx.Member, await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value)))
                return;

            if (usuario.VoiceState != null && usuario.VoiceState.ChannelId != null)
            {
                var firstChannel = await serverContext.BindedDiscordServer.GetChannelAsync(usuario.VoiceState.ChannelId.Value);
                var otherChannel = await serverContext.BindedDiscordServer.GetChannelAsync(config.IntermediaryChannel);

                await ctx.DeferResponseAsync();
                for (int i = 0; i < config.ShakeMoveTimes; i++)
                {
                    await otherChannel.PlaceMemberAsync(usuario);
                    await Task.Delay(200);
                    await firstChannel.PlaceMemberAsync(usuario);
                }
            }
            else if (usuario.VoiceState == null)
            {
                await CommandErrorResponse(ctx, $"O usuário {usuario.Username} não está em call");
                return;
            }
            else if (usuario.VoiceState.ChannelId != ctx.Member.VoiceState.ChannelId)
            {
                await CommandErrorResponse(ctx, $"O usuário {usuario.Username} não está na mesma call que você");
                return;
            }            

            await ctx.RespondAsync("Usuário chacoalhado com sucesso");
        }
        catch (Exception e)
        {
            await DumpException(e);
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