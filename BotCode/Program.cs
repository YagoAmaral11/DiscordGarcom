using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Lavalink;
using DSharpPlus.Entities;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;

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

        // Discord Configs/Commands creation
        public static CommandsNextExtension commands;
        public static DiscordConfiguration discordConfiguration;
        public static CommandsNextConfiguration commandsNextConfiguration;

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

        // Runtime
        public static DateTime InitialTime;

#if DEBUG
        public const string BotVersion = "DEBUGGING";
        public const string Changelog = "- Em Testagem";
#else
        public const string BotVersion = "0.24";
        public const string Changelog = "- Patches e Bugfixes\n- Maior estabilidade para a Jukebox\nConsole melhorado";
#endif

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
            await InitDiscordConfig(); // Inicia o client do Discord para o bot            
            await InitCommands(); // Faz com que os comandos funcionem (eles precisam ser registrados primeiro)
            await client.ConnectAsync(); // Conecta no Discord; Ao bot se conectar, a função de inicializar executa
            await InitModules(); // Inicializa os módulos (Carrega informações, etc.)            
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
        private static Task InitDiscordConfig()
        {
            Console.WriteLine("(Program) Inicializando client e configurações do discord");

            discordConfiguration = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = token.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            };

            client = new DiscordClient(discordConfiguration);

            commandsNextConfiguration = new CommandsNextConfiguration()
            {
                StringPrefixes = config.Prefixs.ToArray(),
                EnableMentionPrefix = true,
                EnableDefaultHelp = false,
                EnableDms = true
            };

            commands = client.UseCommandsNext(commandsNextConfiguration);
            client.Ready += Client_Ready;

            Console.WriteLine("(Program) Inicialização finalizada");

            return Task.CompletedTask;
        }

        private static Task InitCommands()
        {
            Console.WriteLine("(Program) Registrando comandos");

            commands.RegisterCommands<Commands>();

            Console.WriteLine("(Program) Comandos registrados");
            return Task.CompletedTask;
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
            client.MessageCreated += Client_MessageCreated;
            client.MessageDeleted += Client_MessageDeleted;
            AppDomain.CurrentDomain.ProcessExit += Program_Closing;
            client.ComponentInteractionCreated += Client_ComponentInteractionCreated;

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
        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Client inicializado");
                       
            servidor = client.Guilds.Values.First();

            return Task.CompletedTask;
        }

        private static Task Client_MessageDeleted(DiscordClient sender, DSharpPlus.EventArgs.MessageDeleteEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Evento Acionado: Mensagem deletada");

            modulo_Frases.FrasePossivelmenteDeletada(sender, args);

            return Task.CompletedTask;
        }

        private static Task Client_MessageCreated(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Evento Acionado: Mensagem criada");

            modulo_Frases.FrasePossivelmenteCriada(sender, args);

            return Task.CompletedTask;
        }

        private static Task Client_ComponentInteractionCreated(DiscordClient sender, DSharpPlus.EventArgs.ComponentInteractionCreateEventArgs args)
        {
            Console.WriteLine($"(Program) Interação criada: {args.Interaction.Data.CustomId} criada por {args.User.Username} ({args.User.Id})");

            return Task.CompletedTask;
        }

        public static void Program_Closing(object sender, EventArgs e)
        {
            Console.WriteLine("(Program) Bot finalizando");
            
            SaveModules();            
            
            Console.WriteLine("(Program) Bot finalizado");
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
            console.ConsoleMinute();
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

    public class BotConsole
    {
        private int animationProgress;        
        private int totalConsoleLines;

        public BotConsole()
        {
            Init();            
        }

        private void Init()
        {            
            Console.ResetColor();
            Console.CursorVisible = false;
            animationProgress = 0;
            totalConsoleLines = 0;            
        }

        // Atualiza o console por tick
        public void ConsoleTick()
        {                        

        }

        public static void WriteWithColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }   

        // Atualiza o console por minuto (Limpa os logs de evento)
        public void ConsoleMinute()
        {
            Console.Clear();
            WriteWithColor($"(Program) Bot Time: {Program.PrintTimeNow()} (Local Time: {DateTime.UtcNow})\n", ConsoleColor.Magenta);            

            if (Program.modulo_Jukebox.connectedEndpoint != null)
            {
                WriteWithColor($"(Lavalink) Online! Connected to endpoint {Program.modulo_Jukebox.connectedEndpoint.Hostname}\n", ConsoleColor.Green);
            }
            else
            {
                WriteWithColor($"(Lavalink) Offline!\n", ConsoleColor.Red);
            }

            if (Program.modulo_Jukebox.IsConnected)
            {
                WriteWithColor($"(Jukebox) Connected to {Program.modulo_Jukebox.lavalinkPlayback.Channel.Name}\n", ConsoleColor.Green);

                if (Program.modulo_Jukebox.songCurrent != null)   
                    WriteWithColor($"(Jukebox) Playing {Program.modulo_Jukebox.songCurrent.Title}\n", ConsoleColor.DarkGray);

                if (Program.modulo_Jukebox.ThereIsQueue)
                    WriteWithColor($"(Jukebox) With {Program.modulo_Jukebox.songQueue.Count} in queue\n", ConsoleColor.DarkGray);
            }
        }
    }
}