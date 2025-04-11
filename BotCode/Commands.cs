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

        // Verifica se o usuário está em um VC e ele é válido
        public static async Task<bool> PreVerify(DiscordMember pedinte, DiscordChannel canalDeTexto, DiscordChannel canalDeVoz)
        {
            if (pedinte.VoiceState == null)
            {
                await canalDeTexto.SendMessageAsync("Você deve estar em um canal de voz para usar esse comando");
                return false;
            }

            if (canalDeVoz.Type != ChannelType.Voice)
            {
                await canalDeTexto.SendMessageAsync("Você deve estar em um canal de voz para usar esse comando");
                return false;
            }

            return true;
        }

        [Command("Ajuda")]
        [Aliases("h", "help", "?")]
        public async Task Ajuda_Geral(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Ajuda sobre comandos";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, AjudaFrases**: Mostra ajuda relacionada ao módulo de Frases\n";
            embed.Description += "**Garçom, AjudaJukebox**: Mostra ajuda relacionada ao módulo de música, o Jukebox\n";
            embed.Description += "**Garçom, AjudaPerso**: Mostra ajuda relacionada ao módulo de Personalizada\n";
            embed.Description += "**Garçom, AjudaValorant**: Mostra ajuda relacionada ao módulo de Valorant\n";
            embed.Description += "**Garçom, AjudaUtil**: Mostra ajuda relacionada ao módulo de utilidades\n";
            embed.Description += "**Garçom, AjudaCanalTemp**: Mostra ajuda relacionada ao módulo de canais temporários\n";


            embed.Description += "**Garçom, Prefixos**: Mostra todos os prefixos aceitos pelo Garçom\n";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaFrases")]
        [Aliases("?Frases")]
        public async Task Ajuda_Frases(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Comandos de frase";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, FraseDiária** *(fd, FraseDoDia, FraseDaily)*: Mostra qual foi a frase do dia do frases\n";
            embed.Description += "**Garçom, FraseAleatória** *(fa, FraseRandom, RandomFrase)*: Envia uma frase aleatória do frases\n";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaJukebox")]
        [Aliases("?Jukebox")]
        public async Task Ajuda_Jukebox(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Comandos da Jukebox";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, tocar** *(p, Play)*: Adiciona uma música na fila\n";
            embed.Description += "**Garçom, parar** *(dc, Stop, disconnect)*: Disconecta o bot e desliga a jukebox\n";
            embed.Description += "**Garçom, pausa** *(ps, Pause)*: Pausa e despausa a jukebox\n";
            embed.Description += "**Garçom, pular** *(skp, skip, skipar)*: Pula a música atual e toca a próxima da fila\n";
            embed.Description += "**Garçom, fila** *(q, ls, queue, listar, musicas, tocando, playing)*: Mostra a música que está tocando agora e a fila de músicas\n";
            embed.Description += "**Garçom, remover <Índice>** *(r, remove, filaRemover)*: Remove uma música da fila de músicas, de acordo com o índice\n";
            embed.Description += "**Garçom, jump <Índice>** *(jmp, skipTo, pularPara, filaPular, queueJump, queueSkip)*: Pula até a música do índice\n";
            embed.Description += "**Garçom, próxima <Índice>** *(nxt, next, QueueNext, filaProxima, filaNext)*: Seleciona a música do índice para ser a próxima à tocar\n";
            embed.Description += "**Garçom, adiantar <Índice>** *(qp, TocarDaFila, QueuePlay, PlayQueue)*: Pula a música atual e toca à do índice\n";
            embed.Description += "**Garçom, +10**: Pula 10 segundos no player\n";
            embed.Description += "**Garçom, -10**: Volta 10 segundos no player\n";
            embed.Description += "**Garçom, tempo <Horas:Minutos:Segundos>** *(tm, seek)*: Coloca o player no tempo enviado\n";
            embed.Description += "**Garçom, reiniciar** *(rw, restart, rewind)*: Coloca o player no início da música\n";
            embed.Description += "**Garçom, limparfila** *(qc, QueueClear, QueueReset, FilaLimpar, FilaResetar, FilaReiniciar)*: Limpa todas as músicas da fila\n";
            embed.Description += "**Garçom, embaralhar** *(jshf, shuffle, FilaEmbaralhar, FilaAleatorizar, QueueShuffle, QueueRandomize)*: Embaralha a fila de músicas\n";

            embed.Description += "\n\nO bot travou? Tente usar **Garçom, jrc**";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaPerso")]
        [Aliases("?Perso", "AjudaPersonalizada", "?Personalizada")]
        public async Task Ajuda_Perso(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Comandos da Personalizada";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, PersonalizadaRápida** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call\n";
            embed.Description += "**Garçom, PersonalizadaRápida <Máximo por time>** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call, com um número máximo de jogadores por time\n";
            embed.Description += "**Garçom, PersonalizadaRápida <Máximo por time> <Menção à usuários>** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call, com um número máximo de jogadores por time e retirando do sorteio os usuários mencionados, separados por espaço\n";
            embed.Description += "**Garçom, PersonalizadaRápida <Menção à usuários>** *(fp, fastPerso, persoRápida)*: Divide dois times de acordo com os integrantes da call, retirando do sorteio os usuários mencionados, separados por espaço\n";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaValorant")]
        [Aliases("?Valorant")]
        public async Task Ajuda_Valorant(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Comandos de Valorant";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, valorantMapa** *(vlmp, valmp, valMap, ValorantMap, ValorantSortearMapa, ValorantMapSort)*: Escolhe um mapa aleatória da rotação do valorant\n";
            embed.Description += "**Garçom, valorantMapa <Rotação: true or false>** *(vlmp, valmp, valMap, ValorantMap, ValorantSortearMapa, ValorantMapSort)*: Escolhe um mapa aleatória do valorant, da rotação ou não\n";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaUtil")]
        [Aliases("?Util")]
        public async Task Ajuda_Utility(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Comandos Utilitários";
            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, MencionarDeafen** *(udm, MentionDeafen, DeafenMention)*: Menciona todos os usuários que estão no deafen na call que está conectado\n";
            embed.Description += "**Garçom, Contar** *(count, voiceCount, vcc)*: Conta todos os usuários que estão na call\n";
            embed.Description += "**Garçom, Status**: Mostra o status do bot, seu uptime, etc.\n";
            embed.Description += "**Garçom, Mover <Menção da Call Destino> <Menção da Call Original>** *(mv, move)*: Move todos os usuários da call original para a call destino, se tiver permissões. Use #! para mencionar canais de audio";

            embed.Description += "**Garçom, Shutdown** *(kill)*: ***APENAS ADMs*** Desliga o bot\n";

            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("AjudaCanalTemp")]
        [Aliases("?CanalTemp", "?TempChannel")]
        public async Task Ajuda_ChannelManager(CommandContext context)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Color = DiscordColor.Green;
            embed.Description = "";

            embed.Description += "**Garçom, CanalTemporario** *(tmpC, tempChannel, temporarioCanal)*: Cria um canal temporário, que será destruído automaticamente, depois três horas\n";
            embed.Description += "**Garçom, CanalTemporario <Duração: XXdYYhZZmWWs>** *(tmpC, tempChannel, temporarioCanal)*: Cria um canal temporário, que será destruído automaticamente, depois de um certo tempo\n";
            embed.Description += "**Garçom, CanalTemporario <Duração: XXdYYhZZmWWs> <Nome do Canal>** *(tmpC, tempChannel, temporarioCanal)*: Cria um canal temporário, que será destruído automaticamente, depois de um certo tempo, com o nome passado\n";            

            await context.Channel.SendMessageAsync(embed.Build());
        }


        [Command("Shutdown")]
        [Aliases("Kill")]
        public async Task Utility_Close(CommandContext context)
        {
            if (VerifyAdmin(context.User) == false)
                return;

            Console.WriteLine("(Utility) Shutting Down");
            Program.Program_Closing(this, EventArgs.Empty);
            Environment.Exit(0);
        }

        [Command("MencionarDeafen")]
        [Aliases("udm", "MentionDeafen", "DeafenMention")]
        [Cooldown(3, 10, CooldownBucketType.User)]
        public async Task Utility_MentionDeafen(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            Console.WriteLine("(Utility) Mencionando todos os usuários no deafen");

            string mentions = string.Empty;

            foreach (var user in canalDeVoz.Users)
            {
                if (user.VoiceState.IsSelfDeafened)
                {
                    mentions += $"{user.Mention} ";
                }
            }

            if (mentions != string.Empty)
            {
                await canalDeTexto.SendMessageAsync(mentions);
            }
            else
            {
                await canalDeTexto.SendMessageAsync("Nenhum usuário elegível");
            }
            
            await canalDeTexto.SendMessageAsync(Program.GetTaskDoneMessage());
        }

        [Command("Prefixos")]
        [Aliases("pfx", "Prefixes")]
        public async Task Utility_ShowPrefixes(CommandContext context)
        {
            string tmp = "";

            Console.WriteLine($"Quantia de prefixos: {Program.config.Prefixs.Count}");            

            // HACK:
            // A lista de prefixo vem duplicado por causa da deserialização (???)
            // Isso faz com que ela seja percorrida só uma vez
            for (int i = 0; i < Program.config.Prefixs.Count / 2; i++)
            {
                tmp += $"**{Program.config.Prefixs[i]}**\n";
            }

            await context.Channel.SendMessageAsync("Mostrando todos os prefixos do Garçom:");
            await context.Channel.SendMessageAsync(tmp);
            await context.Channel.SendMessageAsync(Program.GetTaskDoneMessage());
        }

        [Command("Contar")]
        [Aliases("Count", "VoiceCount", "vcc")]
        public async Task Utility_CountUsersInVoice(CommandContext context)
        {
            DiscordMember sender = context.Member;
            DiscordChannel channel = context.Channel;

            DiscordVoiceState voiceState = sender.VoiceState; // Serve para ver em qual canal da call está o usuário.

            if (voiceState == null)
            {
                await Program.client.SendMessageAsync(channel, "Para usar esse comando você deve estar conectado em um canal de voz");
                return;
            }

            int All = voiceState.Channel.Users.Count;
            int Bots = 0;
            bool selfConnected = false;
            
            foreach (DiscordMember user in voiceState.Channel.Users)
            {
                if (user.IsBot)
                    Bots++;

                if (user.Id == Program.client.CurrentUser.Id)
                    selfConnected = true;
            }

            if (selfConnected)
            {
                if (Bots > 1)
                {
                    await channel.SendMessageAsync($"No total, tem {All} usuários conectados em {voiceState.Channel.Mention}. {All - Bots} desses são pessoas, {Bots} são bots (contando comigo).");
                }
                else
                {
                    await channel.SendMessageAsync($"No total, tem {All} usuários conectados em {voiceState.Channel.Mention}. {All - 1} desses são pessoas e o outro sou eu");
                }
            }
            else
            {
                if (Bots > 0)
                {
                    await channel.SendMessageAsync($"No total, tem {All} usuários conectados em {voiceState.Channel.Mention}. {All - Bots} desses são pessoas, {Bots} são bots.");
                }
                else
                {
                    await channel.SendMessageAsync($"No total, tem {All} pessoas conectadas em {voiceState.Channel.Mention}.");
                }
            }

        }

        [Command("Status")]
        public async Task Utility_BotStatus(CommandContext context)
        {
            DiscordEmbedBuilder embed = new();

            embed.Color = DiscordColor.Chartreuse;
            embed.Title = "Garçom tá ON!";
            embed.Description = $"**Versão**: {Program.BotVersion}\n**Online desde**: {Program.InitialTime}";
            embed.Description += $"\n\n**Changelog**\n{Program.Changelog}";
            
            await context.Channel.SendMessageAsync(embed.Build());
        }

        [Command("Mover")]
        [Aliases("Mv", "Move")]
        public async Task Utility_MoveVc(CommandContext context, string MençãoCanalDestino, string MençãoCanalParaMover)
        {
            string channelTargetId = MençãoCanalDestino.Substring(MençãoCanalDestino.IndexOf('#') + 1, MençãoCanalDestino.Length - 3);
            string channelInitialId = MençãoCanalParaMover.Substring(MençãoCanalParaMover.IndexOf('#') + 1, MençãoCanalParaMover.Length - 3);

            DiscordMember member = await Program.servidor.GetMemberAsync(context.User.Id);

            if (member.Permissions.HasPermission(Permissions.MoveMembers) || member.Permissions.HasPermission(Permissions.Administrator))
            {
                DiscordChannel target = Program.servidor.GetChannel(ulong.Parse(channelTargetId));
                DiscordChannel initial = Program.servidor.GetChannel(ulong.Parse(channelInitialId));

                if (target.Type != ChannelType.Voice || initial.Type != ChannelType.Voice)
                {
                    await context.RespondAsync("Algum desses canais não são canais de audio");
                    return;
                }

                foreach (var User in initial.Users)
                {
                    await target.PlaceMemberAsync(User);
                }
            }
            else
            {
                await context.RespondAsync("Somente quem tem permissão para mover usuários pode usar esse comando");
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

        [Command("PersonalizadaRapida")]
        public async Task Jogos_PersoFast(CommandContext context, params string[] excludedPlayers)
        {
            await Program.modulo_Jogos.Personalizada_SortearTimes_fast(context.Member, context.Message, 5, excludedPlayers);
        }

        [Command("PersonalizadaRapida")]
        public async Task Jogos_PersoFast(CommandContext context, uint max, params string[] excludedPlayers)
        {
            await Program.modulo_Jogos.Personalizada_SortearTimes_fast(context.Member, context.Message, max, excludedPlayers);
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
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
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueuePriorityPlay(canalDeVoz, canalDeTexto, index);
        }

        [Command("JukeboxRC")]
        [Aliases("jrc")]
        public async Task Jukebox_ResetConnection(CommandContext context)   
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            if (!await Program.modulo_Jukebox.VerifyUsage(canalDeVoz, canalDeTexto) || !await Program.modulo_Jukebox.VerifyWhitelist(canalDeTexto))
            {
                return;
            }

            Program.modulo_Jukebox.ResetConnection();
        }

        [Command("+10")]
        public async Task Jukebox_plus10(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Jump10(canalDeVoz, canalDeTexto);
        }

        [Command("-10")]
        public async Task Jukebox_back10(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Back10(canalDeVoz, canalDeTexto);
        }

        [Command("Tempo")]
        [Aliases("tm", "seek")]
        public async Task Jukebox_Seek(CommandContext context, TimeSpan timeSpan)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Seek(canalDeVoz, canalDeTexto, timeSpan);
        }

        [Command("Reiniciar")]
        [Aliases("rw", "restart", "rewind")]
        public async Task Jukebox_Restart(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Restart(canalDeVoz, canalDeTexto);
        }

        [Command("LimparFila")]
        [Aliases("qc", "QueueClear", "QueueReset", "FilaLimpar", "FilaResetar", "FilaReiniciar")]
        public async Task Jukebox_QueueClear(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.QueueClear(canalDeVoz, canalDeTexto);
        }

        [Command("Embaralhar")]
        [Aliases("jshf", "Shuffle", "FilaEmbaralhar", "FilaAleatorizar", "QueueShuffle", "QueueRandomize")]
        public async Task Jukebox_Shuffle(CommandContext context)
        {
            DiscordMember pedinte = context.Member;
            DiscordChannel canalDeVoz = context.Member.VoiceState?.Channel;
            DiscordChannel canalDeTexto = context.Channel;

            // Pré verificações
            if (!await PreVerify(pedinte, canalDeTexto, canalDeVoz))
            {
                return;
            }

            await Program.modulo_Jukebox.Shuffle(canalDeVoz, canalDeTexto);
        }



        private async Task TempChannel_NewModel(TimeSpan duration, CommandContext context, string ChannelName)
        {
            DateTime data = Program.GetTime() + duration;

            var reg = await Program.modulo_GenDeCanal.NovoCanalTemporário(data, ChannelName, context.User);

            if (reg == null)
            {
                await context.RespondAsync($"Você já criou {Program.config.ChannelManager_MaxTempPerUser} canais temporários, o máximo permitido");
                return;
            }

            // Mensagem de resposta
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            embed.Color = DiscordColor.IndianRed;
            embed.Title = "Canal Temporário";
            embed.Description = $"Novo canal {reg.canal.Mention} criado!\n**Responsável**: {context.User.Username}\n**Duração**: {duration}";
            await context.RespondAsync(embed.Build());
            await context.Channel.SendMessageAsync(Program.GetTaskDoneMessage());
        }

        private async Task TempChannel_NewPrivateModel(TimeSpan duration, CommandContext context, string ChannelName)
        {
            DateTime data = Program.GetTime() + duration;

            var reg = await Program.modulo_GenDeCanal.NovoCanalTemporárioPrivado(data, context.User, ChannelName);

            if (reg == null)
            {
                await context.RespondAsync($"Você já criou {Program.config.ChannelManager_MaxTempPerUser} canais temporários, o máximo permitido");
                return;
            }

            // Mensagem de resposta
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();
            embed.Color = DiscordColor.IndianRed;
            embed.Title = "Canal Temporário";
            embed.Description = $"Novo canal privado {reg.canal.Mention} criado!\n**Responsável**: {context.User.Username}\n**Duração**: {duration}";
            await context.RespondAsync(embed.Build());
            await context.Channel.SendMessageAsync(Program.GetTaskDoneMessage());
        }

        [Command("canalTemporario")]
        [Aliases("tmpC", "tempC", "tempChannel", "TemporarioCanal")]
        public async Task TempChannel_New(CommandContext context)
        {
            // Cria canal de acordo com o tempo especificado            
            await TempChannel_NewModel(new TimeSpan(3, 0, 0), context, $"🕙 Canal de {context.User.Username}");            
        }

        [Command("canalTemporario")]        
        public async Task TempChannel_New(CommandContext context, TimeSpan lifespan)
        {
            // Máixmo de tempo
            if (lifespan.TotalDays > 1)
            {
                await context.RespondAsync("Canais temporários só podem ter no máximo 1 dia de duração");
                return;
            }

            // Cria canal de acordo com o tempo especificado            
            await TempChannel_NewModel(lifespan, context, $"🕙 Canal de {context.User.Username}");
        }

        [Command("canalTemporario")]        
        public async Task TempChannel_New(CommandContext context, TimeSpan lifespan, [RemainingText] string NomeDoCanal)
        {
            // Máixmo de tempo
            if (lifespan.TotalDays > 1)
            {
                await context.RespondAsync("Canais temporários só podem ter no máximo 1 dia de duração");
                return;
            }

            // Cria canal de acordo com o tempo especificado            
            await TempChannel_NewModel(lifespan, context, $"🕙 {NomeDoCanal}");
        }

        [Command("canalTemporarioPrivado")]
        [Aliases("tmpCp", "tempCp", "tempChannelPrivate", "TemporarioCanalPrivado", "CanalPrivado", "tmppvd")]
        public async Task TempChannel_NewPrivate(CommandContext context)
        {
            await TempChannel_NewPrivateModel(new TimeSpan(3, 0, 0), context, $"🔐 Canal de {context.User.Username}");
        }

        [Command("canalTemporarioPrivado")]        
        public async Task TempChannel_NewPrivate(CommandContext context, TimeSpan lifespan)
        {
            // Máixmo de tempo
            if (lifespan.TotalDays > 1)
            {
                await context.RespondAsync("Canais temporários só podem ter no máximo 1 dia de duração");
                return;
            }

            await TempChannel_NewPrivateModel(lifespan, context, $"🔐 Canal de {context.User.Username}");
        }

        [Command("canalTemporarioPrivado")]
        public async Task TempChannel_NewPrivate(CommandContext context, TimeSpan lifespan, [RemainingText] string NomeDoCanal)
        {
            // Máixmo de tempo
            if (lifespan.TotalDays > 1)
            {
                await context.RespondAsync("Canais temporários só podem ter no máximo 1 dia de duração");
                return;
            }

            await TempChannel_NewPrivateModel(lifespan, context, $"🔐 {NomeDoCanal}");
        }

    }
}
