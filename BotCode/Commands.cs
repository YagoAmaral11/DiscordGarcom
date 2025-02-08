using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Remoting.Messaging;
using DSharpPlus.Entities;

namespace GarçomDoKitts
{
    public class Commands : BaseCommandModule
    {

        [Command("FraseDiaria")]
        public async Task FraseDoDia_mostrarFrase(CommandContext context)
        {
            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Mostrando a frase diária, pedido por: {context.User.Username} ({context.User.Id})");

            if (Program.dailyFrase.fraseDoDia == null)
            {
                Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Frase do Dia é nula, impossível mandar frase");
                await Program.client.SendMessageAsync(context.Channel, "Não existe frase disponível");
                return;
            }

            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Mandando a frase do dia");

            await context.Channel.SendMessageAsync("Servindo a frase diária novamente, em um instante...");
            await Program.client.SendMessageAsync(context.Channel, $"Mostrando a frase de {Program.dailyFrase.DiaDoUltimoEnvio}");
            await Program.dailyFrase.Send();
            await Program.client.SendMessageAsync(context.Channel, $"Aqui está!");

            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Frase enviada");
        }

        [Command("FraseAleatoria")]
        public async Task FraseDoDia_fraseAleatoria(CommandContext context)
        {
            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Mostrando uma frase aleatória, pedido por: {context.User.Username} ({context.User.Id})");

            await Program.client.SendMessageAsync(context.Channel, $"Pegando uma frase do \"cardápio\", um momento...");

            int index = Program.dailyFrase.random.Next(0, Program.dailyFrase.quantiaDeFrases);

            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Procurando pela frase de índice {index}");

            DiscordMessage frase;

            if (index < 100)
            {
                var frases = await Program.dailyFrase.canalDeFrases.GetMessagesAsync(100);
                frase = frases[index];
            }
            else
            {
                IReadOnlyList<DiscordMessage> frases = null;
                // pega a primeira mensagem para servir de base
                var primeirasMsgs = await Program.dailyFrase.canalDeFrases.GetMessagesAsync(100);
                DiscordMessage msgPivot = primeirasMsgs[99];
                int currentIndex = index; // usado para pegar a mensagens anteriores

                // "Procura" a mensagem do índice passado, até puxar as mensagens dentro desse índice
                while (currentIndex > 99)
                {
                    await Task.Delay(300);
                    frases = await Program.dailyFrase.canalDeFrases.GetMessagesBeforeAsync(msgPivot.Id, 100);
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
            await Program.client.SendMessageAsync(context.Channel, $"Aqui está!");

            Console.WriteLine("(Command.FraseDoDia_fraseAleatoria) Mensagem enviada pelo client");
            Console.WriteLine($"(Command.FraseDoDia_fraseAleatoria) Frase enviada");
        }

    }

}
