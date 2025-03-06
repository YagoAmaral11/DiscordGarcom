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

        public static bool VerifyAdmin(DiscordUser author)
        {
            if (author.Id == Program.config.Program_AdminID)
                return true;
            return false;
        }



        [Group("Ajuda")]        
        [Aliases("h", "?", "Help")]
        public class HelpCommands
        {
            
            [GroupCommand()]
            public async Task Frases(CommandContext context)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                embed.Title = "Comandos de frase";
                embed.Color = DiscordColor.Green;
                embed.Description = "";

                embed.Description += "**Garçom, FraseDiária** *(fd, FraseDoDia, FraseDaily)*: Mostra qual foi a frase do dia do frases\n";
                embed.Description += "**Garçom, FraseAleatória** *(fa, FraseRandom, RandomFrase)*: Envia uma frase aleatória do frases\n";                

                await context.Channel.SendMessageAsync(embed.Build());  
            }

            [GroupCommand()]
            public async Task Jukebox(CommandContext context)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                embed.Title = "Comandos da Jukebox";
                embed.Color = DiscordColor.Green;
                embed.Description = "";

                embed.Description += "**Garçom, tocar** *(p, Play)*: Adiciona uma música na fila";
                embed.Description += "**Garçom, parar** *(dc, Stop, disconnect)*: Disconecta o bot e desliga a jukebox";
                embed.Description += "**Garçom, pausa** *(ps, Pause)*: Pausa e despausa a jukebox";
                embed.Description += "**Garçom, pular** *(skp, skip, skipar)*: Pausa e despausa a jukebox";
                embed.Description += "**Garçom, fila** *(q, ls, queue, listar, musicas, tocando, playing)*: Mostra a música que está tocando agora e a fila de músicas";
                embed.Description += "**Garçom, remover <Índice>** *(r, remove, filaRemover)*: Remove uma música da fila de músicas, de acordo com o índice";
                embed.Description += "**Garçom, jump <Índice>** *(jmp, skipTo, pularPara, filaPular, queueJump, queueSkip)*: Pula até a música do índice";
                embed.Description += "**Garçom, próxima <Índice>** *(nxt, next, QueueNext, filaProxima, filaNext)*: Seleciona a música do índice para ser a próxima à tocar";
                embed.Description += "**Garçom, adiantar <Índice>** *(qp, TocarDaFila, QueuePlay, PlayQueue)*: Pula a música atual e toca à do índice";

                await context.Channel.SendMessageAsync(embed.Build());
            }

            [GroupCommand()]
            public async Task Perso(CommandContext context)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                embed.Title = "Comandos da Personalizada";
                embed.Color = DiscordColor.Green;
                embed.Description = "";

                embed.Description += "**Garçom, PersonalizadaRápida** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call";
                embed.Description += "**Garçom, PersonalizadaRápida <Máximo por time>** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call, com um número máximo de jogadores por time";

                await context.Channel.SendMessageAsync(embed.Build());
            }

            [GroupCommand()]
            public async Task Valorant(CommandContext context)
            {
                DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

                embed.Title = "Comandos de Valorant";
                embed.Color = DiscordColor.Green;
                embed.Description = "";

                embed.Description += "**Garçom, valorantMapa** *(vlmp, valmp, valMap, ValorantMap, ValorantSortearMapa, ValorantMapSort)*: Escolhe um mapa aleatória da rotação do valorant";
                embed.Description += "**Garçom, valorantMapa <Rotação: true or false>** *(vlmp, valmp, valMap, ValorantMap, ValorantSortearMapa, ValorantMapSort)*: Escolhe um mapa aleatória do valorant, da rotação ou não";

                await context.Channel.SendMessageAsync(embed.Build());
            }

        }



        [Command("FraseDiaria")]
        [Aliases("fd", "FraseDoDia" , "FraseDiária", "FraseDaily", "DailyFrase")]
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
            await Program.modulo_Frases.Send(context.Channel);
            await Program.client.SendMessageAsync(context.Channel, Program.GetTaskDoneMessage());

            Console.WriteLine($"(Command.FraseDoDia_mostrarFrase) Frase enviada");
        }

        [Command("FraseAleatoria")]
        [Aliases("fa", "FraseRandom", "RandomFrase", "FraseAleatória")]
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

        [Command("FrasesFetch")]
        [Aliases("ff")]
        public async Task Frases_fetch(CommandContext context)
        {
            if (VerifyAdmin(context.User) == false)
                return;

            await Program.modulo_Frases.Fetch();
            await context.Channel.SendMessageAsync("Dando fetch nas frases");
        }



        [Command("PersonalizadaRapida")]
        [Aliases("fp", "PersonalizadaRápida", "PersoRapida", "PersoRápida", "fastPerso", "fastPersonalizada", "rápidaPersonalizada", "rapidaPersonalizada", "rapidaPerso", "rápidaPerso")]
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
        [Aliases("vlmp", "valmp", "valMap", "ValorantMap", "ValorantSortearMapa", "ValorantMapSort", "ValorantSortearMap", "ValorantSortearMapas", "ValorantMapaSortear")]
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

        

        [Command("Tocar")]
        [Aliases("p", "Play")]
        public async Task Jukebox_Play(CommandContext context, [RemainingText] string link)
        {            
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Play(pedinte, canalDeVoz, canalDeTexto, link);
        }

        [Command("Parar")]
        [Aliases("dc", "Stop", "Disconnect")]
        public async Task Jukebox_Stop(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Stop(pedinte, canalDeVoz, canalDeTexto);
        }

        [Command("Pausar")]
        [Aliases("ps", "Pause")]
        public async Task Jukebox_Pause(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Pause(canalDeVoz, canalDeTexto);
        }

        [Command("Pular")]
        [Aliases("skp", "Skip", "Skipar")]
        public async Task Jukebox_Skip(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Skip(canalDeVoz, canalDeTexto);
        }

        [Command("Fila")]
        [Aliases("q", "Lista", "Listar", "Musicas", "Queue", "List", "Ls", "Musics", "Tocando", "Playing")]
        public async Task Jukebox_QueueShow(CommandContext context)
        {            
            DiscordChannel canalDeTexto = context.Channel;

            await Program.modulo_Jukebox.QueueShow(canalDeTexto);
        }
        
        [Command("Remover")]
        [Aliases("r", "Remove", "FilaRemover", "QueueRemove")]
        public async Task Jukebox_QueueRemove(CommandContext context, int index)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueueRemove(canalDeVoz, canalDeTexto, index);
        }

        [Command("PularPara")]
        [Aliases("jmp", "Jump", "SkipTo", "QueueJump", "QueueSkip", "FilaPular")]
        public async Task Jukebox_QueueSkipTo(CommandContext context, int index)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueueSkipTo(canalDeVoz, canalDeTexto, index);
        }

        [Command("Próxima")]
        [Aliases("nxt", "Proxima", "Next", "QueueNext", "FilaProxima", "FilaNext")]
        public async Task Jukebox_QueuePriorityNext(CommandContext context, int index)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueuePriorityNext(canalDeVoz, canalDeTexto, index);
        }

        [Command("Adiantar")]
        [Aliases("qp", "TocarDaFila", "QueuePlay", "PlayQueue")]
        public async Task Jukebox_QueuePriorityPlay(CommandContext context, int index)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await Jukebox.PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueuePriorityPlay(canalDeVoz, canalDeTexto, index);
        }



        [Command("Shutdown")]
        [Aliases("Kill")]
        public async Task Program_Close(CommandContext context)
        {
            if (VerifyAdmin(context.User) == false)
                return;

            Console.WriteLine("(Program) Shutting Down");
            Program.Program_Closing(this, EventArgs.Empty);
            Environment.Exit(0);
        }        

    }

}
