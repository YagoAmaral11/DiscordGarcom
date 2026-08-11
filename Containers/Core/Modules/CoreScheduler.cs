using DiscordGarçom.Containers.IO;
using DSharpPlus;
using DSharpPlus.Commands.Trees;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

/*
 *  OBS: Ao usar o CoreScheduler, garantir com que os métodos dos módulos em callback possam ser chamados em PreStart_1, 
 *       ou registrar apenas métodos que registrem uma "fila de chamados" que sejam executados posteriormente quando o módulo esteja realmente pronto. Isto é, 
 *       o método registrar que ele foi chamado e só realmente executar sua lógica depois, com o próprio módulo que usa o CoreScheduler sendo responsável por isso.
 */

public class CoreScheduler(IPersistance persistance) : IModule, IScheduler
{
    public string Name => "Core Scheduler";

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;
    public IEnumerable<CommandBuilder> GetDynamicCommands() => [];
    public List<Type> GetStaticCommands() => [];    

    private TimeSpan MaxDelayLength = TimeSpan.FromDays(1); // O tempo máximo que uma Task de Delay pode aceitar; Não afeta o agendamento;

    private IServerContext serverContext;
    private IPersistance persistance = persistance;

    private readonly object scheduledCallbackLock = new();
    private PriorityQueue<ScheduledCallback, DateTimeOffset> scheduledCallbacksQueue = new();
    private Dictionary<(ulong Id, Type ModuleType), ScheduledCallback> scheduledCallbacksDict = new();
    private Task nextTask = null;
    private CancellationTokenSource cancellationToken = new();
    


    public Task<bool> Initialize(IServerContext serverContext)
    {
        // TODO: Carregar as configurações do Scheduler        
        this.serverContext = serverContext;        
        
        return Task.FromResult(true);
    }

    public Task ReceiveServices(IServiceProvider serviceProvider) => Task.CompletedTask;    

    public async Task<bool> Shutdown()
    {
        Console.WriteLine((this as IModule).LogName + " shutting down, trying to save data");
        await cancellationToken.CancelAsync();
        if (await SaveData())
        {
            Console.WriteLine((this as IModule).LogName + " saved successfully.");
            return true;
        }
        else
        {
            Console.WriteLine((this as IModule).LogName + " failed to save data.");
            return false;
        }        
    }

    public async Task<bool> SaveData()
    {        
        JsonArray finalJson = new();

        foreach (var (Callback, Priority) in scheduledCallbacksQueue.UnorderedItems)
        {
            JsonObject serializedCallback = SerializeScheduledCallback(Callback);
            finalJson.Add(serializedCallback);
        }

        JsonNode jsonNode = finalJson;
        string json = jsonNode.ToJsonString(FileSystem.serializerOptions);
        
        await persistance.WriteJSON(json, "CoreSchedulerData");

        return true;
    }

    public async Task PreStart_1()
    {
        JsonNode callbackArray;

        if (await persistance.KeyExists("CoreSchedulerData.json"))
            callbackArray = JsonNode.Parse(await persistance.ReadJSON("CoreSchedulerData"));
        else
            callbackArray = new JsonArray([]);

        foreach (var JsonObject in callbackArray.AsArray())
        {
            ScheduledCallback callback = DeserializeScheduledCallback(JsonObject.AsObject());
            EnqueueNew(callback, false);
        }

        RunScheduler();
    }

    public Task PreStart_0() => Task.CompletedTask;

    public Task Start() => Task.CompletedTask;    


    // Agenda novos callbacks e cancelar callbacks agendados (adicionar novo callback na fila, executar o scheduler)
    // Com runScheduler = false, não executa o scheduler automaticamente; Útil para quando forem registrados vários novos Callbacks em massa
    // Assim é possível executar o Scheduler uma vez só
    private bool EnqueueNew(ScheduledCallback callback, bool runScheduler = true)
    {
        lock (scheduledCallbackLock)
        {
            if (callback.ManageID == true)
            {
                if (scheduledCallbacksDict.TryGetValue((callback.ID, callback.Owner.ModuleType), out _))
                    return false; // Callback com mesmo ID e Owner já existe, não agendar
            }

            scheduledCallbacksDict.Add((callback.ID, callback.Owner.ModuleType), callback);
            scheduledCallbacksQueue.Enqueue(callback, callback.NextExecution);

            if (runScheduler)
                RunScheduler();

            return true;
        }        
    }

