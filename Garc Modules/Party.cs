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
using System.Text;
using System.Threading.Tasks;

namespace DiscordGarçom.GarcModules;

public class Party(IPersistance persistance, IConfigPersistance configPersistance, CoreChannelManager channelManager) : BaseModule<PartyConfig, PartyData>(persistance, configPersistance)
{
    CoreChannelManager channelManager = channelManager;
    Random sorter = new();
    // Usado para guardar informações de "possíveis partidas".
    // TODO: Depois remover "pre-partidas" muito antigas. No momento, nem todas são registradas como partidas ativas, então só ficam guardadas aqui.
    Dictionary<string, Partida> preMatches = new(); 
    // Usado para guardar em qual canal o admin estava antes de criar a partida. Usado para depois mover os jogadores de volta.
    // TODO: Depois remover "pre-partidas" muito antigas. No momento, só as partidas que o admin move os jogadores para o lobby são removidas daqui.
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

    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {        
        CommandBuilder party = new CommandBuilder().WithName("party");        
        
        var fastMix = new CommandBuilder().WithName("fastmix");
        var fastMix_sort = CommandBuilder.From(FastMix).WithParent(fastMix).WithDescription("Sorteia dois times de acordo com a call do pedinte, que pode ser transformado em uma partida");
        var fastMix_max = CommandBuilder.From(FastMix_max).WithParent(fastMix).WithDescription("Fastmix, com os times tendo um limite de jogadores");
        var fastMix_out = CommandBuilder.From(FastMix_out).WithParent(fastMix).WithDescription("Fastmix, com jogadores excluídos do sorteio");
        var fastMix_maxout = CommandBuilder.From(FastMix_maxout).WithParent(fastMix).WithDescription("Fastmix, com limite de jogadores e jogadores excluídos do sorteio");
        fastMix.WithSubcommands([fastMix_sort, fastMix_max, fastMix_out, fastMix_maxout]);

        var partida = new CommandBuilder().WithName("partida");
        var partida_atual = CommandBuilder.From(CurrentMatch).WithParent(partida).WithDescription("Mostra a partida atual do pedinte, seja como admin ou jogador");
        var partida_mostrar = CommandBuilder.From(CurrentMatch_showId).WithParent(partida).WithDescription("Mostra a partida do ID passado");
        var partida_finalizar = CommandBuilder.From(EndMatch).WithParent(partida).WithDescription("Finaliza a partida do ID passado ou a partida ativa do pedinte");
        partida.WithSubcommands([partida_atual, partida_mostrar, partida_finalizar]);

        party.WithSubcommands([fastMix, partida]);

        return [party];
    }

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
        await args.Interaction.DeferAsync();

        string buttonId = args.Interaction.Data.CustomId;

        if (buttonId.StartsWith(Name) == false)
            return;

        string buttonCode = buttonId.Substring(Name.Length + 1, buttonId.IndexOf('&') - Name.Length - 1);        
        string matchUUID = buttonId.Substring(buttonId.IndexOf('&') + 1);

        if (!IsMatchAdmin(matchUUID, await serverContext.BindedDiscordServer.GetMemberAsync(args.User.Id)))
        {
            var responseBuilder = new DiscordFollowupMessageBuilder();
            responseBuilder.WithContent("Você não tem permissão para executar esse comando nessa partida!").AsEphemeral();
            await args.Interaction.CreateFollowupMessageAsync(responseBuilder);
            return;
        }

        switch (buttonCode)
        {
            case createMixMatch:
                Internal_MatchRegister(matchUUID);                
                break;
            case createMixMatchAndMove:
                Internal_MatchRegister(matchUUID);
                await Internal_MatchMovePlayers(matchUUID);                                
                break;
            case finishMix:                
                Internal_MatchEnd(matchUUID);                
                break;
            case finishMixAndMove:                
                Internal_MatchEnd(matchUUID);                                
                await Internal_MatchMovePlayersToLobby(matchUUID);
                Internal_PreMatchClear(matchUUID);
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

        match.TimeA_Score = -1;
        match.TimeB_Score = -1;        

        return match;
    }

