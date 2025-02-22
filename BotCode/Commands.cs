using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Executors;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using System.Drawing;

namespace GarçomDoKitts
{
    public class Commands : BaseCommandModule
    {

        [Command("FraseDiaria")]
        [Aliases("FraseDoDia" , "FraseDiária", "FraseDaily", "DailyFrase")]
        public async Task Frases_mostrarFrase(CommandContext context)
        {
            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Mostrando a frase diária, pedido por: {context.User.Username} ({context.User.Id})");

            if (Program.modulo_Frases.fraseDoDia == null)
            {
                Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Frase do Dia é nula, impossível mandar frase");
                await Program.client.SendMessageAsync(context.Channel, "Não existe frase disponível");
                return;
            }

            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Mandando a frase do dia");

            await context.Channel.SendMessageAsync("Servindo a frase diária novamente, em um instante...");
            await Program.client.SendMessageAsync(context.Channel, $"Mostrando a frase de {Program.modulo_Frases.diaUltimoEnvio.ToString(Program.config.Program_LocalCulture)}");
            await Program.modulo_Frases.Send();
            await Program.client.SendMessageAsync(context.Channel, Program.GetTaskDoneMessage());

            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Frase enviada");
        }

        [Command("FraseAleatoria")]
        [Aliases("FraseRandom", "RandomFrase", "FraseAleatória")]
        public async Task Frases_fraseAleatoria(CommandContext context)
        {
            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Mostrando uma frase aleatória, pedido por: {context.User.Username} ({context.User.Id})");

            await Program.client.SendMessageAsync(context.Channel, $"Pegando uma frase do \"cardápio\", um momento...");

            int index = Program.modulo_Frases.random.Next(0, Program.modulo_Frases.quantiaDeFrases);

            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Procurando pela frase de índice {index}");

            DiscordMessage frase;

            if (index < 100)
            {
                var frases = await Program.modulo_Frases.canalDeFrases.GetMessagesAsync(100);
                frase = frases[index];
            }
            else
            {
                IReadOnlyList<DiscordMessage> frases = null;
                // pega a primeira mensagem para servir de base
                var primeirasMsgs = await Program.modulo_Frases.canalDeFrases.GetMessagesAsync(100);
                DiscordMessage msgPivot = primeirasMsgs[99];
                int currentIndex = index; // usado para pegar a mensagens anteriores

                // "Procura" a mensagem do índice passado, até puxar as mensagens dentro desse índice
                while (currentIndex > 99)
                {
                    await Task.Delay(300);
                    frases = await Program.modulo_Frases.canalDeFrases.GetMessagesBeforeAsync(msgPivot.Id, 100);
                    currentIndex -= 100;
                    msgPivot = frases[99];
                }

                frase = frases[currentIndex];
            }

            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Frase do dia escolhida: {frase.Content} escrita por {frase.Author.Username}");

            Console.WriteLine("(Command.FraseDoDia_fraseAleatoria) Construindo mensagem para envio");

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.Title = "Frase Aleatória";
            embedBuilder.Color = new Optional<DiscordColor>(new DiscordColor("6225b8"));
            embedBuilder.Footer = new DiscordEmbedBuilder.EmbedFooter();
            embedBuilder.Footer.Text = $"Frase cunhada por {frase.Author.Username}";
            embedBuilder.Footer.IconUrl = frase.Author.AvatarUrl;
            embedBuilder.Timestamp = frase.CreationTimestamp;
            embedBuilder.Description = frase.Content;

            DiscordEmbed embed = embedBuilder.Build();

            Console.WriteLine("(Command.FraseDoDia_fraseAleatoria) Mensagem construída");
            Console.WriteLine("(Command.FraseDoDia_fraseAleatoria) Iniciando envio da mensagem pelo client");

            await context.Channel.SendMessageAsync(embed);
            await Program.client.SendMessageAsync(context.Channel, Program.GetTaskDoneMessage());

            Console.WriteLine("(Command.FraseDoDia_fraseAleatoria) Mensagem enviada pelo client");
            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Frase enviada");
        }



        [Command("PersonalizadaRapida")]
        [Aliases("PersonalizadaRápida", "PersoRapida", "PersoRápida", "fastPerso", "fastPersonalizada", "rápidaPersonalizada", "rapidaPersonalizada", "rapidaPerso", "rápidaPerso")]
        public async Task Jogos_PersoFast(CommandContext context)
        {
            await Program.modulo_Jogos.Personalizada_SortearTimes_fast(context.Member, context.Message);
        }

        [Command("PersonalizadaRapida")]        
        public async Task Jogos_PersoFast(CommandContext context, uint max)
        {
            await Program.modulo_Jogos.Personalizada_SortearTimes_fast(context.Member, context.Message, max);
        }

        [Command("ValorantMapa")]
        [Aliases("ValorantMap", "ValorantSortearMapa", "ValorantMapSort", "ValorantSortearMap", "ValorantSortearMapas", "ValorantMapaSortear")]
        public async Task Jogos_Valorant_SortearMapa(CommandContext context)
        {
            await Jogos_Valorant_SortearMapa(context, true);
        }

        [Command("ValorantMapa")]
        public async Task Jogos_Valorant_SortearMapa(CommandContext context, bool onlyRotation)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();

            Console.WriteLine("(Jogos/Valorant) Sorteando mapa");

            await context.Channel.SendMessageAsync("Sorteando um mapa...");

            ValorantMapa mapaEscolhido = await Program.modulo_Jogos.Valorant_SortearMapa(onlyRotation);

            Console.WriteLine("(Jogos/Valorant) Mapa sorteado");

            builder.Title = mapaEscolhido.Name;
            builder.Color = DiscordColor.Red;
            builder.ImageUrl = mapaEscolhido.ImageURL;

            
            await context.Channel.SendMessageAsync(builder);
            await context.Channel.SendMessageAsync(Program.GetTaskDoneMessage());
        }



        [Command("Shutdown")]
        [Aliases("Kill")]
        public async Task Program_Close(CommandContext context)
        {
            if (context.User.Id == Program.config.Program_AdminID)
            {
                Console.WriteLine("(Program) Shutting Down");
                Program.Program_Closing(this, EventArgs.Empty);
                Environment.Exit(0);
            }
        }        



    }

}
