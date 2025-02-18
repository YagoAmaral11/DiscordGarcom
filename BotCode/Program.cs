using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace GarçomDoKitts
{
    internal class Program
    {
        public static TokenJSON token;
        public static ConfigJSON config;

        public static DiscordClient client;
        public static CommandsNextExtension commands;

        public static DiscordConfiguration discordConfiguration;
        public static CommandsNextConfiguration commandsNextConfiguration;

        public static Timer mainTimer;
        public static Timer logTimer;

        public static Frases modulo_Frases = new();      
        public static Backuper modulo_Backuper = new();

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

            await Task.Delay(-1);
        }

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
                StringPrefixes = new string[] { config.Prefix },
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

        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Client inicializado");            
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

        private static void Program_Closing(object sender, EventArgs e)
        {
            Console.WriteLine("(Program) Bot finalizando");
            
            SaveModules();
            
            Console.WriteLine("(Program) Bot finalizado");
        }

        public async static Task InitModules()
        {
            Console.WriteLine("(Program) Inicializando módulos");

            // Módulos            
            await modulo_Frases.Init();
            modulo_Backuper.Init();

            // Eventos
            Console.WriteLine("(Program) Inicializando Eventos");
            client.MessageCreated += Client_MessageCreated;
            client.MessageDeleted += Client_MessageDeleted;
            AppDomain.CurrentDomain.ProcessExit += Program_Closing;            

            // Timer Principal
            Console.WriteLine("(Program) Inicializando timer principal");
            mainTimer = new Timer(config.Timers_TickTimerMs);
            mainTimer.AutoReset = true;
            mainTimer.Enabled = true;
            mainTimer.Elapsed += Loop;

            // Timer Secundário
            Console.WriteLine("(Program) Inicializando timer secundário");
            logTimer = new Timer(config.Timers_LogTimerMs);
            logTimer.AutoReset = true;
            logTimer.Enabled = true;
            logTimer.Elapsed += LogLoop;

            return;
        }        

        public static Task SaveModules()
        {
            // Verificar se é nulo antes, pois esse método pode ser chamado antes da inicialização
            Console.WriteLine("(Program) Gravando módulos");

            modulo_Frases?.SaveInstance();

            Console.WriteLine("(Program) Módulos gravados");
            return Task.CompletedTask;
        }

        private static void Loop(object sender, ElapsedEventArgs e)
        {
            if (config.Log_Ticks)
            {
                Console.WriteLine($"(Program) Bot Ticking in {GetTime()}");                
            }

            modulo_Frases.Loop();
            modulo_Backuper.Loop();
        }

        private static void LogLoop(object sender, ElapsedEventArgs e)
        {
            if (!config.Log_LogTicks)
                return;

            Console.WriteLine($"(Program) Bot log ticking in {GetTime()}");
        }

        public static DateTime GetTime() => TimeZoneInfo.ConvertTime(DateTime.UtcNow, config.Program_UTC);

    }           
}