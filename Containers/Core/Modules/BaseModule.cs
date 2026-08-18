using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

/*
 *  Um módulo base que implementa IModule e fornece funcionalidades básicas de configuração e persistência de dados.
 *  Cria uma configuração padrão se não houver uma existente e carrega os dados salvos do módulo.
 *  O usuário deve fornecer métodos para criar a instância de config e data default e definir o comportameno do módulo para as configs iniciais
 */

public abstract class BaseModule<Config, SavedData>(IPersistance persistance, IConfigPersistance configPersistance) : IModule
{
    protected Config config;
    protected SavedData data;

    protected IPersistance persistance = persistance;
    protected IConfigPersistance configPersistance = configPersistance;

    protected IServiceProvider services;
    protected IServerContext serverContext;

    public abstract string Name { get; }
    protected abstract bool ThrowExceptionOnMissingConfig { get; }

    public abstract Task ConfigureEventHandlers(EventHandlingBuilder ehb);
    public virtual Task ConfigureServices(IServiceCollection services) => Task.CompletedTask;

    public abstract IEnumerable<CommandBuilder> GetDynamicCommands();
    public abstract List<Type> GetStaticCommands();
    public abstract Task Start();

    public virtual Task PreStart_0() => Task.CompletedTask;
    public virtual Task PreStart_1() => Task.CompletedTask;
    public virtual Task PreStart_2() => Task.CompletedTask;


    public virtual async Task<bool> Initialize(IServerContext serverContext)
    {
        if (persistance == null)
            throw new Exception(((IModule) this).LogName + " IPersistance is not assigned to the module");

        if (configPersistance == null)
            throw new Exception(((IModule) this).LogName + " IConfigPersistance is not assigned to the module");

        this.serverContext = serverContext;        

        if (await configPersistance.ConfigExists(this))
        {
            await LoadConfig();
        }
        else
        {
            // Write default config            
            await configPersistance.WriteConfig(this, InitializeConfig());

            if (ThrowExceptionOnMissingConfig)
                throw new Exception(((IModule) this).LogName + " config not found. Please modify the standard one.");
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(((IModule) this).LogName + " config not found. Initializing with the default one.");
                Console.ResetColor();
            }                
        }

        data = InitializeData();
        await LoadData();

        return true;
    }       

    public virtual Task ReceiveServices(IServiceProvider serviceProvider)
    {
        services = serviceProvider;
        return Task.CompletedTask;
    }

    public virtual async Task<bool> SaveData()
    {
        await persistance.WriteObject(data, typeof(SavedData), Name + "Data");
        return true;
    }

    protected virtual async Task LoadData()
    {
        if (await persistance.KeyExists(Name + "Data.json"))
        {
            SavedData loadedData = (SavedData) await persistance.ReadObject(Name + "Data", typeof(SavedData));
            data = loadedData;
        }
    }

    protected virtual async Task LoadConfig()
    {
        Config loadedConfig = (Config) await configPersistance.LoadConfig(this, typeof(Config));
        config = loadedConfig;
    }


    // Util Methods    

    /// <summary>
    /// Responde a comando com uma mensagem de erro; Caso a mensagem tenha sido enviada por um Slash Command, a resposta é vista somente pelo usuário que mandou o comando
    /// OBS: Não pode se usar o DeferMessage antes para isso funcionar corretamente.
    /// </summary>
    /// <param name="ctx">O CommandContext do comando</param>
    /// <param name="response">A resposta para enviar</param>
    /// <returns></returns>
    public static async Task CommandErrorResponse(CommandContext ctx, string response)
    {
        try
        {
            if (ctx is SlashCommandContext slashCtx)
            {
                var responseBuilder = new DiscordFollowupMessageBuilder();
                responseBuilder.WithContent(response).AsEphemeral();
                await slashCtx.FollowupAsync(responseBuilder);
            }
            else
            {
                await ctx.RespondAsync(response);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }        
    }

    /// <summary>
    /// Verifica se o usuário do comando está conectado em alguma call do servidor
    /// </summary>
    /// <param name="ctx">O CommandContext do comando</param>
    /// <param name="discordChannel">O canal para comparar se o usuário está conectado</param>
    /// <param name="response">A resposta de erro caso o usuário não esteja conectado</param>
    /// <returns>Retorna true se o usuário estiver conectado em algum canal do server, retorna false caso contrário</returns>
    public async Task<bool> CommandVerifyMemberVoiceState(CommandContext ctx, string response = "Você deve estar conectado em um canal de voz para usar esse comando")
    {       
        if (ctx.Member.VoiceState == null || ctx.Member.VoiceState.GuildId == null || ctx.Member.VoiceState.GuildId != serverContext.BindedDiscordServer.Id || ctx.Member.VoiceState.ChannelId == null)
        {
            await CommandErrorResponse(ctx, response);
            return false;
        }

        return true;
    }


    // Usados para inicializar data e config; Data quando não nenhuma data é carregada. Config quando não existe nenhum config inicial.
    protected abstract SavedData InitializeData();
    protected abstract Config InitializeConfig();
}