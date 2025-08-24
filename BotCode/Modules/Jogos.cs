using DSharpPlus;
using DSharpPlus.Entities;
using GarçomDoKitts.configs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.EventArgs;

namespace GarçomDoKitts
{
    public class Jogos
    {
        [JsonIgnore] public static readonly string dataPath = $"{DataIO.DataFolderPath}valorantMaps.json";
        [JsonIgnore] public const string JogosButtons = "Jogos_";
        [JsonIgnore] public const string moverTimeButton = $"{JogosButtons}moveTeam:"; // serve para identificar botões do discord para mover time gerados para uma call temporária

        [JsonIgnore] Random sorteador = new Random();
        [JsonIgnore] ValorantMapas Valorant_Mapas = new ValorantMapas();
        [JsonIgnore] public CircularBuffer<TimesGerados> timesGerados = new(10);
        [JsonIgnore] public DiscordChannel canalDeLobby;

        public async Task Init()
        {
            sorteador = new Random();
            Valorant_Mapas = await DataIO.Load(dataPath, typeof(ValorantMapas)) as ValorantMapas;
            canalDeLobby = await Program.servidor.GetChannelAsync(Program.config.Jogos_CanalDeLobby);

            Program.OnComponentInteractionCreated += OnComponentInteractionCreated; 
        }

        // Events
        public async Task OnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreatedEventArgs args)
        {
            await InteraçãoDeComponente(sender, args);
        }

        // Serve para verificar se o usuário clicou em uma interação
        private async Task InteraçãoDeComponente(DiscordClient sender, ComponentInteractionCreatedEventArgs args)
        {            
            string buttonId = args.Interaction.Data.CustomId;

            if (!buttonId.StartsWith(JogosButtons))
                return;

            if (buttonId.StartsWith(moverTimeButton))
            {
                int startOfTeamId = buttonId.IndexOf(':') + 1;
                string UUID = buttonId.Substring(buttonId.IndexOf(';') + 1);
                
                await Personalizada_MoverTimes(UUID, buttonId[startOfTeamId], args);
                return;
            }
        }


        public string SortFromStringList(List<string> list) => list[sorteador.Next(0, list.Count - 1)];

