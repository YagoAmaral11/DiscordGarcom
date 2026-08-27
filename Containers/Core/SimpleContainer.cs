using DiscordGarçom.Containers.Core.DSharpPlusAdapters;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core;

public class SimpleContainer(IPersistance persistance, IEnumerable<IModule> modules, ulong LinkedServerID) : IServerContext
{
    // Internal 
    public List<IModule> modules = modules.ToList();
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
    private DiscordGuild linkedGuild; public DiscordGuild BindedDiscordServer => linkedGuild;
    public DiscordUser BotDiscordUser => BotDiscordClient.CurrentUser;
    public bool ReadyForCommands => readyForCommands;    

    private List<Type> GetCommands() => [];

    // SimpleContainer Main Flow
    public async Task<bool> Start()
    {
        ArgumentNullException.ThrowIfNull(persistance);

        if (modules.Count == 0)
        {
            Console.WriteLine("Warning: Launching shell without modules");
        }

        return await Initialize();
    }

    private async Task<bool> Initialize()
    {
        ArgumentNullException.ThrowIfNull(persistance);

        AddContainerEventHandlers();
        GetToken();
        GetTextCommandPrefixes();
        VerifyDuplicatedModules();

        DiscordClientBuilder dcClientBuilder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.All | DiscordIntents.GuildVoiceStates);                
        TaskCompletionSource<bool> readyToOperate = new(); // Usada depois para aguardar o término da inicialização                                                                  

        bool initializeResult = await InitStages_Initialize();
        if (!initializeResult)
            throw new Exception("Could not finish initialization stage: Initialize");

        InitStages_ConfigureDSharpEventHandlers(dcClientBuilder, readyToOperate);
        ConfigureBotCommands(dcClientBuilder);

        bool configServicesResult = await InitStages_ConfigureBotServices(dcClientBuilder);
        if (!configServicesResult)
            throw new Exception("Could not finish initialization stage: InitStages_ConfigureBotServices");

        discordClient = await BuildAndStartDiscordClient(dcClientBuilder, readyToOperate);

        await InitStages_SendServices();

        // Verifica se o bot está linkado ao servidor específico
        if (discordClient.Guilds.TryGetValue(linkedServerID, out DiscordGuild val) == false)
            return false;        

        // Finaliza inicialização, preenche o Server context
        linkedGuild = val;
        initialTime = DateTimeOffset.Now;

        await InitStages_PreStarts();
        await InitStages_Start();

