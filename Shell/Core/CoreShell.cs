using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GarçomDoKitts.Shell.Core.DSharpPlusAdapters;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Trees.Metadata;

namespace GarçomDoKitts.Shell.Core;

public class CoreShell(IPersistance persistance, List<IModule> modules, ulong LinkedServerID) : IServerContext
{
    // Internal 
    public List<IModule> modules = modules;
    public IPersistance persistance = persistance;   
    private readonly ulong linkedServerID = LinkedServerID;

    // Config Data
    public static string Name => "Shell";
    public const string TokenAcessKey = "data/BotToken";
    public const string CommandPrefixesAcessKey = "CommandPrefixes";
    private List<string> TextCommandsPrefixes;

    // Runtime Data
    private string Token;
    private DateTime initialTime; public DateTime InitialTime => initialTime;            
    private DiscordClient discordClient; public DiscordClient BotDiscordClient => discordClient;
    private DiscordGuild shellGuild; public DiscordGuild BindedDiscordServer => shellGuild;
    public DiscordUser BotDiscordUser => BotDiscordClient.CurrentUser;
    private IServiceProvider services;

    // Public Methods
    public async Task<bool> Start()
    {
        if (persistance is null)
        {
            throw new Exception("Persistance is not assigned");
        }

        if (modules.Count == 0)
        {
            Console.WriteLine("Warning: Launching shell without modules");
        }

        return await Initialize(null, null);
    }

    public async Task Stop()
    {
        Console.Write("Stopping shell");
        await Shutdown();
    }

    // Methods
    private async Task<bool> Initialize(IServerContext context, IServiceProvider serviceProvider)
    {        
        if (persistance == null)
        {
            throw new Exception("Persistance is not assigned");
        }        

        // Obtem tokens e comandos
        Token = persistance.ReadRaw(TokenAcessKey).Result;        
        TextCommandsPrefixes = persistance.ReadObject(CommandPrefixesAcessKey, typeof(List<string>)).Result as List<string>;

        if (TextCommandsPrefixes == null || TextCommandsPrefixes.Count == 0)
        {
            throw new Exception("No text command prefixes configured");
        }

        if (String.IsNullOrWhiteSpace(Token))
        {
            throw new Exception("Bot Token not found");
        }

        // Inicializa discord client
        DiscordClientBuilder dcClientBuilder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.All);

        // Usada depois para aguardar o término da inicialização
        TaskCompletionSource<bool> readyToOperate = new();

        // Verificar módulos duplicados
        VerifyDuplicatedModules();

        // Inicializa módulos
        foreach (IModule module in modules)
        {
            await module.Initialize(this, services);
        }

        // Configura os event handlers dos eventos do bot
        // Configura a Task que indica o término da inicialização do bot
        dcClientBuilder.ConfigureEventHandlers(eventHandlers =>
        {
            // Inicializa os event handlers dos módulos
            foreach (IModule module in modules)
            {
                module.ConfigureEventHandlers(eventHandlers);
            }

            eventHandlers.HandleGuildDownloadCompleted
            (
                (_, _) =>
                {
                    readyToOperate.SetResult(true);           
                    return Task.CompletedTask;
                }
            );            
        });

        // Cria os processadores de comandos
        TextCommandProcessor textCommandProcessor = new(new() { PrefixResolver = new CustomPrefixResolver(false, TextCommandsPrefixes).ResolvePrefixAsync });
        SlashCommandProcessor slashCommandProcessor = new(new());        
        
        // Adiciona e registra os comandos do bot
        dcClientBuilder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
        {
            serviceProvider = services;
            
            extension.AddProcessor(textCommandProcessor);
            extension.AddProcessor(slashCommandProcessor);            

            // Registra os comandos dos módulos
            foreach(IModule module in modules)
            {
                extension.AddCommands(module.GetStaticCommands());
                extension.AddCommands(module.GetDynamicCommands());                
            }                   

            extension.AddCommands(this.GetCommands()); // Comandos nativos do CoreShell            
        }        
        );                

        // Inicializa o bot
        discordClient = dcClientBuilder.Build();
        await discordClient.ConnectAsync();                

        await readyToOperate.Task; // Aguarda o término da inicialização do bot        

        // Verifica se o bot está linkado ao servidor específico
        if (discordClient.Guilds.TryGetValue(linkedServerID, out DiscordGuild val) == false)
            return false;

        // TODO: Verificar se tem alguma forma de bloquear a execução de comandos até o término da inicialização; Bloquear aqui

        // Finaliza inicialização, preenche o Server context
        shellGuild = val;        
        initialTime = DateTime.Now;

        // Começa a execução dos módulos
        foreach (IModule module in modules)
        {
            await module.Start();
        }

        // TODO: Verificar se tem alguma forma de bloquear a execução de comandos até o término da inicialização; Desbloquear aqui

        return true;
    }

    private bool VerifyDuplicatedModules()
    {
        HashSet<Type> modulesTypes = new();

        foreach (IModule module in modules)
        {
            object moduleObject = module;
            Type type = moduleObject.GetType();

            if (modulesTypes.Contains(type))
            {
                Console.WriteLine($"Error: Duplicated module detected: {module.Name}");
                throw new Exception($"{module.Name} is duplicated");                
            }

            modulesTypes.Add(type);
        }

        return true;
    }

    private async Task SaveData()
    {
        foreach (IModule module in modules)
        {
            await module.SaveData();
        }        
    }

    private async Task<bool> Shutdown()
    {
        foreach (IModule module in modules)
        {
            await module.Shutdown();
        }

        return true;
    }

    private List<Type> GetCommands() => [];
    
}