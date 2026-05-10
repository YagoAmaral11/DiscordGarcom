using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using GarçomDoKitts.Containers.Core;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GarçomDoKitts.GarcModules;

[Command("Frases")]
public class Frases(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : IModule
{
    // Config
    private FrasesConfig config;

    // Dependencies
    private Random rng;
    private IServiceProvider services;
    private IPersistance persistance = persistance;
    private IConfigPersistance configPersistance = configPersistance;
    private IScheduler scheduler = scheduler;
    private IServerContext serverContext; // Qual servidor do Discord que o módulo está rodando, usado para pegar informações do servidor

    // Cached Channels
    private DiscordChannel origin; // Canal de onde as frases serão coletadas
    private DiscordChannel broadcast; // Canal onde as frases serão enviadas        

    // Data
    private FrasesData data;
    private List<ulong> cachedMessageIDs = new(); // A lista com os IDs das mensagens em cache
    private DiscordMessage daily; // Mensagem diária atual        

    // Misc
    private bool ready = false; // Se o módulo está pronto para receber comandos
    private CommandBuilder moduleCB = new(); // Usado para registrar os comandos do módulo


    [JsonIgnore] public string Name => "Frases";


    public List<Type> GetStaticCommands() => [];
    public IEnumerable<CommandBuilder> GetDynamicCommands()
    {
        moduleCB = new CommandBuilder().WithName("Frases");
        // moduleCB.WithDelegate(); TODO: Fazer com que o comando "frases" mostre ajuda ou algo do tipo

        var RandomMessageCB = CommandBuilder.From(RandomMessage).WithParent(moduleCB).WithDescription("Mostra uma frase aleatória do canal de frases");
        var DailyMessageCB = CommandBuilder.From(ResendDaily).WithParent(moduleCB).WithDescription("Mostra a frase diária atual");

        moduleCB.WithSubcommands([RandomMessageCB, DailyMessageCB]);
        
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
        config = new();

        if (await configPersistance.ConfigExists(this))
        {
            // Carrega a configuração existente
            await LoadConfig();            
        }
        else
        {
            // Cria uma configuração inicial
            await configPersistance.WriteConfig(this, config);
            throw new Exception(mod.LogName + " config not found. Please modify the standard one.");
        }

        await LoadData();        

        return true;
    }

    public async Task Start()
    {
        ready = false;
        origin = await serverContext.BindedDiscordServer.GetChannelAsync(config.OriginChannelID);
        broadcast = await serverContext.BindedDiscordServer.GetChannelAsync(config.BroadcastChannelID);

        if (data is not null)
            daily = await origin.GetMessageAsync(data.DailyID);

        await Fetch();

        // Inicializa agendamentos        
        SemanalRepeatDay[] semanalRepeatDays = new SemanalRepeatDay[7];
        for (int i = 0; i < 7; i++)
        {
            semanalRepeatDays[i] = new SemanalRepeatDay((DayOfWeek) i, new TimeSpan(config.DailyTime.Ticks), config.TimeZone);
        }

        scheduler.ScheduleRepeatSemanal(new Func<Task>(DailyMessage), null, 0, semanalRepeatDays);

        ready = true;
    }


    public Task ConfigureEventHandlers(EventHandlingBuilder ehb)
    {
        ehb.HandleMessageCreated(MessageCreated);
        ehb.HandleMessageDeleted(MessageDeleted);
        return Task.CompletedTask;
    }

    private async Task LoadConfig()
    {
        FrasesConfig loadedConfig = await configPersistance.LoadConfig(this, typeof(FrasesConfig)) as FrasesConfig;
        config = loadedConfig;
    }

    public async Task<bool> SaveData()
    {
        await persistance.WriteObject(data, typeof(FrasesData), Name + "Data");
        return true;
    }

    public async Task LoadData()
    {
        if (await persistance.KeyExists(Name + "Data" + ".json"))
        {
            FrasesData loadedData = await persistance.ReadObject(Name + "Data", typeof(FrasesData)) as FrasesData;
            data = loadedData;
        }
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


    // Seleciona e envia uma mensagem aleatória do canal de origem como resposta para o comando
    [Command("Aleatoria")]
    public async Task RandomMessage(CommandContext context)
    {
        if (context.Guild != serverContext.BindedDiscordServer || !ready)
            return;

        ulong messageID = ChooseRandomMessage();
        DiscordMessage msg = await origin.GetMessageAsync(messageID);
        DiscordEmbed embed = EmbedBuilder(msg.Content, msg.Author, msg.Timestamp, config.RandomEmbedTitle, config.RandomEmbedColorHex, msg.JumpLink.ToString());
        await context.RespondAsync(embed);
    }

    // Reenvia a mensagem diária atual como resposta para o comando
    [Command("Diaria")]
    public async Task ResendDaily(CommandContext context)
    {
        if (daily != null)
        {
            await context.RespondAsync(CreateDailyMessageToSend(daily));
        }
        else
        {
            await context.RespondAsync("Muito cedo chefe!\nA mensagem diária não foi escolhida ainda");
        }
    }


    // Pega todas as mensagens do canal de origem
    // Realiza o cache, salvando os IDs em uma lista
    // TODO: Verificar se ainda dá problema de ratelimits e melhorar o logging
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

        await Task.Delay(config.DefaultDelayMs);

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
                await Task.Delay(config.ErrorDelayMs);
            }

            if (oldAnchorId == anchorMessageID && !errorOcorred)
            {
                continueFetching = false;
                break;
            }

            await Task.Delay(config.DefaultDelayMs);
        }                  
        
        cachedMessageIDs = messageIDs;
    }

    // Retorna verdadeiro se a mensagem é realmente uma frase
    // TODO: Melhorar o filtro; Algumas frases ainda não são capturadas, principalmente aquelas que que só tem uma aspas (typos)
    private static bool FilterMessage(DiscordMessage msg)
    {        
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

    // Seleciona e envia uma mensagem diária aleatória do canal de origem para o canal de broadcast
    public async Task DailyMessage()
    {
        if (!config.DoDaily)
            return;

        ulong messageID = ChooseRandomMessage();
        DiscordMessage msg = await origin.GetMessageAsync(messageID);

        daily = msg;        
        data.DailyID = msg.Id;

        await broadcast.SendMessageAsync(CreateDailyMessageToSend(daily));
    }
   

    // Constrói um embed de frase padrão
    private DiscordEmbed CreateDailyMessageToSend(DiscordMessage msg) => EmbedBuilder(msg.Content, msg.Author, msg.Timestamp, config.DailyEmbedTitle, config.DailyEmbedColorHex, msg.JumpLink.ToString());    
    private DiscordEmbed EmbedBuilder(string content, DiscordUser Writer, DateTimeOffset MessageTimestamp, string EmbedTitle = null, string HexColor = null, string MessageUrl = null)
    {
        if (EmbedTitle is null)
            EmbedTitle = config.DailyEmbedTitle;

        if (HexColor is null)
            HexColor = config.StandardEmbedColorHex;

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


}

public class FrasesData
{
    [JsonInclude] public ulong DailyID { get; set; } // O ID da mensagem diária atual
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
    [JsonInclude] public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Local; // Fuso horário para o horário da mensagem diária

    // Config Data (Embeds e Mensagens)
    [JsonInclude] public bool DoMesageLinkBtn { get; private set; } = true; // A cada frase enviada, adicionar um botão com o link para a mensagem original
    [JsonInclude] public string StandardEmbedColorHex { get; private set; } = "d619bd"; // Cor padrão dos embeds (em hexadecimal)
    [JsonInclude] public string DailyEmbedTitle { get; private set; } = "Frase do Dia"; // Título do embed da mensagem diária
    [JsonInclude] public string DailyEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem diária (em hexadecimal)
    [JsonInclude] public string RandomEmbedTitle { get; private set; } = "Frase Aleatória"; // Título do embed da mensagem aleatória
    [JsonInclude] public string RandomEmbedColorHex { get; private set; } = "d619bd"; // Cor do embed da mensagem aleatória (em hexadecimal)
}