    // Cria o embed para mostrar uma partida registrada (ativa ou finalizada)
    private DiscordMessageBuilder MatchRender(Partida match)
    {
        DiscordMessageBuilder msg = new();

        string teamAName = string.IsNullOrEmpty(match.TimeA_Name) ? "A" : match.TimeA_Name;
        string teamBName = string.IsNullOrEmpty(match.TimeB_Name) ? "B" : match.TimeB_Name;

        DiscordEmbedBuilder embed = new();
        embed.WithTitle("Partida Personalizada");

        StringBuilder description = new();
        description.AppendLine(match.Date.ToString());
        description.AppendLine($"UUID: {match.UUID}");
        
        if (!string.IsNullOrWhiteSpace(match.Game))
            description.AppendLine("Jogo: " + match.Game);

        if (match.Finished)
        {
            description.AppendLine($"*Finalizada*");            
        }
        else
        {
            description.AppendLine($"*Em Andamento*");
        }

        embed.WithDescription(description.ToString());

        // A Players Field
        string teamAPlayers = "";

        int count = 0;
        foreach (ulong playerID in match.TimeA)
        {
            DiscordMember member = serverContext.BindedDiscordServer.GetMemberAsync(playerID).Result;

            if (count == 0)
            {
                teamAPlayers += $"{member.Username} ({member.Mention})";                
            }
            else
                teamAPlayers += $", {member.Username} ({member.Mention})";

            count++;
        }

        // B Players Field
        string teamBPlayers = "";
        count = 0;
        foreach (ulong playerID in match.TimeB)
        {
            DiscordMember member = serverContext.BindedDiscordServer.GetMemberAsync(playerID).Result;

            if (count == 0)
            {
                teamBPlayers += $"{member.Username} ({member.Mention})";
            }
            else
                teamBPlayers += $", {member.Username} ({member.Mention})";

            count++;
        }

        // Result
        string timeAScore = "";
        string timeBScore = "";

        if (match.TimeA_Score != -1 && match.TimeB_Score != -1)
        {
            timeAScore = match.TimeA_Score.ToString();
            timeBScore = match.TimeB_Score.ToString();
        }

        // Fields
        embed.AddField(teamAName + timeAScore, teamAPlayers, true);
        embed.AddField(teamBName + timeBScore, teamBPlayers, true);        

        msg.AddEmbed(embed.Build());

        return msg;
    }



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

    // Dado uma partida, ativa ou acabada, move os jogadores de cada time para a call original do admin da partida, ou uma call de lobby de fallback.
    // OBS: Só funciona enquanto a partida ainda estiver no preMatchesAdminVoiceChannel
    private async Task<bool> Internal_MatchMovePlayersToLobby(string matchUUID)
    {
        // TODO: Depois, melhorar o feedback do bot para o usuário do que está acontecendo (jogadores que não foram possíveis mover, etc.)

        if (preMatchesAdminVoiceChannel.TryGetValue(matchUUID, out var adminVCId))
            return false;

        DiscordChannel destination;

        try
        {
            destination = await serverContext.BindedDiscordServer.GetChannelAsync(adminVCId);
        }
        catch
        { 
            destination = await serverContext.BindedDiscordServer.GetChannelAsync(config.MixDefaultLobbyChannelId);
        }

        Partida match;

        if (!data.PartidasAtivas.TryGetValue(matchUUID, out match))
        {
            if (!data.PartidasAntigas.TryGetValue(matchUUID, out match))
                return false;
        }            

        foreach (var playerID in match.Players)
        {
            var playerVoiceState = await serverContext.BindedDiscordServer.GetMemberVoiceStateAsync(playerID);
            var playerDiscordMember = await serverContext.BindedDiscordServer.GetMemberAsync(playerID);

            if (playerVoiceState.ChannelId != null)
                await destination.PlaceMemberAsync(playerDiscordMember);            
        }

        return true;
    }

