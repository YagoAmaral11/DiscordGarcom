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
using Newtonsoft.Json;

namespace GarçomDoKitts
{
    public class Frases
    {
        [JsonIgnore] public static readonly string DataPath = $"{DataIO.DataFolderPath}frases.json";

        // Configs
        [JsonIgnore] public static int FraseDoDiaEnvioHora => Program.config.Frases_HoraDeEnvio;
        [JsonIgnore] public static int FraseDoDiaEnvioMins => Program.config.Frases_MinsDeEnvio;

        // Classes
        [JsonIgnore] public Random random;
        [JsonIgnore] public DiscordChannel canalDeFrases; // origem das frases
        [JsonIgnore] public DiscordChannel canalParaReenviar; // destino das frases        
        [JsonIgnore] public DiscordMessage fraseDoDia; // qual a frase que foi escolhida para o dia.

        // Runtime data
        public ulong fraseDoDiaID;
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

            // Event subscribing
            Program.OnMessageCreated += MessageCreated;
            Program.OnMessageDeleted += MessageDeleted;

            if (Program.config.Frases_totalInicial < 0)
            {
                if (!File.Exists(DataPath))
                {
                    Console.WriteLine("(Frases) Fetching de mensagens acionado");
                    await Fetch();
                }
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

                fraseDoDiaID = tmpLoad.fraseDoDiaID;                
                quantiaDeFrases = tmpLoad.quantiaDeFrases;
                fraseDoDiaEnviada = tmpLoad.fraseDoDiaEnviada;
                diaUltimoEnvio = tmpLoad.diaUltimoEnvio;

                fraseDoDia = await canalDeFrases.GetMessageAsync(fraseDoDiaID);

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

            if (fraseDoDia != null)
            {
                fraseDoDiaID = fraseDoDia.Id;
            }            

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
        public async Task Send(DiscordChannel canalParaEnviar = null)
        {            
            if (canalParaEnviar == null)
            {
                canalParaEnviar = canalParaReenviar;
            }

            Console.WriteLine("(FraseDoDia) Construindo mensagem para envio");

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.Title = "Frase do Dia";            
            embedBuilder.Color = new DiscordColor("d619bd");
            embedBuilder.Footer = new DiscordEmbedBuilder.EmbedFooter();
            embedBuilder.Footer.Text = $"Frase cunhada por {fraseDoDia.Author.Username}";
            embedBuilder.Footer.IconUrl = fraseDoDia.Author.AvatarUrl;
            embedBuilder.Timestamp = fraseDoDia.CreationTimestamp;
            embedBuilder.Description = fraseDoDia.Content;

            DiscordEmbed embed = embedBuilder.Build();

            Console.WriteLine("(FraseDoDia) Mensagem construída");
            Console.WriteLine("(FraseDoDia) Iniciando envio da mensagem pelo client");

            await canalParaEnviar.SendMessageAsync(embed);

            Console.WriteLine("(FraseDoDia) Mensagem enviada pelo client");
            Console.WriteLine($"(FraseDoDia) Enviada frase '{fraseDoDia.Content}' de {fraseDoDia.Author.Username}, criada em {fraseDoDia.CreationTimestamp}");
        }

        // Serve para escolher a frase do dia
        // TODO: Consertar problemas de, quando a API não responder com 100 mensagens (der problema de rate limit), o bot pode travar
        // TODO: Separar o fetch de uma mensagem específica em uma função separada, para que ela possa ser reutilizada; No momento o mesmo código é reutilizado em vários lugares diferentes.        
        public async Task Choose()
        {            
            Console.WriteLine($"(FraseDoDia) Escolhendo um número aleatório para a frase do dia");

            int indexDaFrase = random.Next(0, quantiaDeFrases);

            Console.WriteLine($"(FraseDoDia) Procurando pela frase de índice {indexDaFrase}");

            if (indexDaFrase < 100)
            {
                // Frase é uma das 100 primeiras
                List<DiscordMessage> tmp = new();
                await foreach (var message in canalDeFrases.GetMessagesAsync(100))
                {
                    tmp.Add(message);
                }
                IReadOnlyList<DiscordMessage> frases = tmp.AsReadOnly();
                
                fraseDoDia = frases[indexDaFrase];
            }
            else
            {
                // Frase é depois das 100 primeiras
                IReadOnlyList<DiscordMessage> frases = null;

                // pega a primeira mensagem para servir de base
                List<DiscordMessage> tmp = new();
                await foreach (var message in canalDeFrases.GetMessagesAsync(100))
                {
                    tmp.Add(message);
                }
                IReadOnlyList<DiscordMessage> primeirasMsgs = tmp.AsReadOnly();
                
                DiscordMessage msgPivot = primeirasMsgs[99];
                int currentIndex = indexDaFrase; // usado para pegar a mensagens anteriores

                // "Procura" a mensagem do índice passado, até puxar as mensagens dentro desse índice
                while (currentIndex > 99)
                {
                    await Task.Delay(300);

                    List<DiscordMessage> tmp2 = new();
                    await foreach (var message in canalDeFrases.GetMessagesBeforeAsync(msgPivot.Id, 100))
                    {
                        tmp2.Add(message);
                    }
                    frases = tmp2.AsReadOnly();
                    
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

            List<DiscordMessage> tmp = new();
            await foreach (var message in canalDeFrases.GetMessagesAsync(100))
            {
                tmp.Add(message);
            }
            IReadOnlyList<DiscordMessage> frases = tmp.AsReadOnly();            
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
                List<DiscordMessage> tmp2 = new();
                await foreach (var message in canalDeFrases.GetMessagesBeforeAsync(anchor.Id, 100))
                {
                    tmp2.Add(message);
                }
                frases = tmp2.AsReadOnly();                
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


        // Servem para atualizar a contagem de frases quando elas são criadas ou deletadas      
        private void FrasePossivelmenteDeletada(DiscordClient sender, DSharpPlus.EventArgs.MessageDeletedEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases--;
                Console.WriteLine("(Frases) Removendo uma frase da contagem");
            }
        }

        private void FrasePossivelmenteCriada(DiscordClient sender, DSharpPlus.EventArgs.MessageCreatedEventArgs args)
        {
            if (args.Channel == canalDeFrases)
            {
                quantiaDeFrases++;
                Console.WriteLine("(Frases) Adicionando uma frase da contagem");
            }
        }


        // Eventos
        public Task MessageCreated(DiscordClient sender, DSharpPlus.EventArgs.MessageCreatedEventArgs args)
        {
            FrasePossivelmenteCriada(sender, args);
            return Task.CompletedTask;
        }   

        public Task MessageDeleted(DiscordClient sender, DSharpPlus.EventArgs.MessageDeletedEventArgs args)
        {
            FrasePossivelmenteDeletada(sender, args);
            return Task.CompletedTask;
        }

    }
}
