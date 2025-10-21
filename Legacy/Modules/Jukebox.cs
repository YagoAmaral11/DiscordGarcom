using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Net;
using GarçomDoKitts.configs;
using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.DSharpPlus;
using Lavalink4NET.Extensions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace GarçomDoKitts
{    
    public class Jukebox
    {
        // Config                
        [JsonIgnore] public static ulong channelWhitelistId => Program.config.Jukebox_CommandChannel;
        [JsonIgnore] public static List<JukeboxEndpoint> endpoints = new List<JukeboxEndpoint>();
        [JsonIgnore] public static string endpointsPath = $"{DataIO.DataFolderPath}lavalinks.json";

        // Runtime data
        [JsonIgnore] public DiscordChannel channelMusic; // Canal para enviar mensagens de música        
        [JsonIgnore] public LavalinkTrack songCurrent = null; // Qual música está tocando agora
        [JsonIgnore] public List<LavalinkTrack> songQueue = new(); // Quais as próximas músicas que devem tocar
        [JsonIgnore] public bool songPaused = false;        
        [JsonIgnore] public JukeboxEndpoint connectedEndpoint; // qual endpoint está conectado no momento       
        [JsonIgnore] public float timeoutMs;

        [JsonIgnore] public IServiceProvider lavalinkService;
        [JsonIgnore] public IAudioService audioService; // Gerencia os players do lavalink
        [JsonIgnore] public QueuedLavalinkPlayer lavalinkPlayer; // Gerencia o próprio player do lavalink, conectado em um canal

        // Properties        
        public async Task<DiscordChannel> GetConnectionChannel()
        {
            if (lavalinkPlayer == null || lavalinkPlayer.VoiceChannelId == 0)
                return null;

            return await Program.client.GetChannelAsync(lavalinkPlayer.VoiceChannelId);
        }        

        [JsonIgnore] public bool IsConnected 
        {
            get
            {
                if (lavalinkPlayback != null && channelConnectedVC != null)
                {
                    return true;
                }
                return false;
            }
        }
        [JsonIgnore] public bool ThereIsQueue 
        {
            get
            {
                if (songQueue.Count > 0)
                {
                    return true;
                }
                return false;
            }
        }

        /*
         * TODO: Usado apenas para referência; Depois remover
            ANTIGO:
            // [JsonIgnore] public LavalinkExtension lavalink; // Lavalink desse bot
            // [JsonIgnore] public LavalinkNodeConnection lavalinkNode; // Nodo usado nesse server para música        
            // [JsonIgnore] public LavalinkGuildConnection lavalinkPlayback => lavalinkNode.GetGuildConnection(channelMusic.Guild);
            // [JsonIgnore] public DiscordChannel channelConnectedVC; // Canal que o bot está conectado        
         */

        public async Task Init()
        {
            JsonDocument json = JsonDocument.Parse(File.ReadAllText(endpointsPath));
            endpoints = new List<JukeboxEndpoint>();

            // Inicializa os endpoints
            foreach (var ep in json.RootElement.GetProperty("endpoints").EnumerateArray())
            {
                JukeboxEndpoint e = new(
                    ep.GetProperty("Hostname").GetString(),
                    ep.GetProperty("Port").GetInt32(),
                    ep.GetProperty("SSH").GetBoolean(),
                    ep.GetProperty("Password").GetString()
                );
                endpoints.Add(e);
            }

            // Inicializa o Lavalink                        
            HostApplicationBuilder servicesBuilder = Host.CreateApplicationBuilder();
            servicesBuilder.Services.AddLavalink();
            servicesBuilder.Services.ConfigureLavalink(options =>
            {                
                options.Hostname = config.Lavalink_Hostname;
                options.Port = config.Lavalink_Port;
                options.Password = config.Lavalink_Password;
                options.Ssl = config.Lavalink_Ssl;
            });
            IHost servicesHost = servicesBuilder.Build();
            lavalinkService = servicesHost.Services;
            await servicesHost.RunAsync();

            // Inicializa runtime da jukebox
            songCurrent = null;
            songQueue = new();
            songPaused = false;
            channelMusic = await Program.client.GetChannelAsync(channelWhitelistId);

            // Inicializa o audio service
            audioService = Program.services.GetRequiredService<IAudioService>();

            // lavalink = Program.client.UseLavalink();
            // await Connect();                                                                         
        }

        public async void Loop()
        {
            // Sair da call se não tiver ninguém            
            if (IsConnected && lavalinkPlayback.Channel.Users.Count == 1)
            {
                if (timeoutMs > 0)
                {
                    timeoutMs -= Program.config.Timers_TickTimerMs;
                }
                else
                {
                    Console.WriteLine("(Jukebox) Ninguém na call, desconectando por timeout");
                    await DisconnectAndReset();
                    timeoutMs = Program.config.Jukebox_Timeout;
                }
            }
            else if (IsConnected && lavalinkPlayback.Channel.Users.Count > 1)
            {
                timeoutMs = Program.config.Jukebox_Timeout;
            }
        }


        // Conecta em algum dos endpoints configurados
        private async Task<bool> Connect()
        {
            LavalinkNodeConnection connection = null;
            int current = 0;

            // Tenta conectar em vários endpoints
            while (current < endpoints.Count)
            {
                connection = await ConnectToEndpoint(endpoints[current]);                
                
                if (connection != null)
                {
                    lavalinkNode = connection;
                    connectedEndpoint = endpoints[current]; // salva o endpoint conectado

                    lavalinkNode.GuildConnectionRemoved += LavalinkNode_GuildConnectionRemoved;                              

                    Console.WriteLine($"(Jukebox) Conectado em: {connectedEndpoint.Hostname}");
                    return true;
                }
                else
                {
                    current++;
                }
            }

            lavalinkNode = null; // nenhum nodo conectado
            connectedEndpoint = null; // nenhum endpoint conectado  
            Console.WriteLine("(Jukebox) All nodes did not respond");
            return false;
        }

        // Conecta em um host de lavalink
        private async Task<LavalinkNodeConnection> ConnectToEndpoint(JukeboxEndpoint endpoint)
        {
            ConnectionEndpoint e = endpoint.ToConnectionEndpoint();
            LavalinkNodeConnection con = null;

            LavalinkConfiguration config = new()
            {
                Password = endpoint.Password,
                RestEndpoint = e,
                SocketEndpoint = e,
                SocketAutoReconnect = false                
            };

            try
            {
                con = await lavalink.ConnectAsync(config);
            }
            catch (Exception ex)
            {
                return null;
            }

            return con;
        }

        private async Task<bool> RestoreConnection()
        {
            var musicTime = lavalinkPlayback.CurrentState.PlaybackPosition;
            var discordChannel = lavalinkPlayback.Channel;

            // Resets connection
            await lavalinkPlayback.StopAsync();
            await lavalinkNode.StopAsync();

            // Retry connecting
            await Connect();

            if (lavalinkPlayback != null)
            {
                await lavalinkNode.ConnectAsync(discordChannel); // Reconecta no VC
                await lavalinkPlayback.PlayAsync(songCurrent); // Toca a música que estava tocando
                await lavalinkPlayback.SeekAsync(musicTime); // Retorna para o tempo que estava tocando

                Console.WriteLine("(Jukebox) Connection Restored");
                return true;
            }
            else
            {
                Console.WriteLine("(Jukebox) All of lavalink nodes didnt respond");
                return false;
            }
        }

        public async Task<bool> ChangeConnection()
        {
            LavalinkNodeConnection connection = null;
            int currentConnection = endpoints.IndexOf(connectedEndpoint);
            int current = 0;

            // Tenta conectar nos endpoints que não sejam o que está conectado no momento
            while (current < endpoints.Count)
            {
                if (current == currentConnection)
                {
                    continue;
                }

                connection = await ConnectToEndpoint(endpoints[current]);

                if (connection != null)
                {
                    lavalinkNode = connection;
                    connectedEndpoint = endpoints[current]; // salva o endpoint conectado
                    Console.WriteLine($"(Jukebox) Conectado em: {connectedEndpoint.Hostname}");
                    return true;
                }
                else
                {
                    current++;
                }
            }

            lavalinkNode = null; // nenhum nodo conectado
            connectedEndpoint = null; // nenhum endpoint conectado  
            Console.WriteLine("(Jukebox) All nodes did not respond");
            return false;
        }

        // Retorna verdadeiro se tiver mandando mensagem no canal certo
        public async Task<bool> VerifyWhitelist(DiscordChannel mensagemEnviada)
        {
            Console.WriteLine("(Jukebox) Verificando se a mensagem foi enviada no canal correto");

            if (mensagemEnviada != channelMusic)
            {
                await mensagemEnviada.SendMessageAsync($"Infelizmente só posso responder à comandos de música no canal {channelMusic.Mention}.\nTente novamente lá");
                Console.WriteLine("(Jukebox) Canal incorreto");
                return false;
            }
            else
            {
                Console.WriteLine("(Jukebox) Canal correto");
                return true;
            }
        }

        // Retorna verdadeiro se estiver no mesmo canal de voice que o bot
        public async Task<bool> VerifyUsage(DiscordChannel canalDeVozDoPedinte, DiscordChannel canalDeTexto)
        {
            if (channelConnectedVC != null && channelConnectedVC != canalDeVozDoPedinte)
            {
                await canalDeTexto.SendMessageAsync($"Já estou sendo usado em outro canal de voz {channelConnectedVC.Mention}");
                Console.WriteLine("(Jukebox) Bot já está sendo usado em outro canal");
                return false;
            }

            return true;
        }

        // Retorna verdadeiro se conectou no canal de voz do pedinte
        public async Task<bool> ConnectToVoice(DiscordMember pedinte, DiscordChannel canalDeVoz, DiscordChannel canalDeTexto)
        {
            Console.WriteLine("(Jukebox) Conectando no canal de voz");

            if (!await VerifyUsage(canalDeVoz, canalDeTexto))
            {                
                return false;
            }

            // Se o bot já estiver conectado
            if (lavalinkPlayback != null)
            {
                return true;
            }

            // Conecta em algum endpoint
            if (connectedEndpoint == null)
            {
                bool tmp = await Connect();

                if (!tmp)
                {
                    await canalDeTexto.SendMessageAsync("Nenhum host de lavalink está respondendo. Contate um administrador.");   
                    return false;
                }
            }

            // Conecta no canal de voz do pedinte
            await lavalinkNode.ConnectAsync(canalDeVoz);            

            if (lavalinkPlayback == null)
            {
                await canalDeTexto.SendMessageAsync("Falha ao conectar no canal");
                Console.WriteLine("(Jukebox) Bot não conseguiu se conectar no canal");
                return false;
            }
            
            timeoutMs = Program.config.Jukebox_Timeout;

            // Playback events
            lavalinkPlayback.PlaybackFinished += LavalinkPlayback_PlaybackFinished;
            lavalinkPlayback.TrackException += LavalinkPlayback_TrackException;
            lavalinkPlayback.DiscordWebSocketClosed += LavalinkPlayback_DiscordWebSocketClosed;
            lavalinkPlayback.TrackStuck += LavalinkPlayback_TrackStuck;                        

            return true;
        }        

        // Retorna a primeira correspondência de música, se achar uma.
        public async Task<LavalinkTrack> FetchTrack(string link, DiscordChannel canalDeTexto)
        {
            Console.WriteLine($"(Jukebox) Procurando música {link}");

            LavalinkLoadResult busca = await lavalinkNode.Rest.GetTracksAsync(link, LavalinkSearchType.Plain);

            if (busca.LoadResultType == LavalinkLoadResultType.NoMatches || busca.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                busca = await lavalinkNode.Rest.GetTracksAsync(link, LavalinkSearchType.Youtube);

                if (busca.LoadResultType == LavalinkLoadResultType.NoMatches || busca.LoadResultType == LavalinkLoadResultType.LoadFailed)
                {
                    busca = await lavalinkNode.Rest.GetTracksAsync(link, LavalinkSearchType.SoundCloud);
                }
            }

            if (busca.LoadResultType == LavalinkLoadResultType.NoMatches || busca.LoadResultType == LavalinkLoadResultType.LoadFailed)
            {
                await canalDeTexto.SendMessageAsync("Musica não encontrada");
                Console.WriteLine($"(Jukebox) Música não encontrada");
                return null;
            }

            // Não foram encontradas músicas
            if (busca.Tracks.Count() == 0)
            {
                Console.WriteLine("$(Jukebox) Não forame encontradas músicas. Tocando never gonna give you up de placeholder");
                return lavalinkNode.Rest.GetTracksAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ").Result.Tracks.First(); // Rickroll como placeholder
            }
            else
            {
                Console.WriteLine($"(Jukebox) Música encontrada: {busca.Tracks.First().Title}");
            }
                
            return busca.Tracks.First();
        }

        // Retorna um embed da música que foi adicionada à fila ou irá tocar em breve
        public DiscordEmbed TrackEmbed(string header, LavalinkTrack track)
        {
            DiscordEmbedBuilder embed = new DiscordEmbedBuilder()
            {
                Color = DiscordColor.Violet,
                Title = header,
                Description = $"{track.Title} ({PrintTimeSpan(track.Length)})\n{track.Author}\nFonte: {track.Uri.Host}"
            };

            if (track.Uri.Host == "www.youtube.com")
            {
                embed.ImageUrl = $"https://img.youtube.com/vi/{track.Uri.AbsoluteUri.Substring(track.Uri.AbsoluteUri.IndexOf('=') + 1)}/0.jpg";
            }

            return embed.Build();
        }



        public async Task LavalinkPlayback_PlaybackFinished(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackFinishEventArgs args)
        {
            Console.WriteLine("(Jukebox) Música terminada");            
            songCurrent = null;
            await PlayNext(channelMusic);
        }

        private async Task LavalinkPlayback_TrackException(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackExceptionEventArgs args)
        {
            Console.WriteLine($"(Jukebox) Track Exception, {args.Error}");
            await channelMusic.SendMessageAsync($"Não foi possível tocar essa música ({args.Error})");
            songCurrent = null;
            await PlayNext(channelMusic);
            return;
        }

        private async Task LavalinkPlayback_TrackStuck(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.TrackStuckEventArgs args)
        {
            Console.WriteLine("$(Jukebox) Track Stuck");            
            await channelMusic.SendMessageAsync("Infelizmente não foi possível continuar tocando essa música. Tocando a próxima...");
            songCurrent = null;
            await PlayNext(channelMusic);
            return;
        }

        private async Task LavalinkPlayback_DiscordWebSocketClosed(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.WebSocketCloseEventArgs args)
        {
            Console.WriteLine($"(Jukebox) Lavalink Web Socket Closed (Code: {args.Code}), {args.Reason}");

            if (sender.IsConnected)
            {
                await RestoreConnection();
            }            

            return;
        }

        private async Task LavalinkNode_GuildConnectionRemoved(LavalinkGuildConnection sender, DSharpPlus.Lavalink.EventArgs.GuildConnectionRemovedEventArgs args)
        {
            Console.WriteLine("(Jukebox) Bot disconnected");                        
            await DisconnectAndReset();
            return;
        }


        public async Task DisconnectAndReset()
        {
            // Para as execuções de músicas
            songPaused = false;            
            songCurrent = null;
            songQueue.Clear();
            
            if (lavalinkPlayback != null)
            {
                // Retira eventos de playback
                lavalinkPlayback.PlaybackFinished -= LavalinkPlayback_PlaybackFinished;
                lavalinkPlayback.TrackException -= LavalinkPlayback_TrackException;
                lavalinkPlayback.DiscordWebSocketClosed -= LavalinkPlayback_DiscordWebSocketClosed;
                lavalinkPlayback.TrackStuck -= LavalinkPlayback_TrackStuck;

                // Disconecta da call
                await lavalinkPlayback.DisconnectAsync();
            }

            // Reseta o endpoint
            await lavalinkNode.StopAsync();
            connectedEndpoint = null;
            lavalinkNode.GuildConnectionRemoved -= LavalinkNode_GuildConnectionRemoved;            
        }

        public static string PrintTimeSpan(TimeSpan span) => Program.PrintTimeSpan(span);


        public async Task Play(DiscordMember pedinte, DiscordChannel canalDeVoz, DiscordChannel canalDeTexto, string link)
        {
            Console.WriteLine("(Jukebox) Recebido pedido para tocar música");

            // Verificar whitelist             
            if (!await VerifyWhitelist(canalDeTexto))
                return;

            // Tentar conectar no canal
            if (!await ConnectToVoice(pedinte, canalDeVoz, canalDeTexto))
                return;

            // Buscando música, por url, youtube e soundcloud.
            LavalinkTrack buscaSong = await FetchTrack(link, canalDeTexto);
            if (buscaSong == null)
                return;
                
            // Tocar música ou adicionar na fila
            if (songCurrent == null)
            {
                songCurrent = buscaSong;

                await lavalinkPlayback.PlayAsync(songCurrent);
                await canalDeTexto.SendMessageAsync(TrackEmbed("Tocando agora", buscaSong));                
                await canalDeTexto.SendMessageAsync(Program.GetTaskDoneMessage());

                Console.WriteLine($"(Jukebox) Tocando agora: {buscaSong.Title}");
            }
            else
            {
                songQueue.Add(buscaSong);

                await canalDeTexto.SendMessageAsync(TrackEmbed("Adicionado à fila", buscaSong));
                await canalDeTexto.SendMessageAsync(Program.GetTaskDoneMessage());

                Console.WriteLine($"(Jukebox) Adicionado à fila: {buscaSong.Title}");
            }

        }

        public async Task Stop(DiscordMember pedinte, DiscordChannel canalDeVoz, DiscordChannel canalDeTexto)
        {
            Console.WriteLine("(Jukebox) Recebido pedido para parar de tocar música");

            // Verificar whitelist             
            if (!await VerifyWhitelist(canalDeTexto))
                return;

            // Verificar uso
            if (!await VerifyUsage(canalDeVoz, canalDeTexto))
                return;

            await DisconnectAndReset();
            await canalDeTexto.SendMessageAsync("Desligando a jukebox");            

            Console.WriteLine("(Jukebox) Parando de tocar e desconectando do canal");
        }

        public async Task PlayNext(DiscordChannel canalDeTexto)
        {
            if (IsConnected && ThereIsQueue && songCurrent == null)
            {
                Console.WriteLine($"(Jukebox) Tocando próxima música da fila");

                songCurrent = songQueue.First();                

                await lavalinkPlayback.PlayAsync(songCurrent);
                await canalDeTexto.SendMessageAsync(TrackEmbed("Tocando agora", songCurrent));                

                Console.WriteLine($"(Jukebox) Tocando agora: {songCurrent.Title}");                

                songQueue.Remove(songCurrent);
            }
            else
            {
                Console.WriteLine($"(Jukebox) Fila de músicas vazia");
                await canalDeTexto.SendMessageAsync("A fila de música está vazia");
            }
        }        

        public async Task Pause(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            if (songPaused)
            {
                Console.WriteLine($"(Jukebox) Despausando player");
                songPaused = false;
                await lavalinkPlayback.ResumeAsync();
                await canalDeTexto.SendMessageAsync("Player rodando");
            }
            else
            {
                Console.WriteLine($"(Jukebox) Pausando player");                
                songPaused = true;
                await lavalinkPlayback.PauseAsync();
                await canalDeTexto.SendMessageAsync("Player pausado");
            }
        }

        public async Task Skip(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            Console.WriteLine($"(Jukebox) Pulando Música");

            await lavalinkPlayback.StopAsync();            

            await canalDeTexto.SendMessageAsync("Pulando música");            
        }

        // pula 10s na música
        public async Task Jump10(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            Console.WriteLine($"(Jukebox) Pulando 10s do player");

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync("Pulando 10 segundos...");
            }            

            await lavalinkPlayback.PauseAsync();
            songPaused = true;

            var timeSpan = lavalinkPlayback.CurrentState.PlaybackPosition.Add(new TimeSpan(0, 0, 10));
            await lavalinkPlayback.SeekAsync(timeSpan);

            if (timeSpan.Ticks >= songCurrent.Length.Ticks)
            {
                await lavalinkPlayback.StopAsync();
                Console.WriteLine($"(Jukebox) Música terminou com o pulo");
            }

            await lavalinkPlayback.ResumeAsync();
            songPaused = false;            
        }

        // volta 10s na música
        public async Task Back10(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            Console.WriteLine($"(Jukebox) Voltando 10 segundos no player");

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync("Voltando 10 segundos");
            }            

            await lavalinkPlayback.PauseAsync();
            songPaused = true;

            var timeSpan = lavalinkPlayback.CurrentState.PlaybackPosition.Subtract(new TimeSpan(0, 0, 10));

            if (lavalinkPlayback.CurrentState.PlaybackPosition.Ticks <= 0)
            {
                timeSpan = new TimeSpan(0, 0, 0);
            }

            await lavalinkPlayback.SeekAsync(timeSpan);

            await lavalinkPlayback.ResumeAsync();
            songPaused = false;
        }

        // define o momento da música 
        public async Task Seek(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto, TimeSpan timeSpan, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            // Pular música se avançar mais que o tamanho da música
            if (timeSpan.Ticks >= songCurrent.Length.Ticks)
            {
                await lavalinkPlayback.StopAsync();
                return;
            }

            // Reiniciar música se timeSpan foi negativo
            if (timeSpan.Ticks <= 0)
            {
                await lavalinkPlayback.SeekAsync(new TimeSpan(0, 0, 0));
                return;
            }

            Console.WriteLine($"(Jukebox) Player setado para {PrintTimeSpan(timeSpan)}");

            await lavalinkPlayback.SeekAsync(timeSpan);

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync($"Pulando para ({PrintTimeSpan(timeSpan)})");
            }
        }

        // randomiza a lista
        public async Task Shuffle(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            Console.WriteLine($"(Jukebox) Embaralhando Queue");

            Random rng = new();

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync("Embaralhando fila da jukebox...");
            }

            await lavalinkPlayback.PauseAsync();
            songPaused = true;
            
            List<LavalinkTrack> newQueue = new();

            int originalAmount = songQueue.Count;

            for (int i = 0; i < originalAmount; i++)
            {
                int tmp = rng.Next(songQueue.Count);
                var track = songQueue[tmp];
                songQueue.RemoveAt(tmp);
                newQueue.Add(track);
            }

            songQueue = new(newQueue);

            await lavalinkPlayback.ResumeAsync();
            songPaused = false;

            Console.WriteLine($"(Jukebox) Queue embaralhada");

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync("Fila embaralhada!");
                await canalDeTexto.SendMessageAsync(Program.GetTaskDoneMessage());
            }
        }

        // reinicia a música
        public async Task Restart(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTexto, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTexto) || !await VerifyUsage(canalDeVozPedinte, canalDeTexto))
                return;

            Console.WriteLine($"(Jukebox) Player setado para o início");

            if (sendFeedback)
            {
                await canalDeTexto.SendMessageAsync($"Reiniciando música ");
            }

            await lavalinkPlayback.SeekAsync(new TimeSpan(0, 0, 0));
        }

        // Mostra a fila
        public async Task QueueShow(DiscordChannel canalDeTextoPedinte)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte))
                return;

            Console.WriteLine("(Jukebox) Mostrando fila de músicas");

            DiscordEmbedBuilder embed = new DiscordEmbedBuilder();

            embed.Title = "Fila da Jukebox";
            embed.Color = DiscordColor.Violet;

            // tocando agora
            if (songCurrent == null)
            {
                embed.Description = $"Nenhuma música tocando no momento";
            }
            else
            {
                embed.Description = $"**Tocando agora:**\n{songCurrent.Title}\n{PrintTimeSpan(lavalinkPlayback.CurrentState.PlaybackPosition)}/{PrintTimeSpan(songCurrent.Length)}";
            }

            // fila
            if (!ThereIsQueue)
            {
                embed.AddField("Próximas Músicas:", "Nenhuma música na fila");
            }
            else
            {
                string fila = "";

                int index = 0;
                foreach (var song in songQueue)
                {
                    fila += $"**{index}:** {song.Title} ({PrintTimeSpan(song.Length)})\n";
                    index++;
                }

                embed.AddField("Próximas Músicas:", fila);
            }

            await canalDeTextoPedinte.SendMessageAsync(embed.Build());
            await canalDeTextoPedinte.SendMessageAsync(Program.GetTaskDoneMessage());
        }

        // Remove uma música da fila por índice
        public async Task QueueRemove(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTextoPedinte, int index, bool showFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte) || !await VerifyUsage(canalDeVozPedinte, canalDeTextoPedinte))
                return;

            if (index < 0 || index > songQueue.Count)
            {
                await canalDeTextoPedinte.SendMessageAsync("Uma música com esse índice não existe na fila");
                return;
            }

            Console.WriteLine($"(Jukebox) Removendo música {index} da fila ({songQueue[index].Title})");
            LavalinkTrack track = songQueue[index];
            songQueue.Remove(track);

            if (showFeedback)
            {
                await canalDeTextoPedinte.SendMessageAsync(Program.GetTaskDoneMessage());
                await canalDeTextoPedinte.SendMessageAsync($"{track.Title} foi removido da lista da jukebox");
            }
        }

        // Pula até o índice X da fila
        public async Task QueueSkipTo(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTextoPedinte, int index, bool showFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte) || !await VerifyUsage(canalDeVozPedinte, canalDeTextoPedinte))
                return;

            Console.WriteLine($"(Jukebox) Pulando para à música {index} da fila");

            if (index < 0 || index > songQueue.Count)
            {
                Console.WriteLine($"(Jukebox) Índice inválido");
                await canalDeTextoPedinte.SendMessageAsync("Uma música com esse índice não existe na fila");
                return;
            }

            await lavalinkPlayback.PauseAsync();
            songPaused = true;

            if (showFeedback)
            {
                await canalDeTextoPedinte.SendMessageAsync(Program.GetTaskDoneMessage());
                await canalDeTextoPedinte.SendMessageAsync($"Pulando até a música {songQueue[index].Title}");                
            }

            Console.WriteLine($"(Jukebox) Pulando até {songQueue[index].Title}");

            for (int i = 0; i <= index - 1; i++)
            {
                songQueue.RemoveAt(i);
            }

            await lavalinkPlayback.StopAsync(); // Pula a música

            await lavalinkPlayback.ResumeAsync();
            songPaused = false;
        }

        // Joga a música índice X da fila até o início
        public async Task QueuePriorityNext(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTextoPedinte, int index, bool sendFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte) || !await VerifyUsage(canalDeVozPedinte, canalDeTextoPedinte))
                return;

            Console.WriteLine($"(Jukebox) Selecionando próxima música");

            if (index < 0 || index > songQueue.Count)
            {
                Console.WriteLine($"(Jukebox) Índice inválido");
                await canalDeTextoPedinte.SendMessageAsync("Uma música com esse índice não existe na fila");
                return;
            }

            Console.WriteLine($"(Jukebox) {songQueue[index]} será a próxima música");

            // pausa o playback para evitar problemas
            await lavalinkPlayback.PauseAsync();
            songPaused = true;

            LavalinkTrack track = songQueue[index]; // pega a música que deve ser feita a próxima
            List<LavalinkTrack> copy = new(songQueue); // Copia as músicas para uma Lista reserva

            copy.Remove(track); // Retira a música da fila reserva

            // Cria uma nova fila com a música escolhida sendo a primeira da fila
            songQueue.Clear();
            songQueue.Add(track);

            foreach (var song in copy)
            {
                songQueue.Add(song);
            }

            // despausa o playback
            await lavalinkPlayback.ResumeAsync();
            songPaused = false;

            // feedback
            if (sendFeedback == false)
                return;

            await canalDeTextoPedinte.SendMessageAsync(Program.GetTaskDoneMessage());
            await canalDeTextoPedinte.SendMessageAsync($"{track.Title} será a próxima música a ser tocada");
        }

        // Joga a música índice X da fila até o início, pula a música atual
        public async Task QueuePriorityPlay(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTextoPedinte, int index, bool showFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte) || !await VerifyUsage(canalDeVozPedinte, canalDeTextoPedinte))
                return;

            Console.WriteLine($"(Jukebox) Priority play");

            if (index < 0 || index > songQueue.Count)
            {
                Console.WriteLine($"(Jukebox) Índice inválido");
                await canalDeTextoPedinte.SendMessageAsync("Uma música com esse índice não existe na fila");
                return;
            }

            await QueuePriorityNext(canalDeVozPedinte, canalDeTextoPedinte, index, false);

            if (showFeedback)
            {
                await canalDeTextoPedinte.SendMessageAsync(Program.GetTaskDoneMessage());
                await canalDeTextoPedinte.SendMessageAsync($"Tocando agora a música {songQueue.First().Title} da fila");
                Console.WriteLine($"(Jukebox) Tocando agora: {songQueue.First().Title}");
            }

            await lavalinkPlayback.StopAsync();
        }

        // Limpa a fila
        public async Task QueueClear(DiscordChannel canalDeVozPedinte, DiscordChannel canalDeTextoPedinte, bool showFeedback = true)
        {
            if (!IsConnected)
                return;

            if (!await VerifyWhitelist(canalDeTextoPedinte) || !await VerifyUsage(canalDeVozPedinte, canalDeTextoPedinte))
                return;

            Console.WriteLine($"(Jukebox) Limpando fila");

            songQueue.Clear();
            await canalDeTextoPedinte.SendMessageAsync("Fila da Jukebox limpa");
        }


        public class JukeboxEndpoint(string Hostname, int Port, bool ssh, string key)
        {
            public string Hostname { get; set; } = Hostname;    
            public int Port { get; set; } = Port;
            public bool SSH { get; set; } = ssh;
            public string Password { get; set; } = key;

            public ConnectionEndpoint ToConnectionEndpoint()
            {
                return new ConnectionEndpoint()
                {
                    Hostname = Hostname,
                    Port = Port,
                    Secured = SSH
                };
            }
        }

    }    
}