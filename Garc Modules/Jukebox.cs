using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.Extensions;
using Lavalink4NET.InactivityTracking.Extensions;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.Integrations.Lavasrc;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Server;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordGarçom.GarcModules;

public class Jukebox(IPersistance persistance, IConfigPersistance configPersistance) : BaseModule<JukeboxConfig, JukeboxData>(persistance, configPersistance)
{
    public override string Name => "Jukebox";
    protected override bool ThrowExceptionOnMissingConfig => true;

    protected override JukeboxConfig InitializeConfig() => new();
    protected override JukeboxData InitializeData() => new();

    IAudioService audioService;
    List<TrackSearchMode> searchModes = [];    
    

    private static readonly Dictionary<string, TrackSearchMode> stringToTrackSearchModeDict =
        new(StringComparer.OrdinalIgnoreCase) // Ignora maiúsculas e minúsculas nativamente
        {
            { "youtube", TrackSearchMode.YouTube },
            { "youtubemusic", TrackSearchMode.YouTubeMusic },
            { "soundcloud", TrackSearchMode.SoundCloud },
            { "spotify", TrackSearchMode.Spotify },
            { "deezer", TrackSearchMode.Deezer },
            { "applemusic", TrackSearchMode.AppleMusic },
            { "bandcamp", TrackSearchMode.Bandcamp },
            { "yandexmusic", TrackSearchMode.YandexMusic }            
            // Se o seu Lavalink tiver outros plugins instalados (AppleMusic, Yandex, etc), adicione aqui
        };


    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;

    public override List<Type> GetStaticCommands() => [];
    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        CommandBuilder jukebox = new();
        jukebox.WithName("Jukebox");

        CommandBuilder play = CommandBuilder.From(Play).WithParent(jukebox);
        var playNow = CommandBuilder.From(PlayNow).WithParent(jukebox);
        var playNext = CommandBuilder.From(PlayNext).WithParent(jukebox);
        var join = CommandBuilder.From(Join).WithParent(jukebox);
        var stop = CommandBuilder.From(Stop).WithParent(jukebox);
        var skip = CommandBuilder.From(Skip).WithParent(jukebox);
        var pause = CommandBuilder.From(Pause).WithParent(jukebox);
        var seek = CommandBuilder.From(Seek).WithParent(jukebox);
        var jump10 = CommandBuilder.From(Jump10sec).WithParent(jukebox);
        var jumpless10 = CommandBuilder.From(JumpLess10sec).WithParent(jukebox);
        var restart = CommandBuilder.From(Restart).WithParent(jukebox);
        var queue = CommandBuilder.From(Queue).WithParent(jukebox);
        var queueNext = CommandBuilder.From(QueueNext).WithParent(jukebox);
        var queueRemove = CommandBuilder.From(QueueRemove).WithParent(jukebox);
        var queueClear = CommandBuilder.From(QueueClear).WithParent(jukebox);
        var queueSkipTo = CommandBuilder.From(SkipTo).WithParent(jukebox);
        var shuffle = CommandBuilder.From(Shuffle).WithParent(jukebox);
        var recentQueue = CommandBuilder.From(RecentQueue).WithParent(jukebox);

        jukebox.WithSubcommands([play, playNow, playNext, join, stop, skip, pause, seek, jump10, jumpless10
            , restart, queue, queueNext, queueRemove, queueClear, queueSkipTo, shuffle]);

