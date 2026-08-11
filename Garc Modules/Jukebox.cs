using DiscordGarçom.Containers.Core;
using DiscordGarçom.Containers.Core.Modules;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
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
using System.Threading.Tasks;

namespace DiscordGarçom.GarcModules;

public class Jukebox(IPersistance persistance, IConfigPersistance configPersistance) : BaseModule<JukeboxConfig, JukeboxData>(persistance, configPersistance)
{
    public override string Name => "Jukebox";
    protected override bool ThrowExceptionOnMissingConfig => true;

    protected override JukeboxConfig InitializeConfig() => new();
    protected override JukeboxData InitializeData() => new();

    IAudioService audioService;    
    LavalinkTrack currentTrack = null;
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

    private async Task<(bool sucess, PlayerRetrieveStatus? failureReason)> PlayMusic(string musicQuery, ulong playerChannel)
    {
        var playerResult = await Internal_GetLavaPlayer(playerChannel, true);

        if (!playerResult.IsSuccess)
            return (false, playerResult.Status);

        if (playerResult.Player == null)
            return (false, playerResult.Status);

        LavalinkPlayer player = playerResult.Player;

        LavalinkTrack track = await audioService.Tracks.LoadTrackAsync(musicQuery, new TrackLoadOptions(SearchMode: TrackSearchMode.YouTube));

        if (track is null)
            return (false, null);

        await player.PlayAsync(track);      
        return (true, null);
    }


    [Command("Play")]
    [Description("Toca uma música na jukebox")]
    public async Task Play(CommandContext ctx, [Description("Nome, link, etc. da música")] string query)
    {
        try
        {
            await ctx.DeferResponseAsync();

            DiscordChannel vcUser;

            if (ctx.Member.VoiceState != null && ctx.Member.VoiceState.ChannelId != null)
            {
                vcUser = await serverContext.BindedDiscordServer.GetChannelAsync(ctx.Member.VoiceState.ChannelId.Value);
            }
            else
            {
                await ctx.RespondAsync("Você deve estar em um canal de voz para usar esse comando");
                return;
            }

            var result = await PlayMusic(query, vcUser.Id);

            if (!result.sucess)
            {
                string failReason = result.failureReason.ToString();
                await ctx.FollowupAsync($"Erro tentando tocar: {failReason}");
                return;
            }
            else
            {
                await ctx.FollowupAsync($"Tocando agora");
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
}

public class JukeboxData
{
}