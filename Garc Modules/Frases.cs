using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using GarçomDoKitts.Shell.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GarçomDoKitts.GarcModules;

[Command("Frases")]
public class Frases(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : IModule
{
    // TODO: Alterar a forma de persistir as configs e os dados; Criar uma classe própria em cada módulo para isso e serializar ela.
    // Config Data (Permissões/Uso)
    [JsonInclude] public bool DoDaily { get; private set; } = true; // Ativar mensagem diária
    [JsonInclude] public bool AllowRandomCmd { get; private set; } = true; // Ativar comando frase aleatória
    [JsonInclude] public bool AllowDailyCmd { get; private set; } = true; // Ativar comando para mostrar a frase diária atual    

    // Config Data (Canais e Timings)
    [JsonInclude] public ulong OriginChannelID { get; private set; } // Canal de onde as frases serão coletadas
    [JsonInclude] public ulong BroadcastChannelID { get; private set; } // Canal onde as frases serão enviadas
    [JsonInclude] public int DefaultDelayMs { get; private set; } = 1000; // Quanto tempo cada requisição aguarda no fetching de mensagens
    [JsonInclude] public int ErrorDelayMs { get; private set; } = 2000; // Quanto tempo o módulo aguarda caso exista um problema no fetching das mensagens, um ratelimit por exemplo.
    [JsonInclude] public TimeOnly DailyTime { get; private set; } = new(12, 0); // Horário da mensagem diária

    // Config Data (Embeds e Mensagens)
    [JsonInclude] public bool DoMesageLinkBtn { get; private set; } = true; // A cada frase enviada, adicionar um botão com o link para a mensagem original
    [JsonInclude] public string StandardEmbedColorHex { get; private set; } = "d619bd"; // Cor padrão dos embeds (em hexadecimal)
    [JsonInclude] public string DailyEmbedTitle { get; private set; } = "Frase do Dia"; // Título do embed da mensagem diária
    [JsonInclude] public string DailyEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem diária (em hexadecimal)
    [JsonInclude] public string RandomEmbedTitle { get; private set; } = "Frase Aleatória"; // Título do embed da mensagem aleatória
    [JsonInclude] public string RandomEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem aleatória (em hexadecimal)

    // Runtime Data
    private Random rng;
    private IServiceProvider services;
    private IPersistance persistance = persistance;
    private IConfigPersistance configPersistance = configPersistance;
    private IScheduler scheduler = scheduler;

    private DiscordChannel origin; // Canal de onde as frases serão coletadas
    private DiscordChannel broadcast; // Canal onde as frases serão enviadas    
    private IServerContext serverContext;
    
    private List<ulong> cachedMessageIDs = new(); // A lista com os IDs das mensagens em cache
    private DiscordMessage daily; // Mensagem diária atual    
    private CommandBuilder moduleCB = new();
    private bool ready = false; // Se o módulo está pronto para receber comandos



    [JsonIgnore] public string Name => "Frases";

    // TODO: Depois remover isso; só existe para que seja possível serializar essa classe, mas esse construtor nao deve existir
    public Frases() : this(null, null, null) {}

    public List<Type> GetStaticCommands() => [];
    public IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        moduleCB = new CommandBuilder().WithName("Frases");
        // moduleCB.WithDelegate(); TODO: Fazer com que o comando "frases" mostre ajuda ou algo do tipo

        var RandomMessageCB = CommandBuilder.From(RandomMessage).WithParent(moduleCB).WithDescription("Mostra uma frase aleatória do canal de frases");

        moduleCB.WithSubcommands([RandomMessageCB]);
        
        return [moduleCB];
    }


    public async Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {
        IModule mod = this;

        if (persistance == null)
            throw new Exception(mod.LogName + "IPersistance is not assigned to the module");

        if (configPersistance == null)
            throw new Exception(mod.LogName + "IConfigPersistance is not assigned to the module");

        rng = new();
        this.serverContext = serverContext;
        services = serviceProvider;

        if (await configPersistance.ConfigExists(this))
        {
            // Carrega a configuração existente
            Frases loadedConfig = await configPersistance.LoadConfig(this) as Frases;
            LoadConfig(loadedConfig);
        }
        else
        {
            // Cria uma configuração inicial
            await configPersistance.WriteConfig(this);
            throw new Exception(mod.LogName + " config not found. Please modify the standard one.");
        }

        return true;
    }

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        ehb.HandleMessageCreated(MessageCreated);
        ehb.HandleMessageDeleted(MessageDeleted);
        return Task.CompletedTask;
    }

    private Task MessageCreated(DiscordClient client, MessageCreatedEventArgs args)
    {
        if (args.Channel.Id != origin.Id)
            return Task.CompletedTask;

        ulong newMessageId = args.Message.Id;
        if (FilterMessage(args.Message))
            cachedMessageIDs.Add(newMessageId);

        return Task.CompletedTask;
    }

    private Task MessageDeleted(DiscordClient client, MessageDeletedEventArgs args)
    {
        if (args.Channel.Id != origin.Id)
            return Task.CompletedTask;

        ulong deletedMessageId = args.Message.Id;

        cachedMessageIDs.Remove(deletedMessageId);

        return Task.CompletedTask;
    }

    private void LoadConfig(Frases loadedConfigs)
    {
        DoDaily = loadedConfigs.DoDaily;
        AllowRandomCmd = loadedConfigs.AllowRandomCmd;
        AllowDailyCmd = loadedConfigs.AllowDailyCmd;
        OriginChannelID = loadedConfigs.OriginChannelID;
        BroadcastChannelID = loadedConfigs.BroadcastChannelID;
        DefaultDelayMs = loadedConfigs.DefaultDelayMs;
        ErrorDelayMs = loadedConfigs.ErrorDelayMs;
        DailyTime = loadedConfigs.DailyTime;
        DoMesageLinkBtn = loadedConfigs.DoMesageLinkBtn;
        StandardEmbedColorHex = loadedConfigs.StandardEmbedColorHex;
        DailyEmbedTitle = loadedConfigs.DailyEmbedTitle;
        DailyEmbedColorHex = loadedConfigs.DailyEmbedColorHex;
        RandomEmbedTitle = loadedConfigs.RandomEmbedTitle;
        RandomEmbedColorHex = loadedConfigs.RandomEmbedColorHex;
    }


    public async Task Start()
    {
        ready = false;
        origin = await serverContext.BindedDiscordServer.GetChannelAsync(OriginChannelID);
        broadcast = await serverContext.BindedDiscordServer.GetChannelAsync(BroadcastChannelID);

        await Fetch();

        // Inicializa agendamentos
        SemanalRepeatDay[] semanalRepeatDays = new SemanalRepeatDay[7];
        for (int i = 0; i < 7; i++)
        {
            // TODO: Deve ser criado uma forma de informar o fuso horário; No momento, fica refém ao fuso horário do servidor            
            semanalRepeatDays[i] = new SemanalRepeatDay((DayOfWeek) i, new TimeSpan(DailyTime.Ticks));
        }

        scheduler.ScheduleRepeatSemanal(new Func<Task>(DailyMessage), null, 0, semanalRepeatDays);

        ready = true;
    }


    // Pega todas as mensagens do canal de origem
    // Realiza o cache, salvando os IDs em uma lista
    private async Task Fetch()
    {
        // TODO: Solucionar problemas de ratelimits (?) acontecem por algum motivo
        // Talvez pq as primeiras mensagem são buscadas duas vezes? melhorar essa parte do código
        List<ulong> messageIDs = new();
        List<ulong> filteredMessageIDs = new();        
        bool continueFetching = true;
        ulong anchorMessageID = 0;
        uint fetchCount = 200;
        bool encounteredInitial = false;
        bool hasMessages = false;

        // Seleciona uma mensagem inicial para usar como âncora (para puxar as mensagens anteriores à essa)
        await foreach (DiscordMessage msg in origin.GetMessagesAsync())
        {
            hasMessages = true;
            anchorMessageID = msg.Id;

            if (FilterMessage(msg))
            {                
                encounteredInitial = true;
                break;
            }
            else
            {
                filteredMessageIDs.Add(msg.Id);
            }
        }

        if (!hasMessages)
            return;

        if (encounteredInitial)
        {
            messageIDs.Add(anchorMessageID);
        }

        await Task.Delay(DefaultDelayMs);

        // Procura e adiciona o ID de todas as mensagens na lista de mensagens encontradas; 
        // Só para quando não tiver mais mensagens no canal
        while (continueFetching)
        {
            bool errorOcorred = false;
            ulong oldAnchorId = anchorMessageID;

            try
            {
                await foreach (DiscordMessage msg in origin.GetMessagesBeforeAsync(anchorMessageID, (int) fetchCount))
                {
                    anchorMessageID = msg.Id;

                    if (FilterMessage(msg))
                    {
                        messageIDs.Add(msg.Id);
                    }
                    else
                    {
                        filteredMessageIDs.Add(msg.Id);
                    }
                }
            }
            catch (Exception e)
            {
                // TODO: Dar log no erro
                errorOcorred = true;
                await Task.Delay(ErrorDelayMs);
            }

            if (oldAnchorId == anchorMessageID && !errorOcorred)
            {
                continueFetching = false;
                break;
            }

            await Task.Delay(DefaultDelayMs);
        }                  
        
        cachedMessageIDs = messageIDs;
    }

    // Retorna verdadeiro se a mensagem é realmente uma frase
    private static bool FilterMessage(DiscordMessage msg)
    {
        // TODO: Melhorar o filtro; Algumas frases ainda não são capturadas, principalmente aquelas que que só tem uma aspas (typos)
        string content = msg.Content;
        int aspasDuplas = 0;
        int aspasSimples = 0;

        foreach (char c in content)
        {
            if (c == '\"')
            {
                aspasDuplas++;
            }
            if (c == '\'')
            {
                aspasSimples++;
            }
        }

        if (aspasDuplas >= 2 || aspasSimples >= 2)
            return true;
        return false;
    }

    // Retorna um ID de mensagem aleatório do cache
    private ulong ChooseRandomMessage()
    {
        int count = cachedMessageIDs.Count;
        int index = rng.Next(0, count);
        return cachedMessageIDs[index];
    }       


    public async Task DailyMessage()
    {
        if (!DoDaily)
            return;

        ulong messageID = ChooseRandomMessage();
        DiscordMessage msg = await origin.GetMessageAsync(messageID);
        daily = msg;        
        await broadcast.SendMessageAsync(CreateDailyMessageToSend(daily));
    }
   


    [Command("Aleatoria")]
    public async Task RandomMessage(CommandContext context)
    {
        if (context.Guild != serverContext.BindedDiscordServer || !ready)
            return;

        ulong messageID = ChooseRandomMessage();
        DiscordMessage msg = await origin.GetMessageAsync(messageID);
        DiscordEmbed embed = EmbedBuilder(msg.Content, msg.Author, msg.Timestamp, RandomEmbedTitle, RandomEmbedColorHex, msg.JumpLink.ToString());
        await context.RespondAsync(embed);
    }

    [Command("Diaria")]
    public async Task ResendDaily(CommandContext context) => await broadcast.SendMessageAsync(CreateDailyMessageToSend(daily));    


    // Constrói um embed de frase padrão
    private DiscordEmbed CreateDailyMessageToSend(DiscordMessage msg) => EmbedBuilder(msg.Content, msg.Author, msg.Timestamp, DailyEmbedTitle, DailyEmbedColorHex, msg.JumpLink.ToString());    
    private DiscordEmbed EmbedBuilder(string content, DiscordUser Writer, DateTimeOffset MessageTimestamp, string EmbedTitle = null, string HexColor = null, string MessageUrl = null)
    {
        if (EmbedTitle is null)
            EmbedTitle = DailyEmbedTitle;

        if (HexColor is null)
            HexColor = StandardEmbedColorHex;

        DiscordEmbedBuilder embed = new();

        embed.Title = EmbedTitle;
        embed.Color = new(HexColor);

        if (MessageUrl is not null)
            embed.Url = MessageUrl;

        embed.Footer = new DiscordEmbedBuilder.EmbedFooter();
        embed.Footer.Text = "Frase cunhada por " + Writer.Username;
        embed.Footer.IconUrl = Writer.AvatarUrl;
        embed.Timestamp = MessageTimestamp;
        embed.Description = content;

        return embed.Build();
    }



    public Task<bool> SaveData()
    {
        // TODO: Salvar dados de runtime do módulo, caso necessário
        return Task.FromResult(true);
    }

}

