using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Commands.Processors.SlashCommands;
using Lavalink4NET.Extensions;
using DSharpPlus.Extensions;
using Microsoft.Extensions.Hosting;
using Lavalink4NET;
using System.Runtime.Serialization;

namespace GarçomDoKitts
{   

    public static class Program
    {
        // IO/Data
        public static TokenJSON token;
        public static ConfigJSON config;
        public static TaskDone taskDoneList = new();
        public static readonly string taskDonePath = $"{DataIO.DataFolderPath}mensagens.json";

        // Discord APIs, Channels, Server, etc.
        public static DiscordClient client;
        public static DiscordGuild servidor;
        public static IServiceProvider services;

        // Timers
        public static Timer mainTimer;
        public static Timer longTimer;

        // Módulos
        public static Frases modulo_Frases = new();      
        public static Backuper modulo_Backuper = new();
        public static Jogos modulo_Jogos = new();        
        public static Jukebox modulo_Jukebox = new();        
        public static GerenciadorDeCanal modulo_GenDeCanal = new();
        public static BotConsole console = new();

        // Events
        public delegate Task MessageCreatedDelegate(DiscordClient sender, MessageCreatedEventArgs args);
        public static event MessageCreatedDelegate OnMessageCreated;

        public delegate Task MessageDeletedDelegate(DiscordClient sender, MessageDeletedEventArgs args);    
        public static event MessageDeletedDelegate OnMessageDeleted;

        public delegate Task ComponentInteractionCreatedDelegate(DiscordClient sender, ComponentInteractionCreatedEventArgs args);
        public static event ComponentInteractionCreatedDelegate OnComponentInteractionCreated;

        // Runtime
        public static DateTime InitialTime;
        public const string BotVersion = "0.3";
        public const string Changelog = "- Melhorias significativas no Source Code\n- Migração da biblioteca de Jukebox\n- Adicionado comandos do Discord\n-Adicionado novos comandos";

        // Main
        private static async Task Main(string[] args)
        {
            if (!File.Exists(DataIO.TokenPath))
            {
                Console.WriteLine($"(Program) {DataIO.TokenPath} não encontrado. Verificar se o arquivo existe e está com o nome correto");
                Console.WriteLine($"(Program) Finalizando programa. Sem token é impossível continuar");
                Environment.Exit(-1);
            }

            if (!File.Exists(DataIO.ConfigPath))
            {
                Console.WriteLine($"(Program) {DataIO.ConfigPath} não encontrado. Verificar se o arquivo existe e está com o nome correto");
                ConfigReset();
            }            

            await DataIO.LoadConfig(); // Carrega configs
            InitDiscordClient(); // Inicia o client do Discord para o bot                        
            
            await client.ConnectAsync(); 
            servidor = client.Guilds.Values.First(); // Seleciona o servidor que será conectado no bot

            await InitModules();       
            InitialTime = GetTime();

            await Task.Delay(-1);
        }

        // IO
        public async static void ConfigReset()
        {
            Console.WriteLine("(ConfigReset) Criando um novo arquivo de configuração em branco");

            ConfigJSON json = new ConfigJSON();
            await DataIO.Write($"{DataIO.ConfigPath}", json);

            Console.WriteLine("(ConfigReset) Arquivo de configuração criado");
        }

        public async static void ConfigTemplate()
        {
            Console.WriteLine("(ConfigReset) Criando um novo arquivo de configuração de template");

            ConfigJSON json = new ConfigJSON();
            await DataIO.Write($"template-{DataIO.ConfigPath}", json);

            Console.WriteLine("(ConfigReset) Arquivo de template criado");
        }

        // Init
        private static void InitDiscordClient()
        {
            Console.WriteLine("(Program) Inicializando client e configurações do discord");

            // Inicializa as configurações do DiscordClient
            DiscordClientBuilder clientBuilder = DiscordClientBuilder.CreateDefault(token.Token, DiscordIntents.All);             

            clientBuilder.ConfigureGatewayClient(gateway =>
            {
                gateway.AutoReconnect = true;                
            });

            // Inicializa os eventos do DiscordClient 
            clientBuilder.ConfigureEventHandlers(tmp =>
                {
                    tmp.HandleMessageCreated(Client_MessageCreated);
                    tmp.HandleMessageDeleted(Client_MessageDeleted);
                    tmp.HandleComponentInteractionCreated(Client_ComponentInteractionCreated);
                }
            );

            // Inicializa os comandos             
            clientBuilder.UseCommands((IServiceProvider serviceProvider, CommandsExtension extension) =>
                {
                    TextCommandProcessor textCommandProcessor = new TextCommandProcessor(new()
                        {
                            PrefixResolver = new DefaultPrefixResolver(false, "garc", "g", "Garc", "G").ResolvePrefixAsync
                        }
                    );                                                           

                    extension.AddProcessor(textCommandProcessor);
                    extension.AddCommands([typeof(Commands)]);
                }
            );            

            // Constrói o client do Discord
            client = clientBuilder.Build();                                    

            Console.WriteLine("(Program) Inicialização finalizada");
            return;
        }

