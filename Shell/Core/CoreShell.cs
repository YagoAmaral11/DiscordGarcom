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

namespace GarçomDoKitts.Shell.Core;

public class CoreShell(IPersistance persistance, List<IModule> modules, ulong LinkedServerID) : IServerContext
{
    // Internal 
    public List<IModule> modules = modules;
    public IPersistance persistance = persistance;   
    private ulong linkedServerID = LinkedServerID;

    // Config Data
    public string Name => "Shell";
    public const string TokenAcessKey = "data/BotToken";
    public const string CommandPrefixesAcessKey = "CommandPrefixes";
    private List<string> TextCommandsPrefixes;

    // Runtime Data
    private string Token;
    private DateTime initialTime; public DateTime InitialTime => initialTime;            
    private DiscordClient discordClient; public DiscordClient shellDiscordClient => discordClient;
    private DiscordGuild shellGuild; public DiscordGuild connectedServer => shellGuild;
    public DiscordUser shellDiscordUser => shellDiscordClient.CurrentUser;
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

        // Inicializa módulos
        foreach (IModule module in modules)
        {
            await module.Initialize(this, services);
        }

        // Configura os event handlers dos eventos do bot
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
                extension.AddCommands(module.GetCommands());                
            }                   

            extension.AddCommands(this.GetCommands()); // Comandos nativos do CoreShell

        }        
        );

        // Começa a execução dos módulos
        foreach (IModule module in modules)
        {
            await module.Start();
        }

        // Inicializa o bot
        discordClient = dcClientBuilder.Build();
        await discordClient.ConnectAsync();                

        await readyToOperate.Task; // Aguarda o término da inicialização do bot

        // Finaliza inicialização
        initialTime = DateTime.Now;                                 

        // Verifica se o bot está linkada ao servidor específico
        if (discordClient.Guilds.TryGetValue(linkedServerID, out DiscordGuild val) == true)
        {
            shellGuild = val;
            return true;
        }
        else
        {
            return false;
        }            
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
    
    private List<Type> GetCommands()
    {
        return new List<Type>();
    }
    
}