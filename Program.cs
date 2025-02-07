using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
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

        public static FraseDoDia dailyFrase = new FraseDoDia();

        private static async Task Main(string[] args)
        {
            await ConfigIO.LoadConfig(); // Carrega configs
            InitDiscordConfig(); // Inicia o client do Discord para o bot
            StartLogic(); // Inicia a lógica do Bot, com seus timers e outras coisas.
            await client.ConnectAsync(); // Conecta no Discord
            await Task.Delay(-1);
        }

        private static void InitDiscordConfig()
        {
            discordConfiguration = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = token.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            };

            client = new DiscordClient(discordConfiguration);

            commandsNextConfiguration = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { config.prefix },
                EnableMentionPrefix = true,
                EnableDefaultHelp = false,
                EnableDms = true
            };

            commands = client.UseCommandsNext(commandsNextConfiguration);
            client.Ready += Client_Ready;            
        }

        private static Task Client_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs args)
        {
            StartLogic();
            return Task.CompletedTask;
        }

        private static Task Client_MessageDeleted(DiscordClient sender, DSharpPlus.EventArgs.MessageDeleteEventArgs args)
        {
            dailyFrase.FrasePossivelmenteDeletada(sender, args);

            return Task.CompletedTask;
        }

        private static Task Client_MessageCreated(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            dailyFrase.FrasePossivelmenteCriada(sender, args);

            return Task.CompletedTask;
        }


        public static void StartLogic()
        {
            // Timer
            mainTimer = new Timer(config.mainTimerIntervalMs);
            mainTimer.AutoReset = true;
            mainTimer.Enabled = true;
            mainTimer.Elapsed += Loop;

            // Módulos
            dailyFrase.Init();

            // Eventos
            client.MessageCreated += Client_MessageCreated;
            client.MessageDeleted += Client_MessageDeleted;

            // Outros
            ConfigBlank();
        }

        private static void Loop(object sender, ElapsedEventArgs e)
        {
            if (config.logTicks)
            {
                Console.WriteLine($"[{DateTime.Now}] Bot Ticking");                
            }

            dailyFrase.Loop();
        }

        public async static void ConfigBlank(string name = "configTemplate")
        {
            ConfigJSON json = new ConfigJSON();
            await ConfigIO.Write(name, json);
        }

    }

    public class FraseDoDia
    {
        public float tempoParaFraseMs; // tempo em milessegundos que a próxima frase demorará à chegar. Ainda é aplicado um "check" para ver se o horário mínimo já passou.
        public DiscordMessage fraseDoDia; // qual a frase que foi escolhida para o dia.
        public DiscordChannel canalDeFrases; // origem das frases
        public DiscordChannel canalParaReenviar; // destino das frases
        public int quantiaDeFrases; // quantas frases tem no canal de frases

        public Random random;

        public void Init()
        {
            tempoParaFraseMs = Program.config.minFraseIntervalSec * 1000;
            quantiaDeFrases = Program.config.quantiaInicialDeFrases;

            canalDeFrases = Program.client.GetChannelAsync(Program.config.canalFrasesId).Result;
            canalParaReenviar = Program.client.GetChannelAsync(Program.config.canalFrasesEnvioId).Result;

            random = new Random();
        }

        public async Task Loop()
        {
            tempoParaFraseMs -= Program.config.mainTimerIntervalMs;

            if (tempoParaFraseMs < 0)
            {
                tempoParaFraseMs = 0;
            }

            if (tempoParaFraseMs <= 0)
            {
                tempoParaFraseMs = Program.config.minFraseIntervalSec * 1000;

                if (Program.config.logFrasesDoDia)
                {
                    Console.WriteLine("(DailyFrase) Inicio de escolha de frase");
                }

                await Choose();

                if (Program.config.logFrasesDoDia)
                {
                    Console.WriteLine("(DailyFrase) Frase Escolhida");
                }

                if (Program.config.logFrasesDoDia)
                {
                    Console.WriteLine("(DailyFrase) Inicio de envio");
                }

                await Send();

                if (Program.config.logFrasesDoDia)
                {
                    Console.WriteLine("(DailyFrase) Mensagem enviada");
                }                
            }
        }

        // Serve para enviar a frase do dia
        public async Task Send()
        {            
            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.Title = "Frase do Dia";
            embedBuilder.Color = new Optional<DiscordColor>(new DiscordColor("d619bd"));
            embedBuilder.Footer = new DiscordEmbedBuilder.EmbedFooter();
            embedBuilder.Footer.Text = $"Frase cunhada por {fraseDoDia.Author.Username}";
            embedBuilder.Footer.IconUrl = fraseDoDia.Author.AvatarUrl;
            embedBuilder.Timestamp = fraseDoDia.CreationTimestamp;
            embedBuilder.Description = fraseDoDia.Content;            

            DiscordEmbed embed = embedBuilder.Build();
            await canalParaReenviar.SendMessageAsync(embed);

            if (Program.config.logFrasesDoDia)
            {
                Console.WriteLine($"(DailyFrase) Enviada frase '{fraseDoDia.Content}' de {fraseDoDia.Author.Username}, criada em {fraseDoDia.CreationTimestamp}");
            }
        }

        // Serve para escolher a frase do dia
        public async Task Choose()
        {            
            int indexDaFrase = random.Next(0, quantiaDeFrases);
            
            if (Program.config.logFrasesDoDia)
            {
                Console.WriteLine($"(DailyFrase) Procurando pela frase de índice {indexDaFrase}");
            }

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
                    await Task.Delay(200);
                    frases = await canalDeFrases.GetMessagesBeforeAsync(msgPivot.Id, 100);
                    currentIndex -= 100;
                    msgPivot = frases[99];                    
                }

                fraseDoDia = frases[currentIndex];
            }
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