    // Finaliza uma partida ativa
    private void Internal_MatchEnd(string matchUUID)
    {
        data.PartidasAtivas.TryGetValue(matchUUID, out var match);
        data.PartidasAtivas.Remove(matchUUID);

        if (match == null)
            return;

        match.Finished = true;
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

        var adminVC = await voiceState.GetChannelAsync(true);

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
    private bool DoMatchExist(string matchUUID) => data.PartidasAtivas.ContainsKey(matchUUID) || data.PartidasAntigas.ContainsKey(matchUUID);
    // OBS: Puxa partidas que ainda não foram criadas por padrão
    private Partida GetMatch(string matchUUID, bool allowPreMatches = true)
    {
        if (data.PartidasAtivas.TryGetValue(matchUUID, out var match))
            return match;
        if (data.PartidasAntigas.TryGetValue(matchUUID, out match))
            return match;
        if (preMatches.TryGetValue(matchUUID, out match))
            return match;
        return null;
    }
    private bool IsMatchAdmin(string matchUUID, DiscordMember user)
    {
        var match = GetMatch(matchUUID);
        if (match == null)
            return false;

        return match.Admin == user.Id;
    }


    // O meio legado de criar um mix
    // Sorteia dois times automaticamente dado a call do pedinte, fazendo o mesmo o admin da partida.
    // Cria botões para "aceitar" e realmente criar a partida e calls para dois times, além de mover os jogadores.
    // Ainda cria um botão para mover todos de volta para um outra call de "lobby", para o pós jogo.
    [Command("sortear")]
    public async Task FastMix(CommandContext ctx)
    {        
        await ctx.DeferResponseAsync();
        var result = await AutoVoiceChatSort(ctx.Member);
        await Internal_fastMix(ctx, result);
    }

    [Command("max")]
    // O meio legado de criar um mix, com um número específício de jogadores máximos por time
    public async Task FastMix_max(CommandContext ctx, uint jogadoresmax)
    {
        await ctx.DeferResponseAsync();
        var result = await AutoVoiceChatSort(ctx.Member, jogadoresmax);
        await Internal_fastMix(ctx, result);
    }

    [Command("out")]
    // O meio legado de criar um mix, removendo certos jogadores do sorteio de times
    public async Task FastMix_out(CommandContext ctx, params string[] jogadoresdefora)
    {
        await ctx.DeferResponseAsync();
        var result = await AutoVoiceChatSort(ctx.Member, excludedPlayers: jogadoresdefora);
        await Internal_fastMix(ctx, result);
    }

    [Command("maxout")]
    // O meio legado de criar um mix, com um número específício de jogadores máximos por time e removendo certos jogadores do sorteio de times
    public async Task FastMix_maxout(CommandContext ctx, uint jogadoresmax, params string[] jogadoresdefora)
    {
        await ctx.DeferResponseAsync();
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

        DiscordActionRowComponent actionRow = new([createMatchBtn, createMatchAndMoveBtn, endMatchBtn, endMatchAndMoveBtn]);                        

        finalResponse.AddActionRowComponent(createMatchBtn, createMatchAndMoveBtn, endMatchBtn, endMatchAndMoveBtn);

        await ctx.RespondAsync(finalResponse);
    }


    [Command("atual")]
    // Mostra a partida atual, seja de admin ou de jogador, do pedinte. Se estiver em mais de uma partida, mostra a lista de partidas.
    public async Task CurrentMatch(CommandContext ctx)
    {
        await ctx.DeferResponseAsync();        

        var partidasAtivas = data.PartidasAtivas.Where(match => match.Value.Admin == ctx.Member.Id || match.Value.Players.Contains(ctx.Member.Id));
        var partidasAtivasCount = partidasAtivas.Count();

        if (partidasAtivasCount == 1)
        {
            var match = partidasAtivas.First().Value;
            var msg = MatchRender(match);
            await ctx.RespondAsync(msg);
        }
        else if (partidasAtivasCount > 1)
        {
            StringBuilder sb = new();
            sb.AppendLine($"Você está em {partidasAtivasCount} partidas ativas:");

            foreach (var partida in partidasAtivas)
            {
                sb.AppendLine($"- {partida.Key}");
            }

            sb.AppendLine("Use /party partida (id) para mostrar uma partida em específico");
            await ctx.RespondAsync(sb.ToString());
        }
        else if (partidasAtivasCount == 0)
        {
            if (ctx is SlashCommandContext slashCtx)
            {
                var responseBuilder = new DiscordFollowupMessageBuilder();
                responseBuilder.WithContent("Você não faz parte de nenhuma partida, seja como jogador ou admin").AsEphemeral();
                await slashCtx.FollowupAsync(responseBuilder);
            }
            else
            {
                await ctx.RespondAsync("Você não faz parte de nenhuma partida, seja como jogador ou admin");
            }
        }
    }

    [Command("mostrar")]
    // Mostra a partida do UUID passado
    public async Task CurrentMatch_showId(CommandContext ctx, string id)
    {
        await ctx.DeferResponseAsync();

        string uuid = id.ToLower();
        if (data.PartidasAtivas.TryGetValue(uuid, out Partida partidaAtiva))
        {
            var msg = MatchRender(partidaAtiva);
            await ctx.RespondAsync(msg);
        }
        else if (data.PartidasAntigas.TryGetValue(uuid, out Partida partidaAntiga))
        {
            var msg = MatchRender(partidaAntiga);
            await ctx.RespondAsync(msg);
        }
        else
        {
            if (ctx is SlashCommandContext)
            {
                var responseBuilder = new DiscordFollowupMessageBuilder();
                responseBuilder.WithContent("Essa partida não existe").AsEphemeral();
                await ctx.FollowupAsync(responseBuilder);
            }
            else
            {
                await ctx.RespondAsync("Essa partida não existe");
            }
        }
    }

    [Command("finalizar")]
    // Finaliza a partida do UUID passado, ou a partida ativa do pedinte, se ele for admin da partida.
    public async Task EndMatch(CommandContext ctx, string id = null)
    {
        await ctx.DeferResponseAsync();

        var partidasAtivas = data.PartidasAtivas.Where(match => match.Value.Admin == ctx.Member.Id || match.Value.Players.Contains(ctx.Member.Id));
        var partidasAtivasCount = partidasAtivas.Count();

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!DoMatchExist(id))
            {
                if (ctx is SlashCommandContext)
                {
                    var responseBuilder = new DiscordFollowupMessageBuilder();
                    responseBuilder.WithContent("Essa partida não existe! Verifique o ID passado").AsEphemeral();
                    await ctx.FollowupAsync(responseBuilder);
                }
                else
                {
                    await ctx.RespondAsync("Essa partida não existe! Verifique o ID passado");
                }

                return;
            }

            if (!IsMatchAdmin(id, ctx.Member))
            {
                if (ctx is SlashCommandContext)
                {
                    var responseBuilder = new DiscordFollowupMessageBuilder();
                    responseBuilder.WithContent("Você não tem permissão para executar esse comando nessa partida!").AsEphemeral();
                    await ctx.FollowupAsync(responseBuilder);
                }
                else
                {
                    await ctx.RespondAsync("Você não tem permissão para executar esse comando nessa partida!");
                }
                return;
            }

            Internal_MatchEnd(id);
            await ctx.RespondAsync("Partida finalizada!");
            return;
        }

