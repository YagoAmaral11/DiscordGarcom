using DSharpPlus;
using DSharpPlus.Commands.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core.Modules;

public class CoreScheduler : IModule
{
    public string Name => "Core Scheduler";

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;
    public IEnumerable<CommandBuilder> GetDynamicCommands() => [];    
    public List<Type> GetStaticCommands() => [];    

    public Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SaveData()
    {
        // TODO: Salvar os callbacks agendados 
        throw new NotImplementedException();
    }

    public Task Start()
    {
        throw new NotImplementedException();
    }

    private enum ScheduleType
    {
        Once, // Executa uma vez em uma data específica
        IntervalRepeat, // Executa repetidamente em um intervalo específico
        SemanalRepeat, // Executa em dias específicas da semana
        MonthlyRepeat, // Executa em dias específicos do mês
        DatesRepeat, // Executa em datas específicas do ano
    }

    private class ScheduledCallback
    {
        // TODO: Agendamento do callback, com informação dos horários, se deve repetir, etc.
        public ScheduleType ScheduleType { get; set; }
        public DateTimeOffset NextExecution { get; set; }        
        public TimeSpan? IntervalRepeat_Interval { get; set; }
        public DateTimeOffset[] SemanalRepeat_Days { get; set; } 
        public DateTimeOffset[] MonthlyRepeat_Dates { get; set; }
        public DateTimeOffset[] DatesRepeat_Dates { get; set; }

        // TODO: ID do callback; Pode ser usado pelas classes clientes para gerenciar callbacks agendados, evitando duplicações de agendamentos para a mesma coisa
        public uint ID { get; set; }
        // TODO: SchedulableModule dono do callback
        public SchedulableModule Owner { get; set; }
        // Callback a ser executado, para que possa ser serializado e buscado em runtime por reflection, permitindo o callback e persistência (MethodInfo)
        public MethodInfo MethodInfo { get; set; }
        // Parâmetros do callback, se houver (Array de objects)
        public object[] Parameters { get; set; }
    }

    public class SchedulableModule
    {
        // Como só é permitido um tipo de módulo por vez, podemos usar o Type para identificar o módulo
        public Type ModuleType { get; set; }        
    }

    // TODO: Implementar uma E. D. para gerenciar os callbacks, que ordena automaticamente os callbacks pela data de execução
    // TODO: Implementar um método cancelável para interrupções de callbacks agendados, que espera até o próximo callback agendado usando Task.Delay
    //       e, assim que o tempo chegar, executa o scheduler 
    // TODO: Implementar o método scheduler, que verifica os callbacks agendados, executa os que devem ser executados até aquele momento, rearranja a E. D. e aguarda o próximo callback
    // TODO: Implementar métodos para agendar novos callbacks e cancelar callbacks agendados
    // TODO: Implementar um serializador customizado para ScheduledCallback e SchedulableModule, que permita salvar System.Type, System.Reflection.MethodInfo e Object em um JSON.
}