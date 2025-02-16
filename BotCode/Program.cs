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

        public static FraseDoDia dailyFrase = new FraseDoDia();

        private static async Task Main(string[] args)
        {
            if (!File.Exists(ConfigIO.TokenPath))
            {
                Console.WriteLine($"(Program) {ConfigIO.TokenPath} não encontrado. Verificar se o arquivo existe e está com o nome correto");
            }

            if (!File.Exists(ConfigIO.ConfigPath))
            {
                Console.WriteLine($"(Program) {ConfigIO.ConfigPath} não encontrado. Verificar se o arquivo existe e está com o nome correto");
                ConfigReset();
            }

            await ConfigIO.LoadConfig(); // Carrega configs
            await InitDiscordConfig(); // Inicia o client do Discord para o bot            
            await InitCommands(); // Faz com que os comandos funcionem (eles precisam ser registrados primeiro)
            await client.ConnectAsync(); // Conecta no Discord; Ao bot se conectar, a função de inicializar executa
            await InitModules();
            await Task.Delay(-1);
        }

        public async static void ConfigReset()
        {
            Console.WriteLine("(ConfigReset) Criando um novo arquivo de configuração em branco");
            ConfigJSON json = new ConfigJSON();
            await ConfigIO.Write($"{ConfigIO.ConfigPath}", json);
        }

        public async static void ConfigTemplate()
        {
            Console.WriteLine("(ConfigReset) Criando um novo arquivo de configuração de template");
            ConfigJSON json = new ConfigJSON();
            await ConfigIO.Write($"template-{ConfigIO.ConfigPath}", json);
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
            dailyFrase.FrasePossivelmenteDeletada(sender, args);

            return Task.CompletedTask;
        }

        private static Task Client_MessageCreated(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            Console.WriteLine("(DiscordClient) Evento Acionado: Mensagem criada");
            dailyFrase.FrasePossivelmenteCriada(sender, args);

            return Task.CompletedTask;
        }

        public static Task InitModules()
        {
            Console.WriteLine("(Program) Inicializando módulos");

            // Módulos
            Console.WriteLine("(Program) Inicializando Frase do Dia");
            dailyFrase.Init();

            // Eventos
            Console.WriteLine("(Program) Inicializando Eventos");
            client.MessageCreated += Client_MessageCreated;
            client.MessageDeleted += Client_MessageDeleted;

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

            return Task.CompletedTask;
        }

        private static void Loop(object sender, ElapsedEventArgs e)
        {
            if (config.Log_Ticks)
            {
                Console.WriteLine($"(Program) Bot Ticking in {DateTime.Now}");                
            }

            dailyFrase.Loop();
        }

        private static void LogLoop(object sender, ElapsedEventArgs e)
        {
            if (!config.Log_LogTicks)
                return;

            Console.WriteLine($"(Program) Bot log ticking in {DateTime.Now}");
        }

    }

    public class FraseDoDia
    {        
        int fraseDoDiaEnvioHora;
        int fraseDoDiaEnvioMins;
        public int quantiaDeFrases; // quantas frases tem no canal de frases
        
        public DiscordChannel canalDeFrases; // origem das frases
        public DiscordChannel canalParaReenviar; // destino das frases        

        public Random random;
        public DiscordMessage fraseDoDia; // qual a frase que foi escolhida para o dia.
        bool fraseDoDiaEnviada = false;
        DateTime diaUltimoEnvio;        
        
        public DateTime DiaDoUltimoEnvio => diaUltimoEnvio;

        public void Init()
        {
            Console.WriteLine("(FraseDoDia) Inicializando");

            fraseDoDiaEnviada = false;
            fraseDoDiaEnvioHora = Program.config.FraseDiaria_HoraDeEnvio;
            fraseDoDiaEnvioMins = Program.config.FraseDiaria_MinsDeEnvio;
            
            quantiaDeFrases = Program.config.FraseDiaria_total;

            canalDeFrases = Program.client.GetChannelAsync(Program.config.FraseDiaria_CanalFetchID).Result;
            canalParaReenviar = Program.client.GetChannelAsync(Program.config.FraseDiaria_CanalEnvioID).Result;

            random = new Random();

            Console.WriteLine("(FraseDoDia) Fim da inicialização");
        }

        public async Task Loop()
        {            
            if (!fraseDoDiaEnviada)
            {
                DateTime time = DateTime.Now;

                if (time.Hour >= fraseDoDiaEnvioHora && time.Minute >= fraseDoDiaEnvioMins)
                {
                    // Deve enviar 

                    fraseDoDiaEnviada = true;
                    diaUltimoEnvio = time;

                    await Daily();
                }
            }
            else
            {
                // Verificar passagem do dia
                if (DateTime.Now.Date.CompareTo(diaUltimoEnvio) > 0) 
                {
                    // É o próximo dia
                    fraseDoDiaEnviada = false;   
                }
            }            
        }

        private async Task Daily()
        {
            Console.WriteLine("(FraseDoDia) Inicio de escolha de frase");
            await Choose();
            Console.WriteLine("(FraseDoDia) Fim da escolha de frase");

            Console.WriteLine("(FraseDoDia) Inicio de envio");

            await canalParaReenviar.SendMessageAsync($"Estarei servindo a frase diária aos senhores...");
            await Send();
            await canalParaReenviar.SendMessageAsync($"Aqui está!");

            Console.WriteLine("(FraseDoDia) Fim de envio");
        }

        // Serve para enviar a frase do dia
        public async Task Send()
        {
            Console.WriteLine("(FraseDoDia) Construindo mensagem para envio");

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.Title = "Frase do Dia";
            embedBuilder.Color = new Optional<DiscordColor>(new DiscordColor("d619bd"));
            embedBuilder.Footer = new DiscordEmbedBuilder.EmbedFooter();
            embedBuilder.Footer.Text = $"Frase cunhada por {fraseDoDia.Author.Username}";
            embedBuilder.Footer.IconUrl = fraseDoDia.Author.AvatarUrl;
            embedBuilder.Timestamp = fraseDoDia.CreationTimestamp;
            embedBuilder.Description = fraseDoDia.Content;            

            DiscordEmbed embed = embedBuilder.Build();

            Console.WriteLine("(FraseDoDia) Mensagem construída");
            Console.WriteLine("(FraseDoDia) Iniciando envio da mensagem pelo client");

            
            await canalParaReenviar.SendMessageAsync(embed);            

            Console.WriteLine("(FraseDoDia) Mensagem enviada pelo client");
            Console.WriteLine($"(FraseDoDia) Enviada frase '{fraseDoDia.Content}' de {fraseDoDia.Author.Username}, criada em {fraseDoDia.CreationTimestamp}");
        }
            
        // Serve para escolher a frase do dia
        public async Task Choose()
        {
            Console.WriteLine($"(FraseDoDia) Escolhendo um número aleatório para a frase do dia");

            int indexDaFrase = random.Next(0, quantiaDeFrases);

            Console.WriteLine($"(FraseDoDia) Procurando pela frase de índice {indexDaFrase}");

            if (indexDaFrase < 100)
            {
                // Frase é uma das 100 primeiras
                var frases = await canalDeFrases.GetMessagesAsync(100);
                fraseDoDia = frases[indexDaFrase];
            }
            else
            {                
                // Frase é depois das 100 primeiras

                IReadOnlyList<DiscordMessage> frases = null;
                // pega a primeira mensagem para servir de base
                var primeirasMsgs = await canalDeFrases.GetMessagesAsync(100); 
                DiscordMessage msgPivot = primeirasMsgs[99];
                int currentIndex = indexDaFrase; // usado para pegar a mensagens anteriores

                // "Procura" a mensagem do índice passado, até puxar as mensagens dentro desse índice
                while (currentIndex > 99)
                {                    
                    await Task.Delay(300);
                    frases = await canalDeFrases.GetMessagesBeforeAsync(msgPivot.Id, 100);
                    currentIndex -= 100;
                    msgPivot = frases[99];                    
                }

                fraseDoDia = frases[currentIndex];
            }

            Console.WriteLine($"(FraseDoDia) Frase do dia escolhida: {fraseDoDia.Content} escrita por {fraseDoDia.Author.Username}");
        }

        public void FrasePossivelmenteDeletada(DiscordClient sender, DSharpPlus.EventArgs.MessageDeleteEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases--;
            }
        }

        public void FrasePossivelmenteCriada(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases++;
            }
        }
    }

}