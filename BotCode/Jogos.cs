using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts
{
    public class Jogos
    {

        Random sorteador = new Random();

        public async Task Init()
        {
            sorteador = new Random();
        }        

        public string sortStringList(List<string> list) => list[sorteador.Next(0, list.Count - 1)];

        public async Task Personalizada_SortearTimes_fast(DiscordMember member, DiscordMessage originalMessage, uint maxPorTime = 5)
        {

            DiscordVoiceState voiceState = member.VoiceState; // Serve para ver em qual canal da call está o usuário.

            if (voiceState == null)
            {
                await Program.client.SendMessageAsync(originalMessage.Channel, "Para usar esse comando você deve estar conectado em um canal de voz");
                return;
            }

            if (voiceState.Channel.Users.Count < 3)
            {
                await Program.client.SendMessageAsync(originalMessage.Channel, "Devem ter mais de 2 usuários na call para usar esse comando");
                return;
            }

            DiscordMessageBuilder messageBuilder = new DiscordMessageBuilder();
            messageBuilder.WithReply(originalMessage.Id, true);
            messageBuilder.WithContent("Sorteando times...");
            await messageBuilder.SendAsync(originalMessage.Channel);            

            List<DiscordMember> jogadores = new(voiceState.Channel.Users);            
            List<DiscordMember> timeA = new();
            List<DiscordMember> timeB = new();
            List<DiscordMember> sobra = new();

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

            await messageBuilder.SendAsync(originalMessage.Channel);
            await originalMessage.Channel.SendMessageAsync("Voilà!");
        }

        public async Task Valorant_SortearMapa(DiscordMessage originalMessage, bool OnlyOnRotation = true)
        {            

        }

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
}