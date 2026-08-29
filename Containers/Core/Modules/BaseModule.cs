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

    // Usados para inicializar data e config; Data quando não nenhuma data é carregada. Config quando não existe nenhum config inicial.
    protected abstract SavedData InitializeData();
    protected abstract Config InitializeConfig();


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


    // Helper Methods    
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
                await slashCtx.RespondAsync(responseBuilder);                
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
    /// <param name="member">O Usuário para verificar se está conectado. Se null, usará o dono do comando</param>
    /// <returns>Retorna true se o usuário estiver conectado em algum canal do server, retorna false caso contrário</returns>
    public async Task<bool> CommandVerifyMemberVoiceState(CommandContext ctx, string response = "Você deve estar conectado em um canal de voz para usar esse comando", DiscordMember member = null)
    {       
        member ??= ctx.Member;

        if (member.VoiceState == null || member.VoiceState.GuildId == null || member.VoiceState.GuildId != serverContext.BindedDiscordServer.Id || member.VoiceState.ChannelId == null)
        {
            await CommandErrorResponse(ctx, response);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifica se o bot está preparado para receber comandos
    /// </summary>
    /// <param name="ctx">O CommandContext do comando</param>
    /// <returns>Retorna false se o bot ainda não estiver pronto para receber comandos</returns>
    public async Task<bool> CommandReadyPreCondition(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return false;
        }

        return true;
    }

    public async Task<bool> CommandVerifyMovePermission(CommandContext ctx, DiscordMember user, DiscordChannel channel)
    {
        if (user.Permissions.HasPermission(DiscordPermission.Administrator))
            return true;

        if (channel.PermissionsFor(user).HasPermission(DiscordPermission.MoveMembers))
            return true;

        await CommandErrorResponse(ctx, $"Sem permissões para realizar esse comando");
        return false;
    }

    /// <summary>
    /// Divides a collection into pages and retrieves the items on the specified page.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="list">The collection of items to paginate. Cannot be <see langword="null"/>.</param>
    /// <param name="validPage">When this method returns, contains <see langword="true"/> if the specified page exists; otherwise, <see
    /// langword="false"/>.</param>
    /// <param name="pageFirstElementIndex">The index of the first element on the specified page</param>
    /// <param name="pageLastElementIndex">The index of the last element on the specified page</param>
    /// <param name="pageCount">The amount of pages available</param>
    /// <param name="page">The page number to retrieve. Must be greater than or equal to 1. Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Must be greater than or equal to 1. Defaults to 10.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing the items on the specified page. If the page does not exist, returns
    /// an null collection.</returns>
    public static IEnumerable<T> Paginate<T>(IEnumerable<T> list, out bool validPage, out int pageFirstElementIndex, out int pageLastElementIndex, out int pageCount, int page = 1, int pageSize = 10)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentOutOfRangeException.ThrowIfLessThan<int>(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        int listCount = list.Count();

        if (listCount == 0)
        {
            validPage = false;
            pageFirstElementIndex = -1;
            pageLastElementIndex = -1;
            pageCount = 0;
            return null;
        }

        pageCount = (int) Math.Ceiling((double) listCount / pageSize);        

        pageFirstElementIndex = pageSize * (page - 1);
        if (pageFirstElementIndex < 0)
            pageFirstElementIndex = 0;

        int finalElement = listCount - 1;

        if (finalElement < pageFirstElementIndex)
        {
            validPage = false;
            pageFirstElementIndex = -1;
            pageLastElementIndex = -1;
            return null;
        }

        pageLastElementIndex = pageFirstElementIndex + pageSize - 1;

        if (pageLastElementIndex > finalElement)
            pageLastElementIndex = finalElement;

        validPage = true;
        int rangeLast = pageLastElementIndex + 1;
        return list.ToList()[pageFirstElementIndex..rangeLast];
    }

    public string PrintDiscordRelativeTime(DateTimeOffset date) => $"<t:" + date.ToUnixTimeSeconds() + ":R>";
    public string PrintDiscordTime(DateTimeOffset date, char type) => $"<t:" + date.ToUnixTimeSeconds() + ":" + type + ">";

    public async static Task<bool> DumpException(IModule module, Exception e, IPersistance persistance)
    {
        DateTimeOffset time = DateTimeOffset.Now;
        Console.WriteLine(module.LogName + $" Error at time {time.ToString()}: " + e);
        string filename = $"{time.Date.Year}.{time.Date.Month}.{time.Date.Day}_{time.Hour}.{time.Minute}.{time.Second}.{time.Millisecond}_UTC{time.Offset.ToString().Replace(':', '-')}";

        try
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"{e.HResult}: {e.Source} | " + e.Message);
            stringBuilder.AppendLine(e.StackTrace);
            var str = stringBuilder.ToString();

            await persistance.WriteRaw(str, "ErrorDumps/" + filename, ".txt");
        }
        catch (Exception e2)
        {
            Console.WriteLine(module.LogName + $" Error trying to dump exception: " + e2);
            return false;
        }

        return true;
    }

    public async Task<bool> DumpException(Exception e)
    {
        var module = ((IModule) this);

        DateTimeOffset time = DateTimeOffset.Now;
        Console.WriteLine(module.LogName + $" Error at time {time.ToString()}: " + e);
        string filename = $"{time.Date.Year}.{time.Date.Month}.{time.Date.Day}_{time.Hour}.{time.Minute}.{time.Second}.{time.Millisecond}_UTC{time.Offset.ToString().Replace(':', '-')}";

        try
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine($"{e.HResult}: {e.Source} | " + e.Message);
            stringBuilder.AppendLine(e.StackTrace);
            var str = stringBuilder.ToString();

            await persistance.WriteRaw(str, "ErrorDumps/" + filename, ".txt");
        }
        catch (Exception e2)
        {
            Console.WriteLine(module.LogName + $" Error trying to dump exception: " + e2);
            return false;
        }

        return true;
    }    
}