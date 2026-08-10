using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using DiscordGarçom.Containers.Core.DSharpPlusAdapters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using DSharpPlus.Clients;
using DSharpPlus.Extensions;

namespace DiscordGarçom.Containers.Core;

public class SimpleContainer(IPersistance persistance, IEnumerable<IModule> modules, ulong LinkedServerID) : IServerContext
{
    // Internal 
    public IList<IModule> modules = modules.ToList();
    public IPersistance persistance = persistance;   
    private readonly ulong linkedServerID = LinkedServerID;
    private bool readyForCommands = false; 

    // Config Data
    public static string Name => "SimpleContainer";
    public const string TokenAcessKey = "data/BotToken";
    public const string CommandPrefixesAcessKey = "CommandPrefixes";
    private List<string> TextCommandsPrefixes;

    // Runtime Data
    private string Token;
    private DateTimeOffset initialTime; public DateTimeOffset InitialTime => initialTime;            
    private DiscordClient discordClient; public DiscordClient BotDiscordClient => discordClient;
    private DiscordGuild shellGuild; public DiscordGuild BindedDiscordServer => shellGuild;
    public DiscordUser BotDiscordUser => BotDiscordClient.CurrentUser;
    public bool ReadyForCommands => readyForCommands;    


    private List<Type> GetCommands() => [];



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

    private async Task<bool> Initialize(IServerContext context, IServiceProvider serviceProvider)
    {
        if (persistance == null)
        {
            throw new Exception("Persistance is not assigned");
        }

        // Garante que o método de parada seja chamado quando o processo for encerrado
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        Console.CancelKeyPress += Console_CancelKeyPress;

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


        
        DiscordClientBuilder dcClientBuilder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.All);                
        TaskCompletionSource<bool> readyToOperate = new(); // Usada depois para aguardar o término da inicialização                                                           
        VerifyDuplicatedModules();

        // Inicializa módulos
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Initializing Modules");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.Initialize(this);
        }
        Console.WriteLine("[BotContainer]" + " Finalized modules init");

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
            extension.AddProcessor(textCommandProcessor);
            extension.AddProcessor(slashCommandProcessor);                        

            // Registra os comandos dos módulos
            foreach (IModule module in modules)
            {
                extension.AddCommands(module.GetStaticCommands());
                extension.AddCommands(module.GetDynamicCommands());
            }

            extension.AddCommands(this.GetCommands()); // Comandos nativos do SimpleContainer            
        }
        );


        // Adiciona/Inicializa serviços
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Registering Services");
        Console.ResetColor();

        dcClientBuilder.ConfigureServices(async serviceCollection =>
        {
            foreach (IModule module in modules)
            {
                await module.ConfigureServices(serviceCollection);
            }
        });              

        Console.WriteLine("[BotContainer]" + " Services Registered & Started");


        // Cria o Client com os serviços passados e conecta com o Discord
        discordClient = dcClientBuilder.Build();        
        await discordClient.ConnectAsync();
        await readyToOperate.Task; // Aguarda o término da inicialização do bot        

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Connected to discord");
        Console.ResetColor();                               


        // Passa o ServiceProvider
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Sending Services");
        Console.ResetColor();

        foreach (IModule module in modules)
        {
            await module.ReceiveServices(discordClient.ServiceProvider);
        }

        Console.WriteLine("[BotContainer]" + " Services Sent");


        // Verifica se o bot está linkado ao servidor específico
        if (discordClient.Guilds.TryGetValue(linkedServerID, out DiscordGuild val) == false)
            return false;        

        // Finaliza inicialização, preenche o Server context
        shellGuild = val;
        initialTime = DateTimeOffset.Now;

        // Pre-começa a execução dos módulos
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Pre-Starting stage 0 start");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.PreStart_0();            
        }
        Console.WriteLine("[BotContainer]" + " Finalized Pre-Start 0");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Pre-Starting stage 1 start");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.PreStart_1();            
        }
        Console.WriteLine("[BotContainer]" + " Finalized Pre-Start 1");

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Pre-Starting stage 2 start");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.PreStart_2();            
        }
        Console.WriteLine("[BotContainer]" + " Finalized Pre-Start 2");

        // Começa a execução dos módulos

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Starting Modules");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.Start();            
        }

        readyForCommands = true;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Started all modules. Bot is running.");
        Console.ResetColor();        

        return true;
    }

    public async Task Stop()
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine("Stopping container");
        Console.ResetColor();

        await SaveData();
        await Shutdown();
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
        // No Shutdown dos módulos, já devem salvar seus dados
        foreach (IModule module in modules)
        {
            await module.Shutdown();
        }

        return true;
    }



    public bool TryGetModule<T>(out T Module) where T : IModule
    {
        Module = default(T);

        foreach (var module in modules)
        {
            if (module.GetType() is T)
            {
                Module = (T) module;
                return true;
            }
        }
        return false;
    }
    public T GetModule<T>() where T : IModule
    {
        foreach (var module in modules)
        {
            if (module.GetType() is T)
            {
                return (T) module;
            }
        }

        throw new KeyNotFoundException("Could not find any module of the type " + typeof(T).AssemblyQualifiedName);
    }    
    public object GetModule(Type moduleType)
    {
        foreach (var module in modules)
        {
            if (module.GetType() == moduleType)
            {
                return module;
            }
        }

        throw new KeyNotFoundException("Could not find any module of the type " + moduleType.AssemblyQualifiedName);
    }
    public IEnumerable<IModule> GetAllModules()
    {
        return modules;
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

    
    // Eventos do Console
    private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Impede o encerramento imediato do processo para garantir que o método de parada seja chamado        
        Environment.Exit(-1); // Encerra o processo após a execução do método de parada
    }

    private void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        Stop().Wait();
    }


}