        return true;
    }

    public async Task Stop()
    {
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("Stopping container");
        Console.ResetColor();

        _ = await SaveData();
        _ = await Shutdown();
    }

    private async Task<bool> SaveData()
    {
        Queue<Exception> exceptions = [];
        bool result = true;

        foreach (IModule module in modules)
        {
            try
            {
                await module.SaveData();
            }
            catch (Exception e)
            {
                exceptions.Enqueue(e);
            }            
        }

        if (exceptions.Count > 0)
        {
            result = false;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[BotContainer] It was not possible to save the bot data: Found {exceptions.Count} exceptions");
            Console.ResetColor();
        }

        while (exceptions.Count > 0)
        {
            var e = exceptions.Dequeue();
            Console.WriteLine(e);
        }        

        return result;
    }

    private async Task<bool> Shutdown()
    {
        Queue<Exception> exceptions = [];
        bool result = true;

        foreach (IModule module in modules)
        {
            try
            {
                await module.Shutdown();
            }
            catch (Exception e)
            {
                exceptions.Enqueue(e);
            }            
        }

        if (exceptions.Count > 0)
        {
            result = false;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[BotContainer] It was not possible to shutdown the bot gracefully: Found {exceptions.Count} exceptions");
            Console.ResetColor();
        }

        while (exceptions.Count > 0)
        {
            var e = exceptions.Dequeue();
            Console.WriteLine(e);
        }

        return result;
    }


    // Module Providers
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

    // Helper Functions
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
    
    private void AddContainerEventHandlers()
    {
        // Garante que o método de parada seja chamado quando o processo for encerrado
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        Console.CancelKeyPress += Console_CancelKeyPress;
    }

    private void GetToken()
    {
        // Obtem tokens e comandos
        Token = persistance.ReadRaw(TokenAcessKey).Result;

        if (String.IsNullOrWhiteSpace(Token))
        {
            throw new Exception("Discord Bot Token not found");
        }                
    }

    private void GetTextCommandPrefixes()
    {
        TextCommandsPrefixes = persistance.ReadObject(CommandPrefixesAcessKey, typeof(List<string>)).Result as List<string>;

        if (TextCommandsPrefixes == null || TextCommandsPrefixes.Count == 0)
        {
            throw new Exception("No text command prefixes configured");
        }
    }

    private async Task<bool> InitStages_Initialize()
    {
        // Inicializa módulos
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Initializing Modules");
        Console.ResetColor();

        Queue<(Exception ex, IModule mod)> exceptions = [];
        bool result = true;

        foreach (IModule module in modules)
        {
            try
            {
                await module.Initialize(this);
            }
            catch (Exception e)
            {
                result = false;
                exceptions.Enqueue((e, module));
            }            
        }

        if (!result)
        {
            Console.WriteLine("[BotContainer]" + $" Failed to init modules: Found {exceptions.Count} exceptions");

            while (exceptions.Count > 0)
            {
                var (ex, mod) = exceptions.Dequeue();                
                await BaseModule<int, int>.DumpException(mod, ex, persistance); 
            }
        }
        else
        {
            Console.WriteLine("[BotContainer]" + " Finalized modules init");
        }        

        return result;
    }

    private void InitStages_ConfigureDSharpEventHandlers(DiscordClientBuilder discordClientBuilder, TaskCompletionSource<bool> readyToOperate)
    {
        // Configura os event handlers dos eventos do bot
        // Configura a Task que indica o término da inicialização do bot
        discordClientBuilder.ConfigureEventHandlers(eventHandlers =>
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
    }

    private void ConfigureBotCommands(DiscordClientBuilder discordClientBuilder)
    {
        // Cria os processadores de comandos
        TextCommandProcessor textCommandProcessor = new(new() { PrefixResolver = new CustomPrefixResolver(false, TextCommandsPrefixes).ResolvePrefixAsync });
        SlashCommandProcessor slashCommandProcessor = new(new());

        // Adiciona e registra os comandos do bot
        discordClientBuilder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
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
    }

    private async Task<bool> InitStages_ConfigureBotServices(DiscordClientBuilder discordClientBuilder)
    {
        // Adiciona/Inicializa serviços
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Registering Services");
        Console.ResetColor();

        Queue<(Exception ex, IModule mod)> exceptions = [];
        bool result = true;

        discordClientBuilder.ConfigureServices(async serviceCollection =>
        {
            foreach (IModule module in modules)
            {
                try
                {
                    await module.ConfigureServices(serviceCollection);
                }
                catch (Exception e)
                {
                    result = false;
                    exceptions.Enqueue((e, module));
                }                
            }
        });

        if (!result)
        {
            Console.WriteLine("[BotContainer]" + $" Failed to register services: Found {exceptions.Count} exceptions");

            while (exceptions.Count > 0)
            {
                var (ex, mod) = exceptions.Dequeue();
                await BaseModule<int, int>.DumpException(mod, ex, persistance);
            }
        }
        else
        {
            Console.WriteLine("[BotContainer]" + " Services Registered & Started");
        }
        
        return result;
    }    

    private async Task<DiscordClient> BuildAndStartDiscordClient(DiscordClientBuilder discordClientBuilder, TaskCompletionSource<bool> readyToOperate)
    {
        // Cria o Client com os serviços passados e conecta com o Discord
        discordClient = discordClientBuilder.Build();
        await discordClient.ConnectAsync();
        await readyToOperate.Task; // Aguarda o término da inicialização do bot        

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Connected to discord");
        Console.ResetColor();

        return discordClient;
    }

    private async Task InitStages_SendServices()
    {
        // Passa o ServiceProvider
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Sending Services");
        Console.ResetColor();

        foreach (IModule module in modules)
        {
            await module.ReceiveServices(discordClient.ServiceProvider);
        }

        Console.WriteLine("[BotContainer]" + " Services Sent");
    }

    private async Task<bool> InitStageTemplate(string StartMessage, string EndMessage, string ErrorMessage, Func<IModule, Task> action)
    {
        Queue<(Exception ex, IModule mod)> exceptions = [];
        bool result = true;

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer] " + StartMessage);
        Console.ResetColor();
        
        foreach (IModule module in modules)
        {
            try
            {
                await action.Invoke(module);                
            }
            catch (Exception e)
            {
                result = false;
                exceptions.Enqueue((e, module));
            }            
        }

        if (!result)
        {
            Console.WriteLine("[BotContainer] " + $"{ErrorMessage}: Found {exceptions.Count} exceptions");

            while (exceptions.Count > 0)
            {
                var (ex, mod) = exceptions.Dequeue();
                await BaseModule<int, int>.DumpException(mod, ex, persistance);
            }
        }
        else
            Console.WriteLine("[BotContainer] " + EndMessage);

        return result;
    }

    private async Task InitStages_PreStarts()
    {
        // Pre-começa a execução dos módulos
        if (!await InitStageTemplate("Pre-Starting stage 0 start", "Finalized Pre-Start 0", "Failed to Pre-Start 0", async (module) =>
        {
            await module.PreStart_0();
        }))
            throw new Exception("Could not finish initialization stage: Pre-Start 0");

        if (!await InitStageTemplate("Pre-Starting stage 1 start", "Finalized Pre-Start 1", "Failed to Pre-Start 1", async (module) =>
        {
            await module.PreStart_1();
        }))
            throw new Exception("Could not finish initialization stage: Pre-Start 1");

        if (!await InitStageTemplate("Pre-Starting stage 2 start", "Finalized Pre-Start 2", "Failed to Pre-Start 2", async (module) =>
        {
            await module.PreStart_2();
        }))
            throw new Exception("Could not finish initialization stage: Pre-Start 2");        
    }

    private async Task InitStages_Start()
    {
        // Começa a execução dos módulos
        if (!await InitStageTemplate("Starting stage start", "Finalized Start", "Failed to Start", async (module) =>
        {
            await module.Start();
        }))
            throw new Exception("Could not finish initialization stage: Start");

        readyForCommands = true;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Started all modules. Bot is running.");
        Console.ResetColor();
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