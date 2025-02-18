using DSharpPlus.Entities;
using DSharpPlus;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Timers;

namespace GarçomDoKitts
{
    public class Frases
    {
        public static readonly string DataPath = $"{DataIO.DataFolderPath}frases.json";

        // Configs
        public static int FraseDoDiaEnvioHora => Program.config.Frases_HoraDeEnvio;
        public static int FraseDoDiaEnvioMins => Program.config.Frases_MinsDeEnvio;

        // Classes
        public Random random;
        public DiscordChannel canalDeFrases; // origem das frases
        public DiscordChannel canalParaReenviar; // destino das frases        

        // Runtime data
        public DiscordMessage fraseDoDia; // qual a frase que foi escolhida para o dia.
        public int quantiaDeFrases; // quantas frases tem no canal de frases
        public bool fraseDoDiaEnviada;
        public DateTime diaUltimoEnvio;        

        public async Task Init()
        {
            Console.WriteLine("(Frases) Inicializando");

            Frases tmpLoad = new Frases();

            canalDeFrases = Program.client.GetChannelAsync(Program.config.Frases_CanalFetchID).Result;
            canalParaReenviar = Program.client.GetChannelAsync(Program.config.Frases_CanalEnvioID).Result;
            random = new Random();

            fraseDoDiaEnviada = false;

            if (Program.config.Frases_totalInicial < 0)
            {
                Console.WriteLine("(Frases) Fetching de mensagens acionado");
                await Fetch();
            }
            else
            {
                quantiaDeFrases = Program.config.Frases_totalInicial;
            }            

            if (File.Exists(DataPath))
            {
                Console.WriteLine("(Frases) Dados salvos encontrados");

                tmpLoad = DataIO.Load(DataPath, typeof(Frases)).Result as Frases;

                Console.WriteLine("(Frases) Carregando dados salvos");

                fraseDoDia = tmpLoad.fraseDoDia;
                quantiaDeFrases = tmpLoad.quantiaDeFrases;
                fraseDoDiaEnviada = tmpLoad.fraseDoDiaEnviada;
                diaUltimoEnvio = tmpLoad.diaUltimoEnvio;

                Console.WriteLine("(Frases) Dados sobreescrevidos");
            }
            else
            {
                Console.WriteLine("(Frases) Dados salvos não foram encontrados, indo com dados padrão de acordo com a configuração");
            }

            Console.WriteLine("(Frases) Fim da inicialização");
        }

        public async Task Loop()
        {
            if (!fraseDoDiaEnviada)
            {
                DateTime time = Program.GetTime();

                if (time.Hour >= FraseDoDiaEnvioHora && time.Minute >= FraseDoDiaEnvioMins)
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
                if (Program.GetTime().Date.CompareTo(diaUltimoEnvio) > 0)
                {
                    // É o próximo dia
                    fraseDoDiaEnviada = false;
                }
            }
        }

        public async Task SaveInstance()
        {
            Console.WriteLine("(Frases) Inicializando gravação dos dados");

            await DataIO.Write(DataPath, this);

            Console.WriteLine("(Frases) Dados gravados");
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

        // Serve para descobrir quantas frases tem no canal de frases.
        public async Task Fetch()
        {
            Console.WriteLine("(Frases) Iniciando Fetch");

            int quantia = 0;
            bool cont = true;

            IReadOnlyList<DiscordMessage> frases = await canalDeFrases.GetMessagesAsync(100);
            DiscordMessage anchor = frases[0];

            quantia += frases.Count;
            if (frases.Count < 100)
            {                
                cont = false;
            }
            else
            {                
                anchor = frases[99];
                cont = true;
            }

            while (cont)
            {                
                frases = await canalDeFrases.GetMessagesBeforeAsync(anchor.Id, 100);
                quantia += frases.Count;                

                if (frases.Count < 100)
                {
                    cont = false;
                }
                else
                {
                    anchor = frases[99];
                }                

                await Task.Delay(500);                
            }

            quantiaDeFrases = quantia;            

            Console.WriteLine("(Frases) Fetch finalizado");
        }


        public void FrasePossivelmenteDeletada(DiscordClient sender, DSharpPlus.EventArgs.MessageDeleteEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases--;
                Console.WriteLine("(Frases) Removendo uma frase da contagem");
            }
        }

        public void FrasePossivelmenteCriada(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases++;
                Console.WriteLine("(Frases) Adicionando uma frase da contagem");
            }
        }

    }
}