    // Método scheduler, que verifica os callbacks agendados, executa os que devem ser executados até aquele momento, rearranja a E. D. e aguarda o próximo callback 
    // (verifica se o último callback expirou, executa todos os callbacks expirados da fila e remove eles do dicionário, cancela a antiga Task de delay, cria nova Task de delay com um novo cancelation token)
    private void RunScheduler()
    {        
        List<ScheduledCallback> expiredCallbacks = new();

        lock (scheduledCallbackLock)
        {
            // Cancela a última Task de Delay
            if (nextTask != null)
            {
                cancellationToken.Cancel();
                cancellationToken.Dispose();
                nextTask = null;
            }

            // Verifica possíveis callbacks expirados, e coloca em uma fila para executar eles
            while (scheduledCallbacksQueue.Count > 0 && scheduledCallbacksQueue.Peek().NextExecution <= DateTimeOffset.Now)
            {
                ScheduledCallback expiredCallback = scheduledCallbacksQueue.Dequeue();
                scheduledCallbacksDict.Remove((expiredCallback.ID, expiredCallback.Owner.ModuleType));
                expiredCallbacks.Add(expiredCallback);                
            }

            // Cria uma Task para aguardar o próximo callback
            if (scheduledCallbacksQueue.Count > 0)
            {
                ScheduledCallback nextScheduledCallback = scheduledCallbacksQueue.Peek();
                cancellationToken = new();
                nextTask = DelayUntil(nextScheduledCallback.NextExecution, cancellationToken.Token);
            }
        }

        foreach (var expired in expiredCallbacks)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await InvokeCallback(expired);
                }
                catch (Exception ex)
                {
                    await ((IModule) this).DumpException(ex, persistance);
                }
                finally
                {
                    try
                    {
                        DispatchCallback(expired);
                    }
                    catch (Exception e)
                    {
                        await ((IModule) this).DumpException(e, persistance);
                    }
                }                
            });
        }
    }


    // Método que invoca e executa o callback registrado
    private async Task InvokeCallback(ScheduledCallback callback)
    {        
        MethodInfo method = callback.MethodInfo;
        Type type = callback.Owner.ModuleType;
        object instance = serverContext.GetModule(type);

        try
        {
            object task = method.Invoke(instance, callback.Parameters);

            if (task is Task _task)
            {
                await _task;
            }
        }
        catch (Exception ex)
        {
            await ((IModule) this).DumpException(ex, persistance);            
        }
    }

    // Método que lida com o fim do agendamento e cria um novo callback de acordo com o tipo do callback, se necessário
    private void DispatchCallback(ScheduledCallback callback)
    {        
        DateTimeOffset? nextRepeatDate;

        if (callback.AllowLateRepeat)
        {
            // Se pode/deve realizar repetições de datas atrasadas e não só o último callback registrado, 
            // então usa a ultima data de execução para escolher o próximo callback, não a data atual
            // Funciona pois o DispatchCallback só é executado quando um callback está expirado, então esse callback expirado
            // já foi executado
            nextRepeatDate = callback.NextRepeatDate(useAsCurrentDateTime: callback.NextExecution, allowLateRepeat: true);
        }
        else
        {
            // Se não deve realizar repetições de datas atrasadas, então usa a data inicial para buscar próximos callbacks
            // O IntervalRepeat funciona corretamente e só seleciona a próxima data futura pois abaixo faz que o callback troque
            // sua data de NextRepeat até ela ser uma data futura, caso essa flag esteja ativa            
            nextRepeatDate = callback.NextRepeatDate();
        }

        if (nextRepeatDate == null)
            return; // Esse callback não deve se repetir

        if (callback.ManageID)
        {
            callback.NextExecution = nextRepeatDate.Value;            
            EnqueueNew(callback, true);
        }
        else
        {            
            throw new Exception("Only Managed ID Scheduled Callbacks can repeat");
        }
    }

    // Task cancelável para os callbacks agendados, que espera até o próximo callback agendado usando Task.Delay    
    private async Task DelayUntil(DateTimeOffset executionTime, CancellationToken cancellationToken)
    {
        TimeSpan delay = executionTime - DateTimeOffset.Now;
        
        if (delay <= TimeSpan.Zero)
        {
            // Medida de emergência caso o callback expire exatamente entre o RunScheduler e a criação da Task em DelayUntil.
            // Executa o Scheduler como Task em outra Thread para evitar StackOverflow caso muitos callbacks estejam com tempo mínimó ótimo para 
            // essa situação ocorrer
            // Ao executar o Scheduler novamente, garante que nenhum callback se perda e a Scheduler trave até que outro callback seja adicionado
            _ = Task.Run(() => RunScheduler(), CancellationToken.None);
            return;
        }

        if (delay > MaxDelayLength)
            delay = MaxDelayLength;

        if (delay.TotalMilliseconds > int.MaxValue)
            delay = TimeSpan.FromMilliseconds(int.MaxValue); // Task.Delay só aceita o máximo valor de int de delay

        try
        {
            await Task.Delay(delay, cancellationToken);

            RunScheduler(); // Tempo da Task passou naturalmente, fila de callbacks não foi alterada, então deve chamar o Scheduler novamente para executar
        }
        catch (OperationCanceledException opC)
        {
            // Cancelada intencionalmente pelo Scheduler, não precisa executar o Scheduler novamente pois já está sendo executado;
            // Exceção Esperada;
        }
        catch (Exception ex)
        {
            await ((IModule) this).DumpException(ex, persistance);
        }        
    }
    


    // Métodos de IScheduler, Criar métodos para criar diferentes tipos de callback, de acordo com seu tipo        
    public bool ScheduleCallback(Delegate callback, object[] parameters, ulong ID, DateTimeOffset execution, bool ManagedCallback = true, bool AllowLateRepeat = false)
    {
        ScheduledCallback newCallback = ScheduledCallback.FromTemplate(callback, parameters, ID, ManagedCallback, AllowLateRepeat);

        newCallback.ScheduleType = ScheduleType.Once;
        newCallback.NextExecution = execution;        
        
        return EnqueueNew(newCallback);
    }

    public bool ScheduleRepeatEvery(Delegate callback, object[] parameters, ulong ID, TimeSpan repeatInterval, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null)
    {
        nextExecution ??= DateTimeOffset.Now + repeatInterval;

        ScheduledCallback newCallback = ScheduledCallback.FromTemplate(callback, parameters, ID, ManagedCallback, AllowLateRepeat);
        newCallback.ScheduleType = ScheduleType.IntervalRepeat;
        newCallback.IntervalRepeat_Interval = repeatInterval;
        newCallback.NextExecution = nextExecution.Value;                

        return EnqueueNew(newCallback);
    }

    public bool ScheduleRepeatSemanal(Delegate callback, object[] parameters, ulong ID, SemanalRepeatDay[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null)
    {
        ScheduledCallback newCallback = ScheduledCallback.FromTemplate(callback, parameters, ID, ManagedCallback, AllowLateRepeat);

        newCallback.ScheduleType = ScheduleType.SemanalRepeat;
        newCallback.SemanalRepeat_Days = repeatDays;
        newCallback.NextExecution = nextExecution ?? newCallback.NextRepeatDate().Value;

        return EnqueueNew(newCallback);
    }

    public bool ScheduleRepeatMonthly(Delegate callback, object[] parameters, ulong ID, MonthlyRepeatDate[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null)
    {
        ScheduledCallback newCallback = ScheduledCallback.FromTemplate(callback, parameters, ID, ManagedCallback, AllowLateRepeat);
        newCallback.ScheduleType = ScheduleType.MonthlyRepeat;
        newCallback.MonthlyRepeat_Dates = repeatDays;
        newCallback.NextExecution = nextExecution ?? newCallback.NextRepeatDate().Value;

        return EnqueueNew(newCallback);
    }

    public bool ScheduleRepeatYearly(Delegate callback, object[] parameters, ulong ID, DateTimeOffset[] repeatDays, bool ManagedCallback = true, bool AllowLateRepeat = false, DateTimeOffset? nextExecution = null)
    {
        ScheduledCallback newCallback = ScheduledCallback.FromTemplate(callback, parameters, ID, ManagedCallback, AllowLateRepeat);
        newCallback.ScheduleType = ScheduleType.YearlyRepeat;
        newCallback.YearlyRepeat_Dates = repeatDays;
        newCallback.NextExecution = nextExecution ?? newCallback.NextRepeatDate().Value;

        return EnqueueNew(newCallback);
    }



    // Serializadores    
    public static JsonObject SerializeScheduledCallback(ScheduledCallback callback)
    {
        JsonObject serialized = new JsonObject();
        JsonValue scheduleType = JsonValue.Create<string>(callback.ScheduleType.ToString());
        JsonValue nextExecution = JsonValue.Create<DateTimeOffset>(callback.NextExecution);
        JsonValue intervalRepeat_interval = JsonValue.Create<TimeSpan?>(callback.IntervalRepeat_Interval);           

        JsonArray semanalRepeat_DaysArray;
        if (callback.SemanalRepeat_Days != null && callback.SemanalRepeat_Days.Length > 0)
        {
            List<JsonValue> semanalRepeat_Days = new List<JsonValue>();
            foreach (var day in callback.SemanalRepeat_Days)
            {
                semanalRepeat_Days.Add(JsonValue.Create<SemanalRepeatDay>(day));
            }
            semanalRepeat_DaysArray = new JsonArray(semanalRepeat_Days.ToArray());
        }
        else
        {
            semanalRepeat_DaysArray = new JsonArray();
        }


        JsonArray monthlyRepeat_DatesArray;
        if (callback.MonthlyRepeat_Dates != null && callback.MonthlyRepeat_Dates.Length > 0)
        {
            List<JsonValue> monthlyRepeat_Dates = new List<JsonValue>();
            foreach (var date in callback.MonthlyRepeat_Dates)
            {
                monthlyRepeat_Dates.Add(JsonValue.Create<MonthlyRepeatDate>(date));
            }
            monthlyRepeat_DatesArray = new JsonArray(monthlyRepeat_Dates.ToArray());
        }
        else
        {
            monthlyRepeat_DatesArray = new JsonArray();
        }



        JsonArray datesRepeat_DatesArray;
        if (callback.YearlyRepeat_Dates != null && callback.YearlyRepeat_Dates.Length > 0)
        {
            List<JsonValue> datesRepeat_Dates = new List<JsonValue>();
            foreach (var date in callback.YearlyRepeat_Dates)
            {
                datesRepeat_Dates.Add(JsonValue.Create<DateTimeOffset>(date));
            }
            datesRepeat_DatesArray = new JsonArray(datesRepeat_Dates.ToArray());
        }
        else
        {
            datesRepeat_DatesArray = new JsonArray();
        }


        JsonValue id = JsonValue.Create<ulong>(callback.ID);
        JsonValue manageId = JsonValue.Create<bool>(callback.ManageID);
        JsonObject owner = SerializeSchedulableModule(callback.Owner);
        JsonObject methodInfo = SerializeMethodInfo(callback.MethodInfo);
        JsonValue allowLateRepeat = JsonValue.Create<bool>(callback.AllowLateRepeat);

        JsonArray parameters = new JsonArray();
        if (callback.Parameters != null && callback.Parameters.Length > 0)
        {
            foreach (var param in callback.Parameters)
            {
                parameters.Add(SerializeObject(param));
            }
        }


        serialized["ID"] = id;
        serialized["Owner"] = owner;
        serialized["NextExecution"] = nextExecution;
        serialized["MethodInfo"] = methodInfo;
        serialized["Parameters"] = parameters;        

        serialized["ManageID"] = manageId;
        serialized["AllowLateRepeat"] = allowLateRepeat;

        serialized["ScheduleType"] = scheduleType;
        serialized["IntervalRepeat_Interval"] = intervalRepeat_interval;
        serialized["SemanalRepeat_Days"] = semanalRepeat_DaysArray;
        serialized["MonthlyRepeat_Dates"] = monthlyRepeat_DatesArray;
        serialized["DatesRepeat_Dates"] = datesRepeat_DatesArray;                                        

        return serialized;
    }

    private static JsonObject SerializeSchedulableModule(SchedulableModule module)
    {
        JsonObject serialized = new JsonObject();

        JsonValue moduleType = JsonValue.Create<string>(module.ModuleType.AssemblyQualifiedName);
        serialized["ModuleType"] = moduleType;

        return serialized;
    }

    private static JsonObject SerializeMethodInfo(MethodInfo methodInfo)
    {
        JsonObject serialized = new JsonObject();

        JsonValue declaringType = JsonValue.Create<string>(methodInfo.DeclaringType.AssemblyQualifiedName);
        JsonValue methodName = JsonValue.Create<string>(methodInfo.Name);

        serialized["DeclaringType"] = declaringType;
        serialized["MethodName"] = methodName;

        return serialized;
    }

    // OBS: Só consegue serializar DTOs simples e tipos primitivos; Objetos complexos podem não ser serializados corretamente    
    private static JsonObject SerializeObject(object obj, JsonSerializerOptions serializerOptions = null)
    {
        JsonObject serialized = new();
        serializerOptions ??= new JsonSerializerOptions { IncludeFields = true };

        JsonValue type = JsonValue.Create<string>(obj.GetType().AssemblyQualifiedName);
        string objJson = JsonSerializer.Serialize(obj, obj.GetType(), serializerOptions);

        serialized["Type"] = type;        
        serialized["Value"] = JsonValue.Parse(objJson);

        return serialized;
    }


    // Deserializadores
    public static ScheduledCallback DeserializeScheduledCallback(JsonObject serializedScheduledCallback)
    {
        ScheduledCallback callback = new ScheduledCallback();
        callback.ScheduleType = (ScheduleType) Enum.Parse(typeof(ScheduleType), serializedScheduledCallback["ScheduleType"].GetValue<string>());
        callback.NextExecution = serializedScheduledCallback["NextExecution"].GetValue<DateTimeOffset>();

        if (serializedScheduledCallback["IntervalRepeat_Interval"] != null)
            callback.IntervalRepeat_Interval = JsonSerializer.Deserialize<TimeSpan>(serializedScheduledCallback["IntervalRepeat_Interval"]);
        else
            callback.IntervalRepeat_Interval = null;

        List<SemanalRepeatDay> semanalDays = [];
        foreach (var day in serializedScheduledCallback["SemanalRepeat_Days"].AsArray())
        {            
            semanalDays.Add(JsonSerializer.Deserialize<SemanalRepeatDay>(day, FileSystem.serializerOptions));
        }
        callback.SemanalRepeat_Days = semanalDays.ToArray();

        List<MonthlyRepeatDate> monthlyDates = [];
        foreach (var date in serializedScheduledCallback["MonthlyRepeat_Dates"].AsArray())
        {
            monthlyDates.Add(JsonSerializer.Deserialize<MonthlyRepeatDate>(date, FileSystem.serializerOptions));
        }
        callback.MonthlyRepeat_Dates = monthlyDates.ToArray();

        List<DateTimeOffset> datesRepeatDates = [];
        foreach (var date in serializedScheduledCallback["DatesRepeat_Dates"].AsArray())
        {
            datesRepeatDates.Add(JsonSerializer.Deserialize<DateTimeOffset>(date, FileSystem.serializerOptions));
        }
        callback.YearlyRepeat_Dates = datesRepeatDates.ToArray();

        callback.ID = serializedScheduledCallback["ID"].GetValue<ulong>();
        callback.ManageID = serializedScheduledCallback["ManageID"].GetValue<bool>();
        callback.Owner = DeserializeSchedulableModule(serializedScheduledCallback["Owner"].AsObject());
        callback.MethodInfo = DeserializeMethodInfo(serializedScheduledCallback["MethodInfo"].AsObject());
        callback.AllowLateRepeat = serializedScheduledCallback["AllowLateRepeat"].GetValue<bool>();

        List<object> parameters = [];
        foreach (var param in serializedScheduledCallback["Parameters"].AsArray())
        {
            parameters.Add(DeserializeObject(param.AsObject()));
        }
        callback.Parameters = parameters.ToArray();

        return callback;
    }

    private static SchedulableModule DeserializeSchedulableModule(JsonObject serializedModule)
    {
        string moduleTypeName = serializedModule["ModuleType"].GetValue<string>();
        Type moduleType = Type.GetType(moduleTypeName);

        SchedulableModule module = new SchedulableModule()
        {
            ModuleType = moduleType
        };

        return module;
    }

    private static MethodInfo DeserializeMethodInfo(JsonObject serializedMethodInfo)
    {
        string declaringTypeName = serializedMethodInfo["DeclaringType"].GetValue<string>();
        string methodName = serializedMethodInfo["MethodName"].GetValue<string>();

        Type declaringType = Type.GetType(declaringTypeName);
        MethodInfo methodInfo = declaringType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

        return methodInfo;
    }

    private static object DeserializeObject(JsonObject serializedObj, JsonSerializerOptions serializerOptions = null)
    {
        serializerOptions ??= new JsonSerializerOptions { IncludeFields = true };

        string typeName = serializedObj["Type"].GetValue<string>();
        Type type = Type.GetType(typeName);

        string objJson = serializedObj["Value"].ToJsonString();
        object obj = JsonSerializer.Deserialize(objJson, type, serializerOptions);

        return obj;
    }



    // Classes 
    public class ScheduledCallback
    {
        public ScheduledCallback()
        {
            IntervalRepeat_Interval = null;
            SemanalRepeat_Days = null;
            MonthlyRepeat_Dates = null;
            YearlyRepeat_Dates = null;
            Parameters = null;
        }

        // Agendamento do callback, com informação dos horários, se deve repetir, etc.
        public ScheduleType ScheduleType { get; set; }
        public DateTimeOffset NextExecution { get; set; }
        public TimeSpan? IntervalRepeat_Interval { get; set; }
        public SemanalRepeatDay[] SemanalRepeat_Days { get; set; }
        public MonthlyRepeatDate[] MonthlyRepeat_Dates { get; set; }
        public DateTimeOffset[] YearlyRepeat_Dates { get; set; }

        // ID do callback; Pode ser usado pelas classes clientes para gerenciar callbacks agendados, evitando duplicações de agendamentos para a mesma coisa
        public ulong ID { get; set; }
        // Se verdadeiro, o Scheduler automaticamente verifica se já existe um callback com o mesmo ID e Owner antes de agendar um novo; Se existir, o novo não é agendado. Útil para evitar agendamentos duplicados.        
        // Se falso, o callback é sempre agendado (Útil para callbacks únicos/dinâmicos). Não é possil rastrear ou cancelar esses tipos de callbacks
        public bool ManageID { get; set; } = true;
        // Se verdadeiro, quando o Scheduler for religado e tiver callbacks atrasados, se esses callbacks tiverem repetições (não forem únicos), 
        // ele sempre será re-agendado para a sua próxima data programada, mesmo que ela esteja atrasada. Isso faz com que seja possível que um callback atrasado
        // seja executado várias vezes (para cada vez que atrasou) quando o Scheduler voltar a executar, até a data atual.
        // Se falso, quando o callback estiver atrasado e sua repetição for re-agendada, ela será automaticamente re-agendada para uma data futura
        // fazendo com que somente o callback mais atrasado de mesmo ID seja executado.
        public bool AllowLateRepeat { get; set; } = false;
        // SchedulableModule dono do callback
        public SchedulableModule Owner { get; set; }
        // Callback a ser executado, para que possa ser serializado e buscado em runtime por reflection, permitindo o callback e persistência (MethodInfo)
        public MethodInfo MethodInfo { get; set; }
        // Parâmetros do callback, se houver (Array de objects)
        public object[] Parameters { get; set; }


        public DateTimeOffset? NextRepeatDate(DateTimeOffset? useAsCurrentDateTime = null, bool allowLateRepeat = false)
        {
            DateTimeOffset? nextRepeat = null;            
            DateTimeOffset currentDate = DateTimeOffset.Now;

            if (useAsCurrentDateTime != null)
                currentDate = useAsCurrentDateTime.Value;

            switch (ScheduleType)
            {
                case ScheduleType.Once:
                    nextRepeat = null;
                    break;
                case ScheduleType.IntervalRepeat:

                    if (!allowLateRepeat)
                    {
                        TimeSpan atraso = currentDate - NextExecution;

                        if (atraso <= TimeSpan.Zero)
                        {
                            nextRepeat = NextExecution.Add(IntervalRepeat_Interval.Value);
                        }
                        else
                        {
                            long vezesAtrasado = (long) Math.Ceiling(atraso.TotalMilliseconds / IntervalRepeat_Interval.Value.TotalMilliseconds);

                            // Caso a data de repetição bata exatamente com o horário do relógio
                            if (NextExecution.Add(IntervalRepeat_Interval.Value * vezesAtrasado) <= currentDate)
                            {
                                // Retorna corretamente a exata próxima data para repetição
                                nextRepeat = NextExecution.Add(IntervalRepeat_Interval.Value * (vezesAtrasado + 1));
                            }
                            else
                            {
                                nextRepeat = NextExecution.Add(IntervalRepeat_Interval.Value * vezesAtrasado);
                            }
                        }                        
                    }
                    else
                    {
                        nextRepeat = NextExecution.Add(IntervalRepeat_Interval.Value);
                    }                        

                    break;
                case ScheduleType.SemanalRepeat:
                    // Cria, de acordo com os dias de repetição semanais, os próximos dias de repetição
                    // Depois ordena por ordem cronológica e seleciona o primeiro (mais próximo)                    

                    nextRepeat = SemanalRepeat_Days.Select
                    (
                        s =>
                        {
                            // Verifica a diferença do dia de repetição e o dia atual, 
                            // depois adiciona 7 dias para os casos onde o dia atual já passou o dia de repetição                                
                            int dif = ((int) s.DayOfWeek - (int) currentDate.DayOfWeek + 7) % 7;

                            // Se o dia de hoje é um dia de repetição, verifica se deve repetir hoje ou não;
                            // Caso contrário, essa repetição deve ser da próxima semana
                            DateTimeOffset dataAtual = currentDate;
                            DateTimeOffset dataRepetiçãoOffset = new DateTimeOffset(new DateOnly(dataAtual.Year, dataAtual.Month, dataAtual.Day), new TimeOnly(s.TimeOfDay.Ticks), s.TimeZone.BaseUtcOffset);                                                        

                            if (dif == 0 && currentDate >= dataRepetiçãoOffset)
                                dif = 7;

                            return dataRepetiçãoOffset.AddDays(dif);
                        }
                    ).Where(d => d > currentDate).OrderBy(d => d).FirstOrDefault();

                    break;
                case ScheduleType.MonthlyRepeat:
                    // Cria, de acordo com os dias de repetição mensais, os próximos dias de repetição
                    // Depois, ordena cronologicamente e seleciona o mais próximo
                    var candidates = MonthlyRepeat_Dates.Select(s =>
                    {
                        int year = currentDate.Year;
                        int month = currentDate.Month;

                        // Garante que o mês desse dia mensal de repetição será válido
                        var nextValidDate = FindNextValidMonthAndYear(s, year, month);
                        year = nextValidDate.nextValidYear;
                        month = nextValidDate.nextValidMonth;

                        // Cria o dia válido desse dia de repetição mensal
                        var possibleNext = new DateTimeOffset(new DateOnly(year, month, s.DayOfMonth), new TimeOnly(s.TimeOfDay.Ticks), s.TimeZone.BaseUtcOffset);

                        // Verifica se a repetição desse dia nesse mês já passou; Caso sim, passar para o próximo mês possível
                        if (currentDate >= possibleNext)
                        {
                            nextValidDate = FindNextValidMonthAndYear(s, year, month + 1);
                            year = nextValidDate.nextValidYear;
                            month = nextValidDate.nextValidMonth;
                            possibleNext = new DateTimeOffset(new DateOnly(year, month, s.DayOfMonth), new TimeOnly(s.TimeOfDay.Ticks), s.TimeZone.BaseUtcOffset);
                        }

                        return possibleNext;
                    });

                    nextRepeat = candidates.Where(c => c > currentDate).OrderBy(c => c).FirstOrDefault();
                    break;
                case ScheduleType.YearlyRepeat:
                    // Filtra as datas que já passaram, ordena e seleciona a mais próxima
                    nextRepeat = YearlyRepeat_Dates.Where(d => d > currentDate).OrderBy(d => d).FirstOrDefault();
                    break;
            }

            return nextRepeat;
        }


        public static (int nextValidMonth, int nextValidYear) FindNextValidMonthAndYear(MonthlyRepeatDate s, int year, int month)
        {
            // Procura o próximo mês que tem esse dia
            while (true)
            {
                if (month > 12) { month = 1; year++; }

                if (s.IsValid(month, year))
                {
                    return (month, year);
                }

                month++;
            }
        }

        public static ScheduledCallback FromTemplate(Delegate callback, object[] parameters, ulong ID, bool ManagedCallback = true, bool AllowLateRepeat = false)
        {
            ScheduledCallback c = new();

            c.ID = ID;
            c.ManageID = ManagedCallback;
            c.Parameters = parameters;
            c.MethodInfo = callback.Method;
            c.AllowLateRepeat = AllowLateRepeat;

            SchedulableModule ownerModule = new();
            ownerModule.ModuleType = callback.Method.DeclaringType;
            c.Owner = ownerModule;

            return c;
        }

    }

    public class SchedulableModule
    {
        // Como só é permitido um tipo de módulo por vez, podemos usar o Type para identificar o módulo
        public Type ModuleType { get; set; }
    }


}