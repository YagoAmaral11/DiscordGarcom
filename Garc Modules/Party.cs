using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordGarçom.GarcModules;

public class Party(IPersistance persistance, IConfigPersistance configPersistance, CoreChannelManager channelManager) : BaseModule<PartyConfig, PartyData>(persistance, configPersistance)
{
    CoreChannelManager channelManager = channelManager;
    Random sorter = new();
    // Usado para guardar informações de "possíveis partidas".
    // TODO: Depois remover "pre-partidas" muito antigas.
    Dictionary<string, Partida> preMatches = new(); 
    // Usado para guardar em qual canal o admin estava antes de criar a partida. Usado para depois mover os jogadores de volta.
    // TODO: Depois remover "pre-partidas" muito antigas
    Dictionary<string, ulong> preMatchesAdminVoiceChannel = new(); 

    public override string Name => "Party";
    protected override bool ThrowExceptionOnMissingConfig => true;

    // "Button Codes"    
    private const string createMixMatch = "mix_create";
    private const string createMixMatchAndMove = "mix_create_andMove";
    private const string finishMix = "mix_finish";
    private const string finishMixAndMove = "mix_finish_andMove";

    protected override PartyConfig InitializeConfig() => new();
    protected override PartyData InitializeData() => new();
    
    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        ehb.HandleComponentInteractionCreated(OnInteraction);
        return Task.CompletedTask;
    }

    public override IEnumerable<CommandBuilder> GetDynamicCommands() => []; // TODO: Criar comandos para gerenciar e listar as partidas
    public override List<Type> GetStaticCommands() => [];


    public override async Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {
        bool baseReturn = await base.Initialize(serverContext, serviceProvider);

        sorter = new();

        return baseReturn;
    }

    public override Task Start() => Task.CompletedTask;


    private async Task OnInteraction(DiscordClient client, ComponentInteractionCreatedEventArgs args)
    {
        string buttonId = args.Interaction.Data.CustomId;

        if (buttonId.StartsWith(Name) == false)
            return;

        string buttonCode = buttonId.Substring(Name.Length - 1, buttonId.IndexOf('&') - 1);
        string matchUUID = buttonId.Substring(buttonId.IndexOf('&') - 1);

        switch (buttonCode)
        {
            case createMixMatch:
                Internal_MatchRegister(matchUUID);
                // TODO: Remover botão de criar partida
                break;
            case createMixMatchAndMove:
                Internal_MatchRegister(matchUUID);
                await Internal_MatchMovePlayers(matchUUID);                
                // TODO: Remover botão de criar partida/mover times
                break;
            case finishMix:                
                Internal_MatchEnd(matchUUID);
                // TODO: Remover botão de terminar partida
                break;
            case finishMixAndMove:                
                Internal_MatchEnd(matchUUID);
                // TODO: Mover jogadores de volta para a call original/lobby
                // TODO: Remover botões de terminar partida
                break;
        }

    }


    // Cria uma nova partida em branco, preenchendo apenas os times, o admin da partida, a data e o UUID. Não registra a partida como ativa nem como acabada.
    private static Partida MatchCreate(DiscordMember admin, IEnumerable<DiscordMember> teamA, IEnumerable<DiscordMember> teamB)
    {
        Partida match = new(teamA.Select(t => t.Id), teamB.Select(t => t.Id)); 
        match.UUID = Guid.NewGuid().ToString(); // TODO: Não confiar no GUID cegamente; Depois verificar se é único, e criar novo se não for
        match.Admin = admin.Id;
        match.Date = DateTimeOffset.Now;
        match.Finished = false;
        return match;
    }

    // Cria o embed para mostrar uma partida registrada (ativa ou finalizada)
    private DiscordMessageBuilder MatchRender(Partida match) => throw new NotImplementedException();



    // Registra uma "pré-partida" como uma partida ativa, e remove a "pré-partida"
    private void Internal_MatchRegister(string matchUUID)
    {
        if (!preMatches.ContainsKey(matchUUID))
            return;

        data.PartidasAtivas.Add(matchUUID, preMatches[matchUUID]);
        preMatches.Remove(matchUUID);
    }

    // Dado uma partida ativa, move os jogadores de cada time para uma call temporário para o time
    private async Task Internal_MatchMovePlayers(string matchUUID, TimeSpan? tempTeamsVoiceChannelsLifespan = null)
    {
        // TODO: Depois, melhorar o feedback do bot para o usuário do que está acontecendo (jogadores que não foram possíveis mover, etc.)

        data.PartidasAtivas.TryGetValue(matchUUID, out var match);

        if (match == null)
            return;

        tempTeamsVoiceChannelsLifespan ??= TimeSpan.FromHours(1);
        var lifespan = DateTimeOffset.Now + tempTeamsVoiceChannelsLifespan.Value;
        var canalA = await channelManager.NewGeneralTempChannel(lifespan, "•᲼ 🔵 Time A");
        var canalB = await channelManager.NewGeneralTempChannel(lifespan, "•᲼ 🔴 Time B");

        foreach (var playerID in match.Players)
        {
            var playerVoiceState = await serverContext.BindedDiscordServer.GetMemberVoiceStateAsync(playerID);
            var playerDiscordMember = await serverContext.BindedDiscordServer.GetMemberAsync(playerID);

            if (playerVoiceState.ChannelId == preMatchesAdminVoiceChannel[matchUUID])
            {
                if (match.TimeA.Contains(playerID))
                {
                    await canalA.channel.PlaceMemberAsync(playerDiscordMember);
                }
                else if (match.TimeB.Contains(playerID))
                {
                    await canalB.channel.PlaceMemberAsync(playerDiscordMember);
                }
            }
        }
    }

    // Finaliza uma partida ativa
    private void Internal_MatchEnd(string matchUUID)
    {
        data.PartidasAtivas.TryGetValue(matchUUID, out var match);
        data.PartidasAtivas.Remove(matchUUID);

        if (match == null)
            return;

        data.PartidasAntigas.Add(matchUUID, match);
    }

    // Remove o cache que salva em qual call o admin da partida estava ao criar uma "pré-partida"
    // OBS: Só executar isso depois que a partida for finalizada/usuários já na call anterior.
    private void Internal_PreMatchClear(string matchUUID)
    {
        preMatchesAdminVoiceChannel.Remove(matchUUID);
    }


    // Automaticamente sorteia dois times e gera uma partida (mas nao registra ela), de acordo com um admin de partida e os usuários na call que o admin esteja.
    private async Task<(AutoSortResult result, Partida match, IEnumerable<DiscordMember> leftOut)> AutoVoiceChatSort(DiscordMember admin, uint maxPorTime = 5, string[] excludedPlayers = null)
    {
        DiscordVoiceState voiceState = admin.VoiceState;        

        // Verificações Iniciais
        if (CanUserCreateMatches(admin) == false)
            return (AutoSortResult.CantCreateMoreMatches, null, null);

        if (voiceState == null)
            return (AutoSortResult.AdminNotInVoiceChat, null, null);

        var adminVC = await voiceState.GetChannelAsync();

        if (adminVC.Users.Count < 3)
            return (AutoSortResult.LessThanThreePlayers, null, null);                

        // Sorteio
        List<DiscordMember> jogadores = new(adminVC.Users);
        List<DiscordMember> timeA = new();
        List<DiscordMember> timeB = new();
        List<DiscordMember> sobra = new();

        List<DiscordMember> tmp = new(jogadores);

        // Parte para excluir jogadores do sorteio
        foreach (DiscordMember jogador in tmp)
        {
            if (jogador.Id == serverContext.BotDiscordClient.CurrentUser.Id)
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
            int index = sorter.Next(0, jogadores.Count);
            timeA.Add(jogadores[index]);
            jogadores.Remove(jogadores[index]);
        }

        for (int i = 0; i < jogadoresPorTime; i++)
        {
            int index = sorter.Next(0, jogadores.Count);
            timeB.Add(jogadores[index]);
            jogadores.Remove(jogadores[index]);
        }

        sobra = new(jogadores);
        jogadores = null;

        // Criar a partida
        var match = MatchCreate(admin, timeA, timeB);
        return (AutoSortResult.Sucess,  match, sobra);
    }

    // O modo legado de mostrar os jogadores selecionados no sorteio de dois times, para o mix    
    private async Task<DiscordMessageBuilder> Internal_LegacyRenderMixTeams(Partida match, IEnumerable<DiscordMember> leftOut = null)
    {
        var messageBuilder = new DiscordMessageBuilder();
        DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();

        // Time A
        embedBuilder.Title = "Time A";
        embedBuilder.Color = new DiscordColor(255, 50, 50);

        for (int i = 0; i < match.TimeA.Count; i++)
        {
            DiscordMember member = await serverContext.BindedDiscordServer.GetMemberAsync(match.TimeA[i]);

            if (i == 0)
            {                
                embedBuilder.Description = $"{member.Mention}";
                continue;
            }

            embedBuilder.Description += $", {member.Mention}";
        }

        messageBuilder.AddEmbed(embedBuilder.Build());

        // Time B
        embedBuilder.Title = "Time B";
        embedBuilder.Color = new DiscordColor(50, 50, 255);

        for (int i = 0; i < match.TimeB.Count; i++)
        {
            DiscordMember member = await serverContext.BindedDiscordServer.GetMemberAsync(match.TimeB[i]);

            if (i == 0)
            {
                embedBuilder.Description = $"{member.Mention}";
                continue;
            }

            embedBuilder.Description += $", {member.Mention}";
        }

        messageBuilder.AddEmbed(embedBuilder.Build());

        // Time de sobra
        var sobraQnt = leftOut.Count();
        if (sobraQnt > 0)
        {
            embedBuilder.Title = "De outra";
            embedBuilder.Color = new DiscordColor(110, 110, 110);            

            List<DiscordMember> leftOutList = new(leftOut);

            for (int i = 0; i < sobraQnt; i++)
            {                
                if (i == 0)
                {
                    embedBuilder.Description = $"{leftOutList[i].Mention}";
                    continue;
                }

                embedBuilder.Description += $", {leftOutList[i].Mention}";
            }

            messageBuilder.AddEmbed(embedBuilder.Build());
        }

        return messageBuilder;
    }



    private bool CanUserCreateMatches(DiscordMember user) => data.PartidasAtivas.Where(match => match.Value.Admin == user.Id).Count() < config.MaxConcurrentMatchesPerAdmin;



    // O meio legado de criar um mix
    // Sorteia dois times automaticamente dado a call do pedinte, fazendo o mesmo o admin da partida.
    // Cria botões para "aceitar" e realmente criar a partida e calls para dois times, além de mover os jogadores.
    // Ainda cria um botão para mover todos de volta para um outra call de "lobby", para o pós jogo.
    [Command("fastmix")]
    public async Task FastMix(CommandContext ctx)
    {        
        var result = await AutoVoiceChatSort(ctx.Member);
        await Internal_fastMix(ctx, result);
    }

    [Command("fastmix")]
    // O meio legado de criar um mix, com um número específício de jogadores máximos por time
    public async Task FastMix(CommandContext ctx, uint jogadoresmax)
    {
        var result = await AutoVoiceChatSort(ctx.Member, jogadoresmax);
        await Internal_fastMix(ctx, result);
    }

    [Command("fastmix")]
    // O meio legado de criar um mix, removendo certos jogadores do sorteio de times
    public async Task FastMix(CommandContext ctx, params string[] jogadoresdefora)
    {
        var result = await AutoVoiceChatSort(ctx.Member, excludedPlayers: jogadoresdefora);
        await Internal_fastMix(ctx, result);
    }

    [Command("fastmix")]
    // O meio legado de criar um mix, com um número específício de jogadores máximos por time e removendo certos jogadores do sorteio de times
    public async Task FastMix(CommandContext ctx, uint jogadoresmax, params string[] jogadoresdefora)
    {
        var result = await AutoVoiceChatSort(ctx.Member, jogadoresmax, jogadoresdefora);
        await Internal_fastMix(ctx, result);
    }

    private async Task Internal_fastMix(CommandContext ctx, (AutoSortResult result, Partida match, IEnumerable<DiscordMember> leftOut) result)
    {
        DiscordFollowupMessageBuilder builder = new();
        DiscordMessageBuilder msgBuilder = new();

        switch (result.result)
        {
            case AutoSortResult.AdminNotInVoiceChat:

                if (ctx is SlashCommandContext)
                {
                    builder.WithContent("Você deve estar em um canal de voz para poder usar esse comando!").AsEphemeral();
                    await ctx.FollowupAsync(builder);
                }
                else if (ctx is TextCommandContext textCtx)
                {
                    await msgBuilder.WithReply(textCtx.Message.Id).WithContent("Você deve estar em um canal de voz para poder usar esse comando!").SendAsync(textCtx.Channel);
                }

                return;
            case AutoSortResult.LessThanThreePlayers:

                if (ctx is SlashCommandContext)
                {
                    builder.WithContent("Deve haver mais de 2 pessoas em call para usar esse comando").AsEphemeral();
                    await ctx.FollowupAsync(builder);
                }
                else if (ctx is TextCommandContext textCtx)
                {
                    await msgBuilder.WithReply(textCtx.Message.Id).WithContent("Deve haver mais de 2 pessoas em call para usar esse comando").SendAsync(textCtx.Channel);
                }

                return;
            case AutoSortResult.CantCreateMoreMatches:

                if (ctx is SlashCommandContext)
                {
                    builder.WithContent($"Você já atingiu o limite de partidas concorrentes ({config.MaxConcurrentMatchesPerAdmin}), tente finalizar uma primeiro!").AsEphemeral();
                    await ctx.FollowupAsync(builder);
                }
                else if (ctx is TextCommandContext textCtx)
                {
                    await msgBuilder.WithReply(textCtx.Message.Id).WithContent($"Você já atingiu o limite de partidas concorrentes ({config.MaxConcurrentMatchesPerAdmin}), tente finalizar uma primeiro!").SendAsync(textCtx.Channel);
                }

                return;
            case AutoSortResult.Sucess:
                break;
            default:
                if (ctx is SlashCommandContext)
                {
                    builder.WithContent("Um erro ocorreu. Tente novamente.").AsEphemeral();
                    await ctx.FollowupAsync(builder);
                }
                else if (ctx is TextCommandContext textCtx)
                {
                    await msgBuilder.WithReply(textCtx.Message.Id).WithContent("Um erro ocorreu. Tente novamente.").SendAsync(textCtx.Channel);
                }
                return;
        }

        preMatches.Add(result.match.UUID, result.match);
        preMatchesAdminVoiceChannel.Add(result.match.UUID, (ulong) ctx.Member.VoiceState.ChannelId);

        DiscordMessageBuilder finalResponse = await Internal_LegacyRenderMixTeams(result.match, result.leftOut);
        DiscordButtonComponent createMatchBtn = new(DiscordButtonStyle.Success, $"{Name}.{createMixMatch}&{result.match.UUID}", "Confirmar partida");
        DiscordButtonComponent createMatchAndMoveBtn = new(DiscordButtonStyle.Success, $"{Name}.{createMixMatchAndMove}&{result.match.UUID}", "Confirmar partida & Criar calls");
        DiscordButtonComponent endMatchBtn = new(DiscordButtonStyle.Secondary, $"{Name}.{finishMix}&{result.match.UUID}", "Finalizar partida");
        DiscordButtonComponent endMatchAndMoveBtn = new(DiscordButtonStyle.Secondary, $"{Name}.{finishMixAndMove}&{result.match.UUID}", "Finalizar partida & Mover jogadores");

        finalResponse.AddActionRowComponent(createMatchBtn, createMatchAndMoveBtn, endMatchBtn, endMatchAndMoveBtn);

        await ctx.RespondAsync(finalResponse);
    }



    public enum AutoSortResult
    {
        AdminNotInVoiceChat,
        LessThanThreePlayers,
        CantCreateMoreMatches,
        Sucess
    }

    public class Partida
    {
        public string UUID { get; set; }
        public ulong Admin { get; set; } // O admin da partida; Geralmente o pedinte
        public DateTimeOffset Date { get; set; }
        public bool Finished { get; set; } = false;
        public string Game { get; set; }

        private readonly List<ulong> timeA = new();
        public IReadOnlyList<ulong> TimeA => timeA;
        public string TimeA_Name {  get; set; }
        public float TimeA_Score {  get; set; }

        private readonly List<ulong> timeB = new();
        public IReadOnlyList<ulong> TimeB => timeB;
        public string TimeB_Name { get; set; }
        public float TimeB_Score { get; set; }

        public IReadOnlyList<ulong> Players => [.. timeA, .. timeB];        

        public Partida(IEnumerable<ulong> timeA, IEnumerable<ulong> timeB)
        {
            this.timeA = new(timeA);
            this.timeB = new(timeB);
        }
    }

}

public class PartyConfig
{
    public int MaxConcurrentMatchesPerAdmin { get; set; } = 5;
}

public class PartyData
{
    public Dictionary<string, Party.Partida> PartidasAtivas { get; set; }
    public Dictionary<string, Party.Partida> PartidasAntigas { get; set; } // TODO: Alterar a forma de salvar partidas antigas para algo mais eficiente.
}