using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    
    List<LavalinkTrack> TrackQueue = new();
    List<LavalinkTrack> RecentTracks = new();

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


        foreach (var str in config.LavalinkSearchSources)
        {
            string cleanStr = str.Trim().Replace(" ", "").Replace("-", "");

            if (Enum.TryParse<TrackSearchMode>(cleanStr, true, out var result))
            {
                if (!searchModes.Contains(result))
                    searchModes.Add(result);
            }            
        }

        if (searchModes.Count == 0)
        {
            throw new ArgumentException("Could not parse an valid Lavalink Search Mode. Change config and try to add \"Youtube\" or \"Spotify\".");
        }
    }

    public override Task Start() => Task.CompletedTask;



    private async Task<PlayerResult<LavalinkPlayer>> Internal_GetLavaPlayer(ulong channelId, bool joinVoiceChannel = true)
    {
        PlayerChannelBehavior channelBehavior = joinVoiceChannel ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None;
        PlayerRetrieveOptions playerRetrieveOptions = new(ChannelBehavior: channelBehavior);
        LavalinkPlayerOptions lavalinkPlayerOptions = new()
        {
            SelfDeaf = true,
            SelfMute = false,
        };

        var result = await audioService.Players.RetrieveAsync(serverContext.BindedDiscordServer.Id, channelId, PlayerFactory.Default, Options.Create(lavalinkPlayerOptions), playerRetrieveOptions);
        return result;
    }

    // Serve para pegar o player do lavalink; Returna true se o player estiver no mesmo canal
    private async Task<(bool sucess, LavalinkPlayer player)> GetLavaPlayer(ulong channelID)
    {
        var result = await Internal_GetLavaPlayer(channelID, true);

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
        ulong? memberVcID = ctx.Member.VoiceState.ChannelId;

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
    private async Task<LavalinkPlayer> JukeboxInitialChecks(CommandContext ctx)
    {
        if (await VerifyMemberChannel(ctx) == false)
            return null;

        ulong memberVC = ctx.Member.VoiceState.ChannelId.Value;

        var (sucess, player) = await GetLavaPlayer(memberVC);

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
    private async Task<(List<LavalinkTrack>, bool isPlaylist, bool isSearch, TrackLoadResult trackLoadResult)> JukeboxSearchTracks(string query, bool returnSearch = false)
    {
        TrackLoadResult trackLoadResult;

        if (Uri.TryCreate(query, UriKind.Absolute, out var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
        {
            // Buscar Diretamente
            trackLoadResult = await audioService.Tracks.LoadTracksAsync(query, new TrackLoadOptions());
        }
        else
        {
            // Buscar com fallback
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
                    break;
                }
            }

            // Não conseguiu encontrar nada em nenhum modo de busca
            return ([], false, returnSearch, new TrackLoadResult());
        }                

        if (trackLoadResult.Playlist is not null)
        {
            return (new List<LavalinkTrack>(trackLoadResult.Tracks), true, false, trackLoadResult);
        }
        else if (trackLoadResult.Tracks.Length > 1)
        {
            if (returnSearch)
            {
                return (new List<LavalinkTrack>(trackLoadResult.Tracks), false, true, trackLoadResult);
            }
            else
            {
                return ([trackLoadResult.Tracks.FirstOrDefault()], false, false, trackLoadResult);
            }
        }
        else
        {
            return ([trackLoadResult.Track], false, false, trackLoadResult);
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

            if (!JukeboxHaveTrack(player))
            {                                                
                // Buscar música/playlist de música
                await player.PlayAsync(currentTrack); // Tocar imediatamente
                // Enviar mensagem que a música está tocando
                // TODO: Atualizar "player" do discord (Um embed que mostra momento da música, com opções pra pular, pausar, tocar, etc.)
            }
            else
            {
                // Buscar música/playlist de música
                // Colocar na fila de músicas
                // Enviar mensagem que a música foi adicionada na fila
            }
        }
        catch (Exception e)
        {
            await ((IModule) this).DumpException(e, persistance);
        }
    }

}

public class JukeboxConfig
{
    public string LavalinkIP { get; set; } = "https://localhost";
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