        if (partidasAtivasCount == 1)
        {
            var match = partidasAtivas.First().Value;

            if (!IsMatchAdmin(match.UUID, ctx.Member))
            {
                if (ctx is SlashCommandContext)
                {
                    var responseBuilder = new DiscordFollowupMessageBuilder();
                    responseBuilder.WithContent("Você não tem permissão para executar esse comando nessa partida!").AsEphemeral();
                    await ctx.FollowupAsync(responseBuilder);
                }
                else
                {
                    await ctx.RespondAsync("Você não tem permissão para executar esse comando nessa partida!");
                }
                return;
            }

            Internal_MatchEnd(id);
            await ctx.RespondAsync("Partida finalizada!");
        }
        else if (partidasAtivasCount > 1)
        {
            StringBuilder sb = new();
            sb.AppendLine($"Você está em {partidasAtivasCount} partidas ativas:");

            foreach (var partida in partidasAtivas)
            {
                sb.AppendLine($"- {partida.Key}");
            }

            sb.AppendLine("Use /party terminarpartida (id) para mostrar uma partida em específico");
            await ctx.RespondAsync(sb.ToString());
        }
        else if (partidasAtivasCount == 0)
        {
            if (ctx is SlashCommandContext)
            {
                var responseBuilder = new DiscordFollowupMessageBuilder();
                responseBuilder.WithContent("Você não faz parte de nenhuma partida, seja como jogador ou admin").AsEphemeral();
                await ctx.FollowupAsync(responseBuilder);
            }
            else
            {
                await ctx.RespondAsync("Você não faz parte de nenhuma partida, seja como jogador ou admin");
            }
        }

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

        public List<ulong> timeA { get; set; } = new();
        public IReadOnlyList<ulong> TimeA => timeA;
        public string TimeA_Name {  get; set; }
        public float TimeA_Score {  get; set; }

        public List<ulong> timeB { get; set; } = new();
        public IReadOnlyList<ulong> TimeB => timeB;
        public string TimeB_Name { get; set; }
        public float TimeB_Score { get; set; }

        public IReadOnlyList<ulong> Players => [.. timeA, .. timeB];        

        public Partida()
        {
        }

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
    public ulong MixDefaultLobbyChannelId { get; set; } = 0; // O canal de voz de fallback para onde os jogadores serão movidos no fim do partida, no botão de Mix FinishMixAndMove
}

public class PartyData
{
    public Dictionary<string, Party.Partida> PartidasAtivas { get; set; } = new();
    public Dictionary<string, Party.Partida> PartidasAntigas { get; set; } = new(); // TODO: Alterar a forma de salvar partidas antigas para algo mais eficiente.
}