        public async static Task InitModules()
        {
            Console.WriteLine("(Program) Inicializando módulos");            

            // Módulos            
            await modulo_Frases.Init();
            await modulo_Jogos.Init();
            modulo_Backuper.Init();
            await modulo_Jukebox.Init();
            await modulo_GenDeCanal.Init();

            // Eventos
            Console.WriteLine("(Program) Inicializando Eventos");
            AppDomain.CurrentDomain.ProcessExit += Program_Closing;            

            // Timer Principal
            Console.WriteLine("(Program) Inicializando timer principal");
            mainTimer = new Timer(config.Timers_TickTimerMs);
            mainTimer.AutoReset = true;
            mainTimer.Enabled = true;
            mainTimer.Elapsed += Loop;

            // Timer secundário
            Console.WriteLine("(Program) Inicializando timer secundário");
            longTimer = new Timer(1000 * 60);
            longTimer.AutoReset = true;
            longTimer.Enabled = true;
            longTimer.Elapsed += LongLoop;

            // Inicializa o console personalizado
            console = new();            

            // Outros
            taskDoneList = await DataIO.Load(DataIO.TaskDonePath, typeof(TaskDone)) as TaskDone;

            return;
        }

        // Events
        private static Task Client_MessageCreated(DiscordClient sender, MessageCreatedEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Evento Acionado: Mensagem criada");
            OnMessageCreated?.Invoke(sender, args);
            return Task.CompletedTask;
        }

        private static Task Client_MessageDeleted(DiscordClient sender, MessageDeletedEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Evento Acionado: Mensagem deletada");
            OnMessageDeleted?.Invoke(sender, args);
            return Task.CompletedTask;
        }        

        private static Task Client_ComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreatedEventArgs args)
        {
            Console.WriteLine($"(Program) Interação criada: {args.Interaction.Data.CustomId} criada por {args.User.Username} ({args.User.Id})");
            OnComponentInteractionCreated?.Invoke(sender, args);
            return Task.CompletedTask;
        }

        // Others
        public static Task SaveModules()
        {
            // Verificar se é nulo antes, pois esse método pode ser chamado antes da inicialização
            Console.WriteLine("(Program) Gravando módulos");

            modulo_Frases?.SaveInstance();
            modulo_GenDeCanal?.SaveInstance();

            Console.WriteLine("(Program) Módulos gravados");
            return Task.CompletedTask;
        }
        
        public static string GetTaskDoneMessage()
        {
            uint weightTotal = 0;

            foreach (var msg in taskDoneList.Msgs)
            {
                weightTotal += msg.Weight;
            }

            Random random = new Random();
            uint next = (uint) random.Next(0, (int) weightTotal + 1);
            
            for (int i = 0; i < weightTotal; i++)
            {
                var current = taskDoneList.Msgs[i];

                if (next <= current.Weight)
                {
                    return taskDoneList.Msgs[i].Msg;
                }
                else
                {
                    next -= current.Weight;  
                }
            }

            return taskDoneList.Msgs[0].Msg;
        }

        public static void Program_Closing(object sender, EventArgs e)
        {
            Console.WriteLine("(Program) Bot finalizando");

            SaveModules();

            Console.WriteLine("(Program) Bot finalizado");
        }

        // Loops
        private static async void Loop(object sender, ElapsedEventArgs e)
        {
            console.ConsoleTick();
            await modulo_Frases.Loop();
            await modulo_Backuper.Loop();
            modulo_Jukebox.Loop();
            modulo_GenDeCanal.Loop();            
        }

        private static async void LongLoop(object sender, ElapsedEventArgs e)
        {
            await Task.Run(() => console.ConsoleMinute());            
        }

        // Time
        public static DateTime GetTime() => TimeZoneInfo.ConvertTime(DateTime.UtcNow, config.Program_UTC);
        public static string PrintTimeNow() => GetTime().ToString(config.Program_LocalCulture);
        public static string PrintTime(DateTime time) => time.ToString(config.Program_LocalCulture);
        public static string PrintTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.Days > 0)
            {
                return timeSpan.ToString(@"dd\dhh\:mm\:ss");
            }
            else if (timeSpan.Hours > 0)
            {
                return timeSpan.ToString(@"hh\:mm\:ss");
            }
            else
            {
                return timeSpan.ToString(@"mm\:ss");
            }
        }

    }               
}