        return [jukebox];
    }


    public override async Task ConfigureServices(IServiceCollection services)
    {
        // Carrega as configs aqui pois precisa delas para configurar o serviço do lavalink
        if (await configPersistance.ConfigExists(this))
        {
            await LoadConfig();
        }
        else
        {
            // Write default config            
            await configPersistance.WriteConfig(this, InitializeConfig());

            if (ThrowExceptionOnMissingConfig)
                throw new Exception(((IModule) this).LogName + " config not found. Please modify the standard one.");
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(((IModule) this).LogName + " config not found. Initializing with the default one.");
                Console.ResetColor();
            }
        }

        services.AddLavalink();
        services.AddInactivityTracking();

        services.ConfigureLavalink(c =>
        {
            string baseAdressPrefix = config.LavalinkSecure ? "https" : "http";
            c.BaseAddress = new Uri($"{baseAdressPrefix}://{config.LavalinkIP}:{config.LavalinkPort}");
            c.Passphrase = config.LavalinkKeyword;            
        });

        // /*
        services.Configure<IdleInactivityTrackerOptions>(c =>
        {
            c.Timeout = TimeSpan.FromMinutes(config.JukeboxDisconnectOnInactiveMinutes);
        });

        services.Configure<UsersInactivityTrackerOptions>(c =>
        {
            c.Timeout = TimeSpan.FromMinutes(config.JukeboxDisconnectOnAlone);
        });        
        // /*

        return;
    }

    public override async Task PreStart_0()
    {
        audioService = services.GetService<IAudioService>();
        ArgumentNullException.ThrowIfNull(audioService);

        await audioService.StartAsync();
        
        ConfigureLavalinkSearchSources();

    }

    public override Task Start() => Task.CompletedTask;


    private void ConfigureLavalinkSearchSources()
    {
        foreach (var str in config.LavalinkSearchSources)
        {
            string cleanStr = str.Trim().Replace(" ", "").Replace("-", "");

            if (stringToTrackSearchModeDict.TryGetValue(cleanStr, out var mode))
            {
                if (!searchModes.Contains(mode))
                    searchModes.Add(mode);
            }
        }

        if (searchModes.Count == 0)
        {
            throw new ArgumentException("Could not parse an valid Lavalink Search Mode. Change config and try to add \"Youtube\" or \"Spotify\".");
        }
    }

    private async Task<PlayerResult<JukeboxPlayer>> Internal_GetLavaPlayer(ulong VoiceChannelID, DiscordChannel TextChannel, bool joinVoiceChannel = true)
    {
        PlayerChannelBehavior channelBehavior = joinVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None;
        PlayerRetrieveOptions playerRetrieveOptions = new(ChannelBehavior: channelBehavior);

        JukeboxPlayerConfig jukeboxPlayerOptions = new()
        {
            Config = config,
            BindedModule = this,
            BindedTextChannel = TextChannel,
            SelfDeaf = true,
            SelfMute = false,
        };        

        var serverID = serverContext.BindedDiscordServer.Id;
        var options = Options.Create(jukeboxPlayerOptions);                        
        var result = await audioService.Players.RetrieveAsync<JukeboxPlayer, JukeboxPlayerConfig>(serverID, VoiceChannelID, JukeboxPlayer.CreatePlayerAsync, options, playerRetrieveOptions);        
        return result;
    }

    // Serve para pegar o player do lavalink; Returna true se o player estiver no mesmo canal
    private async Task<(bool sucess, JukeboxPlayer player)> GetLavaPlayer(ulong channelID, DiscordChannel TextChannel)
    {
        var result = await Internal_GetLavaPlayer(channelID, TextChannel, true);

        if (!result.IsSuccess)        
            return (false, null);

        if (result.Player.VoiceState.VoiceChannelId == null)
            return (false, null);

        if (result.Player.VoiceState.VoiceChannelId != channelID)
            return (false, result.Player);

        return (true, result.Player);
    }

    // Verifica se o usuário mandante do comando está em um canal ou se o comando foi enviado no canal de música da whitelist
    // retorna True se verdadeiro, retorna falso e responde com followup nos casos de erro
    private async Task<bool> VerifyMemberChannel(CommandContext ctx)
    {
        ulong channelCommandSentID = ctx.Channel.Id;        

        if (config.WhitelistEnable && config.WhitelistChannel != channelCommandSentID)
        {
            var channel = await serverContext.BindedDiscordServer.GetChannelAsync(config.WhitelistChannel);
            await CommandErrorResponse(ctx, "A Jukebox só responde à comandos enviados no canal " + channel.Mention);
            return false;
        }

        if (await CommandVerifyMemberVoiceState(ctx) == false)
            return false;        

        return true;
    }

    // Faz as verificações de segurança iniciais da Jukebox (se o usuário está em um canal/canal da jukebox) e retorna o player se 
    // tudo der certo. Responde com feedback em caso de erros.
    private async Task<JukeboxPlayer> JukeboxInitialChecks(CommandContext ctx)
    {
        if (await VerifyMemberChannel(ctx) == false)
            return null;

        ulong memberVC = ctx.Member.VoiceState.ChannelId.Value;

        var (sucess, player) = await GetLavaPlayer(memberVC, ctx.Channel);

        if (!sucess)
        {
            if (player != null)
            {
                await CommandErrorResponse(ctx, "Você deve estar no mesmo canal da Jukebox para usar esse comando");
            }
            else
            {
                await CommandErrorResponse(ctx, "Ocorreu um erro na Jukebox");
            }

            return null;
        }
        else
        {            
            return player;
        }
    }

    // Buscar músicas do lavalink, retornado uma música só ou uma playlist     
    private async Task<(List<LavalinkTrack>, bool sucess, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult)> JukeboxSearchTracks(string query, bool returnSearch = false)
    {
        TrackLoadResult trackLoadResult = new();

        if (Uri.TryCreate(query, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            // Buscar Diretamente
            trackLoadResult = await audioService.Tracks.LoadTracksAsync(query, new TrackLoadOptions());
        }
        else
        {
            // Buscar com fallback
            bool found = false;

            foreach (var searchMode in searchModes)
            {
                var options = new TrackLoadOptions
                {
                    SearchMode = searchMode,                    
                };

                var result = await audioService.Tracks.LoadTracksAsync(query, options);

                if (result.IsSuccess)
                {
                    trackLoadResult = result;              
                    found = true;
                    break;
                }
            }

            // Não conseguiu encontrar nada em nenhum modo de busca
            if (!found)
            {
                return ([], false, false, returnSearch, TrackLoadResult.CreateEmpty());
            }            
        }                

        if (trackLoadResult.Playlist is not null)
        {
            return (new List<LavalinkTrack>(trackLoadResult.Tracks), true, true, false, trackLoadResult);
        }
        else if (trackLoadResult.Tracks.Length > 1)
        {
            if (returnSearch)
            {
                return (new List<LavalinkTrack>(trackLoadResult.Tracks), true, false, true, trackLoadResult);
            }
            else
            {
                return ([trackLoadResult.Tracks.FirstOrDefault()], true, false, false, trackLoadResult);
            }
        }
        else
        {
            return ([trackLoadResult.Track], true, false, false, trackLoadResult);
        }
    }

    public async static Task<DiscordSectionComponent> JukeboxTrackSection(LavalinkTrack track, string header, LavalinkPlayer player = null)
    {
        LavalinkServerInformation serverInfo = null;

        if (player != null)
            serverInfo = await player.ApiClient.RetrieveServerInformationAsync();

        string trackName = track.Title;
        string trackAuthor = track.Author;
        string? trackAlbum = null;        
        string trackSource = track.SourceName;
        string trackTimespan = PrintTimeSpan(track.Duration);

        if (serverInfo != null)
        {
            var plugins = serverInfo.Plugins;

            if (plugins.Any(p => p.Name.Equals("lavasrc-plugin", StringComparison.OrdinalIgnoreCase))) 
            {
                var extendedInfo = new ExtendedLavalinkTrack(track);                

                if (extendedInfo.Album.HasValue)
                    trackAlbum = extendedInfo.Album.Value.Name;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("## **" + header + "**");
        sb.AppendLine(trackName + " ***(" + trackTimespan + ")***");
        sb.AppendLine(trackAuthor);
        if (trackAlbum != null)
            sb.AppendLine(trackAlbum);
        sb.AppendLine("De: " + trackSource);

        DiscordSectionComponent section = new(sb.ToString(), new DiscordThumbnailComponent(track.ArtworkUri.ToString()));        
        return section;
    }

    public async static Task<DiscordSectionComponent> JukeboxPlaylistQueuedSection(PlaylistInformation playlist, string header, JukeboxConfig config, LavalinkPlayer player = null)
    {
        LavalinkServerInformation serverInfo = null;

        if (player != null)
            serverInfo = await player.ApiClient.RetrieveServerInformationAsync();

        string? artwork = null;
        string? playlistAuthor = null;
        int? totalTracks = null;
        string playlistName = playlist.Name;

        if (serverInfo != null)
        {
            var plugins = serverInfo.Plugins;

            if (plugins.Any(p => p.Name.Equals("lavasrc-plugin", StringComparison.OrdinalIgnoreCase)))
            {
                var extendedInfo = new ExtendedPlaylistInformation(playlist);
                artwork = extendedInfo.ArtworkUri.ToString();
                playlistAuthor = extendedInfo.Author;
                totalTracks = extendedInfo.TotalTracks;                
            }
        }

        artwork ??= config.DefaultFallbackPlaylistImageUrl;


        var sb = new StringBuilder();
        sb.AppendLine("## **" + header + "**");

        if (totalTracks != null)
            sb.Append(totalTracks + " de ");
        sb.Append(playlistName);

        if (totalTracks != null)
            sb.Append(" foram adicionadas");
        else
            sb.Append(" foi adicionado");

        sb.Append(" à fila");
        sb.AppendLine();

        sb.AppendLine(playlistAuthor);        

        DiscordSectionComponent section = new(sb.ToString(), new DiscordThumbnailComponent(artwork));
        return section;
    }

    public static bool JukeboxHaveTrack(LavalinkPlayer player) => player.State == PlayerState.Playing || player.State == PlayerState.Paused;
    public static bool JukeboxIsPaused(LavalinkPlayer player) => player.State == PlayerState.Paused;
    public static LavalinkTrack JukeboxCurrentTrack(LavalinkPlayer player) => player.CurrentTrack;
    public static string PrintTimeSpan(TimeSpan timeSpan)
    {                
        if (timeSpan.Days > 0)
        {
            return timeSpan.ToString(@"dd\dhh\:mm\:ss");
        }
        else if (timeSpan.Hours > 0)
        {
            return timeSpan.ToString(@"hh\:mm\:ss");
        }
        else
        {
            return timeSpan.ToString(@"mm\:ss");
        }
    }

    private static async Task<bool> ChangePlayerTrackTime(JukeboxPlayer player, TimeSpan tempo)
    {
        if (player.CurrentTrack == null)
            return false;

        if (tempo > player.CurrentTrack.Duration)
        {
            await player.SkipTrack();
        }
        else
            await player.SeekAsync(tempo);

        return true;
    }

    public async Task CallTrackException(ITrackQueueItem track, TrackException exception)
    {
        try
        {
            await ((IModule) this).DumpException(new Exception($"Track Exception in {track.Track.Title} ({track.Identifier}): {exception.Message}, {exception.Cause}"), persistance);
        }
        catch
        {
            await ((IModule) this).DumpException(new Exception("Error trying to create Track Exception"), persistance);
        }
    }

    public async Task CallTrackStuck(ITrackQueueItem track, TimeSpan threshold)
    {
        try
        {
            await ((IModule) this).DumpException(new Exception($"Track Stuck in {track.Track.Title} ({track.Identifier}) at: {PrintTimeSpan(threshold)}"), persistance);
        }
        catch
        {
            await ((IModule) this).DumpException(new Exception("Error trying to create Track Stuck Exception"), persistance);
        }
    }


    // COMANDOS DA JUKEBOX
    // ADIÇÃO DE MÚSICAS

    [Command("Play")]
    [Description("Toca ou adiciona na fila uma música")]
    public async Task Play(CommandContext ctx, [Description("Nome, link, etc. da música")] string query)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }            

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            // Buscar música/playlist de música
            await ctx.DeferResponseAsync();
            (List<LavalinkTrack> tracks, bool isSucess, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult) = await JukeboxSearchTracks(query);            

            if (!isSucess)
            {
                await CommandErrorResponse(ctx, "Nenhuma música foi encontrada");
                return;
            }

            // Adiciona uma ou mais músicas à fila
            bool hadTrack = player.JukeboxHaveTrack;
            var uniqueTrack = tracks.First();

            if (isSearch || (!isPlaylist && !isSearch))
            {
                await player.AddTrackAndPlay(uniqueTrack);
            }

            if (isPlaylist)
            {
                await player.AddTracksAndPlay(tracks);
            }

            // Mensagem de resposta final
            DiscordContainerComponent container;

            string message;
            if (!hadTrack)
                message = "Tocando Agora";
            else
                message = "Adicionado à fila";

            if (!isPlaylist)
                container = new([await JukeboxTrackSection(uniqueTrack, message, player)], color: new DiscordColor(config.JukeboxEmbedColorHex));
            else
                container = new([await JukeboxPlaylistQueuedSection(trackLoadResult.Playlist, message, config, player)], color: new DiscordColor(config.JukeboxEmbedColorHex));

            DiscordMessageBuilder builder = new();
            builder.EnableV2Components();
            builder.AddContainerComponent(container);
            await ctx.RespondAsync(builder);
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Playnow")]
    [Description("Toca uma música imediatamente")]
    public async Task PlayNow(CommandContext ctx, [Description("Nome, link, etc. da música")] string query)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            // Buscar música/playlist de música
            await ctx.DeferResponseAsync();
            (List<LavalinkTrack> tracks, bool isSucess, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult) = await JukeboxSearchTracks(query);

            if (!isSucess)
            {
                await CommandErrorResponse(ctx, "Nenhuma música foi encontrada");
                return;
            }

            // Toca a música e, caso for playlist, coloca as subsequentes na fila           
            var uniqueTrack = tracks.First();            

            if (isSearch || (!isPlaylist && !isSearch))
            {
                await player.PlayAsync(uniqueTrack);
            }
            else if (isPlaylist)
            {
                var toAdd = new List<LavalinkTrack>(tracks);
                toAdd.Remove(uniqueTrack);

                await player.PlayAsync(uniqueTrack);
                await player.AddTracksAndPlay(tracks);
            }

            // Mensagem de resposta final
            DiscordContainerComponent container;

            string message = "Tocando Agora";            

            if (!isPlaylist)
                container = new([await JukeboxTrackSection(uniqueTrack, message, player)], color: new DiscordColor(config.JukeboxEmbedColorHex));
            else
                container = new([await JukeboxPlaylistQueuedSection(trackLoadResult.Playlist, message, config, player)], color: new DiscordColor(config.JukeboxEmbedColorHex));

            DiscordMessageBuilder builder = new();
            builder.EnableV2Components();
            builder.AddContainerComponent(container);
            await ctx.RespondAsync(builder);
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Playnext")]
    [Description("Adiciona uma música no ínício da fila")]
    public async Task PlayNext(CommandContext ctx, [Description("Nome, link, etc. da música")] string query)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            // Buscar música/playlist de música
            await ctx.DeferResponseAsync();
            (List<LavalinkTrack> tracks, bool isSucess, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult) = await JukeboxSearchTracks(query);

            if (!isSucess)
            {
                await CommandErrorResponse(ctx, "Nenhuma música foi encontrada");
                return;
            }

            // Adiciona uma música no início da fila       
            var uniqueTrack = tracks.First();

            if (isSearch || (!isPlaylist && !isSearch))
            {
                player.TrackQueue.Insert(0, uniqueTrack);               
            }
            if (isPlaylist)
            {
                await CommandErrorResponse(ctx, "Você só pode adicionar uma música na fila com esse comando");
            }

            // Mensagem de resposta final
            DiscordContainerComponent container;

            string message = "Adicionado à fila";                                        

            container = new([await JukeboxTrackSection(uniqueTrack, message, player)], color: new DiscordColor(config.JukeboxEmbedColorHex));
            
            DiscordMessageBuilder builder = new();
            builder.EnableV2Components();
            builder.AddContainerComponent(container);
            await ctx.RespondAsync(builder);
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    // GERENCIAMENTO DE PLAYER

    [Command("Join")]
    [Description("Conecta a Jukebox na call")]
    public async Task Join(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            // OBS: JukeboxInitialChecks já verifica se o usuário está ou não em uma call ou na mesma call do bot;
            //      Além disso ela também recupera o player de lavalink e, se ele não estiver conectado, já conecta ele na call
            //      (que é exatamente o que queremos, por isso esse comando é vazio e apenas chama o JukeboxInitialChecks)
            var player = await JukeboxInitialChecks(ctx);            
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Stop")]
    [Description("Desconecta e limpa a fila da jukebox")]
    public async Task Stop(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            await player.DisconnectAsync();
            await player.DisposeAsync();
            await ctx.RespondAsync("Jukebox desconectada");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Skip")]
    [Description("Pula a música atual e toca a próxima da fila")]
    public async Task Skip(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            await player.SkipTrack();
            await ctx.RespondAsync("Pulando música");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Pause")]
    [Description("Pausa ou despausa a reprodução da música atual")]
    public async Task Pause(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            if (player.IsPaused)
            {
                await player.ResumeAsync();
                await ctx.RespondAsync("Reprodução despausada");
            }
            else
            {
                await player.PauseAsync();
                await ctx.RespondAsync("Reprodução pausada");
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Seek")]
    [Description("Coloca a música atual em um tempo específico")]
    public async Task Seek(CommandContext ctx, [Description("Suporta formatos como: XX:YY:ZZ ou XXhYYmZZs")] TimeSpan tempo, [Description("Usa o momento atual da música se verdadeiro")] bool useRelative = false)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            if (useRelative)
            {
                if (player.CurrentTrack == null)
                {
                    await CommandErrorResponse(ctx, "Não há música na Jukebox");
                    return;
                }                    

                tempo = player.Position.Value.Position + tempo;
            }

            var result = await ChangePlayerTrackTime(player, tempo);

            if (!result)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }                

            await ctx.RespondAsync($"Momento de reprodução alterado: *({PrintTimeSpan(tempo)}/{PrintTimeSpan(player.CurrentTrack.Duration)})*");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("forward")]
    [Description("Pula 10 segundos da música atual")]
    public async Task Jump10sec(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;


            if (player.CurrentTrack == null)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }

            var tempo = player.Position.Value.Position + TimeSpan.FromSeconds(10);
            var result = await ChangePlayerTrackTime(player, tempo);

            if (!result)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }

            await ctx.RespondAsync($"Momento de reprodução alterado: ({PrintTimeSpan(tempo)})/({PrintTimeSpan(player.CurrentTrack.Duration)})");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("back")]
    [Description("Retrocede 10 segundos da música atual")]
    public async Task JumpLess10sec(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;


            if (player.CurrentTrack == null)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }

            var tempo = player.Position.Value.Position - TimeSpan.FromSeconds(10);
            var zero = TimeSpan.FromSeconds(0);

            if (tempo < zero)
            {
                tempo = zero;
            }

            var result = await ChangePlayerTrackTime(player, tempo);

            if (!result)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }

            await ctx.RespondAsync($"Momento de reprodução alterado: ({PrintTimeSpan(tempo)})/({PrintTimeSpan(player.CurrentTrack.Duration)})");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Restart")]
    [Description("Reinicia a música atual")]
    public async Task Restart(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            if (player.CurrentTrack == null)
            {
                await CommandErrorResponse(ctx, "Não há música na Jukebox");
                return;
            }

            await player.SeekAsync(TimeSpan.FromSeconds(0));
            await ctx.RespondAsync($"{player.CurrentTrack.Title} foi recomeçada");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    // GERENCIAMENTO DE FILA

    [Command("Queue")]
    [Description("Mostra a fila de músicas")]
    // TODO: Criar uma nova versão padrão da Jukebox que faz com que a lista seja feita de embeds com as thumbs da música,
    // com as próximas músicas e qual a música atual que está tocando
    // [Description("Se deve mostrar a versão simplificada ou não")] bool simplificar = false
    public async Task Queue(CommandContext ctx, [Description("A página mostrada")] int pagina = 1)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            bool simplificar = true;

            if (simplificar)
            {
                DiscordEmbedBuilder embed = new();
                embed.Title = "Fila da Jukebox";
                embed.Color = new DiscordColor(config.JukeboxEmbedColorHex);               

                // Música Atual
                if (player.CurrentTrack != null)
                {
                    embed.AddField("Música Atual", player.CurrentTrack.Title + $" ***({PrintTimeSpan(player.Position.Value.Position)}/{PrintTimeSpan(player.CurrentTrack.Duration)})***");
                }

                // Fila de músicas
                string fila = "";

                var result = Paginate(player.TrackQueue, out bool validPage, out int indexDaPrimeiraMusicaPag, out int indexMusicaFinalPag, 
                    out int qntPaginas, pagina, config.JukeboxSimplifiedQueueMessageMaxTracks);                

                if (player.TrackQueue.Count == 0)
                {
                    fila = "A fila está vazia. Use o comando play para inserir novas músicas";
                } 
                else if (!validPage)
                {
                    await CommandErrorResponse(ctx, $"Essa página não existe. Tente com uma entre 1 e {qntPaginas}");
                    return;
                }
                else
                {
                    int index = indexDaPrimeiraMusicaPag;
                    for (int i = 0; i < result.Count(); i++)
                    {
                        var song = player.TrackQueue[index];

                        fila += $"**{index}:** {song.Title} *({PrintTimeSpan(song.Duration)})*\n";

                        index++;
                    }

                    fila += $"\n\nExibindo página {pagina} de {qntPaginas}";
                }

                embed.AddField("Próximas Músicas", fila);

                var finalEmbed = embed.Build();
                await ctx.RespondAsync(finalEmbed);
            }
            else
            {
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Recents")]
    [Description("Mostra as músicas tocadas recentemente")]
    public async Task RecentQueue(CommandContext ctx, [Description("A página mostrada")] int pagina = 1)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            DiscordEmbedBuilder embed = new();
            embed.Title = "Músicas Anteriores";
            embed.Color = new DiscordColor(config.JukeboxEmbedColorHex);

            // Fila Recente
            string filaRecente = "";

            var result = Paginate(player.RecentTracks, out bool validPage, out int indexDaPrimeiraMusicaPag,
                out int indexMusicaFinalPag, out int qntPaginas, pagina, config.JukeboxSimplifiedQueueMessageMaxTracks);                      

            if (player.RecentTracks.Count == 0)
            {
                filaRecente = "Não há músicas recentes. Quando uma tocar, ficará guardada aqui";
            }
            else if (!validPage)
            {
                await CommandErrorResponse(ctx, $"Essa página não existe. Tente com uma entre 1 e {qntPaginas}");
                return;
            }
            else
            {
                int index = indexDaPrimeiraMusicaPag;
                for (int i = 0; i < result.Count(); i++)
                {
                    var song = player.RecentTracks[index];

                    filaRecente += $"**{index - player.RecentTracks.Count}:** {song.Title} *({PrintTimeSpan(song.Duration)})*\n";

                    index++;
                }

                filaRecente += $"\n\nExibindo página {pagina} de {qntPaginas}";
            }

            embed.WithDescription(filaRecente);            

            var finalEmbed = embed.Build();
            await ctx.RespondAsync(finalEmbed);
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Queuenext")]
    [Description("Coloca uma música da fila no início")]
    public async Task QueueNext(CommandContext ctx, [Description("Índice da música para ser a próxima a tocar")] int id, 
        [Description("Se a música atual desse ser pulada")] bool pular = false)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            if (id < 0 || id > player.TrackQueue.Count)
            {
                await ctx.RespondAsync("Uma música com esse índice não existe na fila. Tente usar o comando de fila para verificar.");
                return;
            }

            await player.PauseAsync();

            LavalinkTrack track = player.TrackQueue[id];
            List<LavalinkTrack> copy = new(player.TrackQueue);

            copy.Remove(track);

            player.TrackQueue.Clear();
            player.TrackQueue.Add(track);
            player.TrackQueue.AddRange(copy);

            await player.ResumeAsync();
            await ctx.RespondAsync($"{track.Title} será a próxima música a ser tocada");

            if (pular)
            {
                await player.SkipTrack();
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }            
    
    [Command("Remove")]
    [Description("Remove uma música de indíce ID da fila de músicas")]
    public async Task QueueRemove(CommandContext ctx, [Description("Índice da música à retirar")] int id)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;
            
            if (id < 0 || id > player.TrackQueue.Count)
            {
                await ctx.RespondAsync("Uma música com esse índice não existe na fila. Tente usar o comando de fila para verificar.");
                return;
            }

            var track = player.TrackQueue[id];
            player.TrackQueue.Remove(track);

            await ctx.RespondAsync($"{track.Title} foi removida da fila");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Clear")]
    [Description("Limpa a fila de músicas")]
    public async Task QueueClear(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            player.TrackQueue.Clear();
            await ctx.RespondAsync("A fila de músicas foi limpa");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("Skipto")]
    [Description("Pula até a música de índice ID da fila")]
    public async Task SkipTo(CommandContext ctx, [Description("Índice da música para pular")] int id)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            await ctx.DeferResponseAsync();

            if (id < 0 || id > player.TrackQueue.Count)
            {
                await ctx.RespondAsync("Uma música com esse índice não existe na fila. Tente usar o comando de fila para verificar.");
                return;
            }

            await player.PauseAsync();

            for (int i = 0; i <= id - 1; i++)
            {
                player.TrackQueue.RemoveAt(0);
            }

            await player.SkipTrack();
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

    [Command("shuffle")]
    [Description("Aleatoriza a fila atual de música")]
    public async Task Shuffle(CommandContext ctx)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
            return;
        }

        try
        {            
            var player = await JukeboxInitialChecks(ctx);

            await ctx.DeferResponseAsync();

            var initial = new List<LavalinkTrack>(player.TrackQueue);
            var final = new List<LavalinkTrack>();

            Random rng = new();

            while (initial.Count > 0)
            {
                var currentIndex = rng.Next(0, initial.Count);
                var current = initial[currentIndex];
                initial.Remove(current);
                final.Add(current);
            }

            player.ChangeQueue(final);            

            await ctx.RespondAsync("Fila embaralhada");
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

}


public class JukeboxPlayer(IPlayerProperties<JukeboxPlayer, JukeboxPlayerConfig> properties) : LavalinkPlayer(properties)
{
    private List<LavalinkTrack> trackQueue = new();
    private List<LavalinkTrack> recentTracks = new();
    private JukeboxConfig config = properties.Options.Value.Config;
    public DiscordChannel bindedTextChannel = properties.Options.Value.BindedTextChannel;
    private Jukebox bindedModule = properties.Options.Value.BindedModule;

    public bool JukeboxHaveTrack => State == PlayerState.Playing || State == PlayerState.Paused;
    public bool JukeboxIsPaused => State == PlayerState.Paused;
    public LavalinkTrack JukeboxCurrentTrack => CurrentTrack;
    public List<LavalinkTrack> TrackQueue => trackQueue;
    public List<LavalinkTrack> RecentTracks => recentTracks;


    public static ValueTask<JukeboxPlayer> CreatePlayerAsync(IPlayerProperties<JukeboxPlayer, JukeboxPlayerConfig> properties, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new JukeboxPlayer(properties));
    }
    

    public async Task AddTracksAndPlay(IEnumerable<LavalinkTrack> tracks)
    {
        LavalinkTrack firstTrack;

        if (!tracks.Any())
            return;

        firstTrack = tracks.First();
        var tracksList = tracks.ToList();
        tracksList.RemoveAt(0);

        if (!JukeboxHaveTrack)
        {
            await PlayAsync(firstTrack);
        }
        else
        {
            trackQueue.Add(firstTrack);
        }

        trackQueue.AddRange(tracksList);
    }

    public async Task AddTrackAndPlay(LavalinkTrack track)
    {
        if (JukeboxHaveTrack)
        {
            trackQueue.Add(track);
        }
        else
        {
            await PlayAsync(track);
        }
    }

    public async Task SkipTrack()
    {        
        if (trackQueue.Count <= 0)
        {
            await StopAsync();            
        }
        else
        {
            var nextTrack = trackQueue[0];
            trackQueue.RemoveAt(0);
            await PlayAsync(nextTrack);
        }            
    }    

    public void ChangeQueue(List<LavalinkTrack> newQueue)
    {
        trackQueue = newQueue;
    }


    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);
        
        // TODO: Atualizar "player" do discord (Um embed que mostra momento da música, com opções pra pular, pausar, tocar, etc.)
    }

    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);

        if (endReason == TrackEndReason.LoadFailed)
        {
            await bindedTextChannel.SendMessageAsync("Erro ao tentar carregar: " + track.Track.Title + $" ({track.Track.Uri.Host})");
        }

        // funcionamento das músicas recentes
        recentTracks.Add(track.Track);

        if (endReason == TrackEndReason.Replaced || endReason == TrackEndReason.Cleanup)
        {            
            return;
        }        

        if (trackQueue.Count > 0)
        {
            // funcionamento da fila            
            var nextTrack = trackQueue[0];
            trackQueue.RemoveAt(0);
            await PlayAsync(nextTrack, cancellationToken: CancellationToken.None);            

            // mensagem do "Tocando Agora":
            DiscordContainerComponent container = new([await Jukebox.JukeboxTrackSection(nextTrack, "Tocando Agora", this)], color: new DiscordColor(config.JukeboxEmbedColorHex));
            DiscordMessageBuilder builder = new();
            builder.EnableV2Components();
            builder.AddContainerComponent(container);
            await builder.SendAsync(bindedTextChannel);
        }
        else
        {
            await bindedTextChannel.SendMessageAsync("A fila da jukebox está vazia!");            
        }        
    }

    protected override async ValueTask NotifyTrackExceptionAsync(ITrackQueueItem track, TrackException exception, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackExceptionAsync(track, exception, cancellationToken);
        await bindedTextChannel.SendMessageAsync($"Ocorreu um erro na reprodução da música {track.Track.Title}: {exception.Cause}");

        _ = Task.Run(() => bindedModule.CallTrackException(track, exception), CancellationToken.None);        
    }

    protected override async ValueTask NotifyTrackStuckAsync(ITrackQueueItem track, TimeSpan threshold, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStuckAsync(track, threshold, cancellationToken);
        await bindedTextChannel.SendMessageAsync($"A reprodução da música {track.Track.Title} travou");

        _ = Task.Run(() => bindedModule.CallTrackStuck(track, threshold), CancellationToken.None);
    }

    protected override ValueTask NotifyVoiceStateUpdatedAsync(VoiceState voiceState, CancellationToken cancellationToken = default)
    {
        return base.NotifyVoiceStateUpdatedAsync(voiceState, cancellationToken);        
    }    

}

public record JukeboxPlayerConfig : LavalinkPlayerOptions
{
    public DiscordChannel BindedTextChannel { get; set; }
    public JukeboxConfig Config { get; set; }
    public Jukebox BindedModule { get; set; }
}

public class JukeboxConfig
{
    public string LavalinkIP { get; set; } = "localhost";
    public uint LavalinkPort { get; set; } = 2333;
    public string LavalinkKeyword { get; set; } = "youshallnotpass";
    public bool LavalinkSecure { get; set; } = true;
    public List<string> LavalinkSearchSources { get; set; } = ["Youtube", "Spotify", "Soundcloud"];
    public bool WhitelistEnable { get; set; } = true;
    public ulong WhitelistChannel { get; set; } = 0;
    public string DefaultFallbackPlaylistImageUrl { get; set; } = "https://cdn-icons-png.flaticon.com/512/608/608386.png";
    public string JukeboxEmbedColorHex { get; set; } = "#F55305";
    public int JukeboxSimplifiedQueueMessageMaxTracks { get; set; } = 10;
    public int JukeboxDisconnectOnInactiveMinutes { get; set; } = 10;
    public int JukeboxDisconnectOnAlone { get; set; } = 5;
}

public class JukeboxData
{
}