        public async Task Personalizada_SortearTimes_fast(DiscordMember member, DiscordChannel messageChannel, uint maxPorTime = 5, string[] excludedPlayers = null)
        {
            DiscordVoiceState voiceState = member.VoiceState; // Serve para ver em qual canal da call está o usuário.            

            // Verificações Iniciais
            if (voiceState == null)
            {
                await Program.client.SendMessageAsync(messageChannel, "Para usar esse comando você deve estar conectado em um canal de voz");
                return;
            }

            DiscordChannel voiceChatChannel = await voiceState.GetChannelAsync();

            if (voiceChatChannel.Users.Count < 3)
            {
                await Program.client.SendMessageAsync(messageChannel, "Devem ter mais de 2 usuários na call para usar esse comando");
                return;
            }
            
            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder();            
            messageBuilder.WithContent("Sorteando times...");
            await messageBuilder.SendAsync(messageChannel);            

            List<DiscordMember> jogadores = new(voiceChatChannel.Users);            
            List<DiscordMember> timeA = new();
            List<DiscordMember> timeB = new();
            List<DiscordMember> sobra = new();

            List<DiscordMember> tmp = new(jogadores);

            // Parte para excluir jogadores do sorteio
            foreach (DiscordMember jogador in tmp)
            {
                if (jogador.Id == Program.client.CurrentUser.Id)
                {
                    jogadores.Remove(jogador);
                    continue;
                }

                if (excludedPlayers == null)
                    continue;

                foreach (string user in excludedPlayers)
                {                    
                    if ($"<@{jogador.Id}>" == user)
                    {
                        jogadores.Remove(jogador);
                    }
                }
            }            

            uint jogadoresTotal = (uint) jogadores.Count;
            uint jogadoresPorTime = (uint) jogadoresTotal / 2;

            if (jogadoresPorTime > maxPorTime)
            {
                jogadoresPorTime = maxPorTime;
            }

            uint jogadoresDeSobra = (uint) jogadoresTotal - jogadoresPorTime * 2;

            // Sorteia até um time encher,
            // Depois, sorteia jogadores para fechar o outro time.
            // Se sobrar algum jogador ainda, jogar para a sobra/outra.
            for (int i = 0; i < jogadoresPorTime; i++)
            {
                int index = sorteador.Next(0, jogadores.Count);
                timeA.Add(jogadores[index]);
                jogadores.Remove(jogadores[index]);
            }            

            for (int i = 0; i < jogadoresPorTime; i++)
            {
                int index = sorteador.Next(0, jogadores.Count);
                timeB.Add(jogadores[index]);
                jogadores.Remove(jogadores[index]);
            }

            sobra = new(jogadores);
            jogadores = null;

            // Mensagens finais
            messageBuilder = new DiscordMessageBuilder();
            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();

            // Time A
            embedBuilder.Title = "Time A";
            embedBuilder.Color = new DiscordColor(255, 50, 50);
            
            for (int i = 0; i < jogadoresPorTime; i++)
            {
                if (i == 0)
                {
                    embedBuilder.Description = $"{timeA[i].Mention}";
                    continue;
                }

                embedBuilder.Description += $", {timeA[i].Mention}";
            }

            messageBuilder.AddEmbed(embedBuilder.Build());

            // Time B
            embedBuilder.Title = "Time B";
            embedBuilder.Color = new DiscordColor(50, 50, 255);

            for (int i = 0; i < jogadoresPorTime; i++)
            {
                if (i == 0)
                {
                    embedBuilder.Description = $"{timeB[i].Mention}";
                    continue;
                }

                embedBuilder.Description += $", {timeB[i].Mention}";
            }

            messageBuilder.AddEmbed(embedBuilder.Build());

            // Time de sobra
            if (jogadoresDeSobra > 0)
            {
                embedBuilder.Title = "De outra";
                embedBuilder.Color = new DiscordColor(110, 110, 110);

                for (int i = 0; i < jogadoresDeSobra; i++)
                {
                    if (i == 0)
                    {
                        embedBuilder.Description = $"{sobra[i].Mention}";
                        continue;
                    }

                    embedBuilder.Description += $", {sobra[i].Mention}";
                }

                messageBuilder.AddEmbed(embedBuilder.Build());
            }

            // Salvar time gerado
            TimesGerados relatorio = new TimesGerados(member, timeA, timeB, sobra);
            timesGerados.Add(relatorio);

            // Botões                        
            DiscordButtonComponent moverTimeA = new(DiscordButtonStyle.Danger, $"{moverTimeButton}0;{relatorio.UUID}", "Mover Time A");
            DiscordButtonComponent moverTimeB = new(DiscordButtonStyle.Primary, $"{moverTimeButton}1;{relatorio.UUID}", "Mover Time B");
            messageBuilder.AddActionRowComponent(moverTimeA, moverTimeB);               

            await messageBuilder.SendAsync(messageChannel);
            await messageChannel.SendMessageAsync(Program.GetTaskDoneMessage());
        }        

