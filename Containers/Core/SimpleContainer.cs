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

namespace DiscordGarçom.Containers.Core;

public class SimpleContainer(IPersistance persistance, IEnumerable<IModule> modules, ulong LinkedServerID) : IServerContext
{
    // Internal 
    public IList<IModule> modules = modules.ToList();
    public IPersistance persistance = persistance;   
    private readonly ulong linkedServerID = LinkedServerID;

    // Config Data
    public static string Name => "SimpleContainer";
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

        // Inicializa discord client
        DiscordClientBuilder dcClientBuilder = DiscordClientBuilder.CreateDefault(Token, DiscordIntents.All);

        // Usada depois para aguardar o término da inicialização
        TaskCompletionSource<bool> readyToOperate = new();

        // Verificar módulos duplicados
        VerifyDuplicatedModules();

        // Inicializa módulos
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine("[BotContainer]" + " Initializing Modules");
        Console.ResetColor();
        foreach (IModule module in modules)
        {
            await module.Initialize(this, services);
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
            serviceProvider = services;

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

        // Inicializa o bot
        discordClient = dcClientBuilder.Build();
        await discordClient.ConnectAsync();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Connected to discord");
        Console.ResetColor();

        await readyToOperate.Task; // Aguarda o término da inicialização do bot        

        // Verifica se o bot está linkado ao servidor específico
        if (discordClient.Guilds.TryGetValue(linkedServerID, out DiscordGuild val) == false)
            return false;

        // TODO: Verificar se tem alguma forma de bloquear a execução de comandos até o término da inicialização; Bloquear aqui

        // Finaliza inicialização, preenche o Server context
        shellGuild = val;
        initialTime = DateTime.Now;

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
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[BotContainer]" + " Started all modules. Bot is running.");
        Console.ResetColor();

        // TODO: Verificar se tem alguma forma de bloquear a execução de comandos até o término da inicialização; Desbloquear aqui        

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