public class FrasesData
{
    [JsonInclude] public string DailyID { get; set; }
}

public class FrasesConfig
{
    // Config Data (Permissões/Uso)
    [JsonInclude] public bool DoDaily { get; private set; } = true; // Ativar mensagem diária
    [JsonInclude] public bool AllowRandomCmd { get; private set; } = true; // Ativar comando frase aleatória
    [JsonInclude] public bool AllowDailyCmd { get; private set; } = true; // Ativar comando para mostrar a frase diária atual    

    // Config Data (Canais e Timings)
    [JsonInclude] public ulong OriginChannelID { get; private set; } // Canal de onde as frases serão coletadas
    [JsonInclude] public ulong BroadcastChannelID { get; private set; } // Canal onde as frases serão enviadas
    [JsonInclude] public int DefaultDelayMs { get; private set; } = 1000; // Quanto tempo cada requisição aguarda no fetching de mensagens
    [JsonInclude] public int ErrorDelayMs { get; private set; } = 2000; // Quanto tempo o módulo aguarda caso exista um problema no fetching das mensagens, um ratelimit por exemplo.
    [JsonInclude] public TimeOnly DailyTime { get; private set; } = new(12, 0); // Horário da mensagem diária

    // Config Data (Embeds e Mensagens)
    [JsonInclude] public bool DoMesageLinkBtn { get; private set; } = true; // A cada frase enviada, adicionar um botão com o link para a mensagem original
    [JsonInclude] public string StandardEmbedColorHex { get; private set; } = "d619bd"; // Cor padrão dos embeds (em hexadecimal)
    [JsonInclude] public string DailyEmbedTitle { get; private set; } = "Frase do Dia"; // Título do embed da mensagem diária
    [JsonInclude] public string DailyEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem diária (em hexadecimal)
    [JsonInclude] public string RandomEmbedTitle { get; private set; } = "Frase Aleatória"; // Título do embed da mensagem aleatória
    [JsonInclude] public string RandomEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem aleatória (em hexadecimal)
}