        public async Task Personalizada_MoverTimes(string timeGeradoUUID, char Time, ComponentInteractionCreatedEventArgs args)
        {
            DiscordMember donoDaAção = await Program.servidor.GetMemberAsync(args.User.Id);
            TimesGerados time = null;

            // Procurar nos times gerados o time com o UUID passado pelo botão
            foreach (TimesGerados t in timesGerados)
            {
                if (t.UUID == timeGeradoUUID)
                {
                    time = t;
                    break;
                }
            }

            if (time != null)
            {
                if (time.Pedinte == donoDaAção)
                {
                    // Mover Time
                    if (Time == '0')
                    {
                        // Time A
                        var vc = await Program.modulo_GenDeCanal.NovoCanalTemporário(Program.GetTime() + new TimeSpan(0, 40, 0), "🟥 Time A"); // Cria o Canal
                        await moverTime(donoDaAção, time.TimeA, vc.canal); // Move todos os jogadores para o canal
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent($"Movendo Time A para {vc.canal.Mention}").WithReply(args.Message.Id)));                                                
                    }
                    else if (Time == '1')   
                    {
                        // Time B
                        var vc = await Program.modulo_GenDeCanal.NovoCanalTemporário(Program.GetTime() + new TimeSpan(0, 40, 0), "🟦 Time B"); // Cria o canal
                        await moverTime(donoDaAção, time.TimeB, vc.canal); // Move todos os jogadores para o canal
                        await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent($"Movendo Time B para {vc.canal.Mention}").WithReply(args.Message.Id)));                        
                    }
                }
                else
                {
                    // Usuário inválido
                    await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent("Somente quem sorteou o time pode mover os jogadores. Peça para ele tentar!").WithReply(args.Message.Id)));
                }
            }
            else
            {
                // Time não existe mais
                await args.Interaction.CreateResponseAsync(DiscordInteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(new DiscordMessageBuilder().WithContent("Infelizmente esse time não é mais válido. Crie outro e tente novamente.").WithReply(args.Message.Id)));
            }            

        }        

        private async Task moverTime(DiscordMember sorteador, IReadOnlyList<DiscordMember> jogadoresDoTime, DiscordChannel destinoVC)
        {
            foreach (var jogador in jogadoresDoTime)
            {                
                DiscordChannel playerChannel = await jogador.VoiceState.GetChannelAsync();

                if (jogador.VoiceState != null)
                {
                    if (playerChannel.Guild == Program.servidor)
                    {
                        if (playerChannel == canalDeLobby || sorteador.Permissions.HasPermission(DiscordPermission.MoveMembers))
                        {
                            await destinoVC.PlaceMemberAsync(jogador);
                            continue;
                        }
                        else
                        {
                            await destinoVC.SendMessageAsync($"Não foi possível mover o jogador {jogador.Mention}, já que {sorteador.Mention} não tem permissões para mover usuários e {jogador.Mention} não está em {canalDeLobby.Mention}");
                            continue;
                        }
                    }
                }
                await destinoVC.SendMessageAsync($"Não foi possível mover o jogador {jogador.Mention} pois ele não está conectado em uma call que tenho acesso");
            }
        }

        public ValorantMapa Valorant_SortearMapa(bool OnlyOnRotation = true)
        {
            while (true)
            {
                int random = sorteador.Next(0, Valorant_Mapas.Mapas.Count);
                
                if (OnlyOnRotation && Valorant_Mapas.Mapas[random].OnRotation)
                {
                    return Valorant_Mapas.Mapas[random];
                }
                else if (!OnlyOnRotation)
                {
                    return Valorant_Mapas.Mapas[random];
                }                
            }
        }

    }

    public class ValorantMapas
    {
        public List<ValorantMapa> Mapas { get; set; }
    }

    public class ValorantMapa
    {
        public string Name { get; set; }
        public string ImageURL { get; set; }
        public bool OnRotation { get; set; }
    }

    public class ValorantAgente
    {
        public string Name { get; set; }
        public string ImageURL { get; set; }
        public ValorantRole Role { get; set; }  
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ValorantRole
    {
        Duelista,
        Iniciador,
        Controlador,
        Sentinela
    }

    public class TimesGerados
    {
        public string UUID { get; }
        public DiscordMember Pedinte { get; }

        private readonly List<DiscordMember> timeA;
        private readonly List<DiscordMember> timeB;
        private readonly List<DiscordMember> sobra;

        public IReadOnlyList<DiscordMember> TimeA => timeA;
        public IReadOnlyList<DiscordMember> TimeB => timeB;
        public IReadOnlyList<DiscordMember> Sobra => sobra;

        public IReadOnlyList<DiscordMember> Participantes => [.. timeA, .. timeB];        

        public TimesGerados(DiscordMember pedinte, List<DiscordMember> a, List<DiscordMember> b, List<DiscordMember> sobra = null)
        {
            this.Pedinte = pedinte;
            this.timeA = a;
            this.timeB = b;
            this.sobra = sobra;
            UUID = Guid.NewGuid().ToString();   
        }
    }

    public class CircularBuffer<T> : IReadOnlyList<T>
    {
        private readonly List<T> buffer;
        private int head = 0;
        private int count = 0;

        public int Capacity { get; }

        public CircularBuffer(int capacity)
        {
            Capacity = capacity;
            buffer = new List<T>(capacity);
        }

        public void Add(T item)
        {
            if (count < Capacity)
            {
                buffer.Add(item);
                count++;
            }
            else
            {
                buffer[head] = item; // Overwrite oldest element
            }

            head = (head + 1) % Capacity;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= count)
                    throw new IndexOutOfRangeException("Index is out of range.");

                return buffer[(head - count + index + Capacity) % Capacity];
            }
        }

        public int Count => count;

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}