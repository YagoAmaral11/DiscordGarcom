using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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
    JukeboxPlayer currentServerPlayer;
    

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

        jukebox.WithSubcommands([play]);

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
        
        services.AddLogging(logging =>
        {
            logging.AddConsole(); // Garante que vai para o console
            logging.SetMinimumLevel(LogLevel.Trace); // Permite logs detalhados
            logging.AddFilter("DSharpPlus.Net.Gateway.ITransportService", LogLevel.Trace);
        });        

        services.AddLavalink();

        services.ConfigureLavalink(c =>
        {
            string baseAdressPrefix = config.LavalinkSecure ? "https" : "http";
            c.BaseAddress = new Uri($"{baseAdressPrefix}://{config.LavalinkIP}:{config.LavalinkPort}");
            c.Passphrase = config.LavalinkKeyword;
        });

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
            BindedTextChannel = TextChannel,
            SelfDeaf = true,
            SelfMute = false,
        };

        LavalinkPlayerOptions lavalinkPlayerOptions = new()
        {            
            SelfDeaf = true,
            SelfMute = false,
        };

        var serverID = serverContext.BindedDiscordServer.Id;
        var options = Options.Create(jukeboxPlayerOptions);                
        // var result = audioService.Players.RetrieveAsync(serverID, VoiceChannelID, PlayerFactory.Default, Options.Create(lavalinkPlayerOptions), playerRetrieveOptions);
        var result = await audioService.Players.RetrieveAsync<JukeboxPlayer, JukeboxPlayerConfig>(serverID, VoiceChannelID, JukeboxPlayer.CreatePlayerAsync, options, playerRetrieveOptions);        
        return result;
    }

    // Serve para pegar o player do lavalink; Returna true se o player estiver no mesmo canal
    private async Task<(bool sucess, JukeboxPlayer player)> GetLavaPlayer(ulong channelID, DiscordChannel TextChannel)
    {
        var result = await Internal_GetLavaPlayer(channelID, TextChannel, false);

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


    private static bool JukeboxHaveTrack(LavalinkPlayer player) => player.State == PlayerState.Playing || player.State == PlayerState.Paused;
    private static bool JukeboxIsPaused(LavalinkPlayer player) => player.State == PlayerState.Paused;
    private static LavalinkTrack JukeboxCurrentTrack(LavalinkPlayer player) => player.CurrentTrack;



    [Command("Play")]
    [Description("Toca ou coloca na fila uma música na jukebox")]
    public async Task Play(CommandContext ctx, [Description("Nome, link, etc. da música")] string query)
    {
        if (!serverContext.ReadyForCommands)
        {
            await CommandErrorResponse(ctx, "O bot está inicializando. Aguarde e tente novamente em breve");
        }            

        try
        {
            var player = await JukeboxInitialChecks(ctx);
            if (player == null)
                return;

            // Buscar música/playlist de música
            await ctx.DeferResponseAsync();
            (List<LavalinkTrack> tracks, bool isSucess, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult) = await JukeboxSearchTracks(query);
            LavalinkTrack firstTrack = null;                        

            if (isSucess)
            {
                firstTrack = tracks.FirstOrDefault();

                if (isPlaylist | isSearch)
                {
                    tracks.RemoveAt(0);
                }
            }            

            if (!JukeboxHaveTrack(player))
            {
                // Tocar imediatamente
                await player.PlayAsync(firstTrack); 
                // TODO: Enviar mensagem que a música está tocando
                // TODO: Atualizar "player" do discord (Um embed que mostra momento da música, com opções pra pular, pausar, tocar, etc.)
            }
            else
            {
                player.TrackQueue.Add(firstTrack);
                // TODO: Enviar mensagem que a música foi adicionada na fila
            }

            if (isPlaylist)
            {
                // Buscar outras músicas da playlist
                player.TrackQueue.AddRange(tracks);
                // TODO: Enviar mensagem que as músicas foi adicionada na fila
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

}


public class JukeboxPlayer : LavalinkPlayer
{
    List<LavalinkTrack> trackQueue = new(); public List<LavalinkTrack> TrackQueue => trackQueue;
    List<LavalinkTrack> recentTracks = new(); public List<LavalinkTrack> RecentTracks => recentTracks;
    public DiscordChannel bindedTextChannel;

    public JukeboxPlayer(IPlayerProperties<JukeboxPlayer, JukeboxPlayerConfig> properties) : base(properties)
    {
        bindedTextChannel = properties.Options.Value.BindedTextChannel;
    }

    public static ValueTask<JukeboxPlayer> CreatePlayerAsync(IPlayerProperties<JukeboxPlayer, JukeboxPlayerConfig> properties, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new JukeboxPlayer(properties));
    }

    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem track, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackStartedAsync(track, cancellationToken);

        // TODO: Falar que está tocando agora a seguinte música
    }

    protected override async ValueTask NotifyTrackEndedAsync(ITrackQueueItem track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        await base.NotifyTrackEndedAsync(track, endReason, cancellationToken);

        if (trackQueue.Count > 0)
        {
            var nextTrack = trackQueue[0];
            trackQueue.RemoveAt(0);
            await PlayAsync(nextTrack, cancellationToken: CancellationToken.None);            
        }
        else
        {
            await bindedTextChannel.SendMessageAsync("A fila da jukebox está vazia!");            
        }        
    }

}

public record JukeboxPlayerConfig : LavalinkPlayerOptions
{
    public DiscordChannel BindedTextChannel { get; set; }
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
}

public class JukeboxData
{
}