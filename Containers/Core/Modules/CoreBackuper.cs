using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

public class CoreBackuper(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : BaseModule<CoreBackuperConfig, CoreBackuperData>(persistance, configPersistance)
{    
    IScheduler scheduler = scheduler;    

    public override string Name => "CoreBackuper";
    protected override bool ThrowExceptionOnMissingConfig => false;
    protected override CoreBackuperConfig InitializeConfig() => new();
    protected override CoreBackuperData InitializeData() => new();

    public override Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;

    public override List<Type> GetStaticCommands() => [];
    public override IEnumerable<CommandBuilder> GetDynamicCommands()
    {        
        var backupCmd = CommandBuilder.From(RealizeBackupCmd).WithDescription("Realiza um backup manual dos dados dos módulos do bot");
        return [backupCmd];
    }    


    public override Task Start()
    {
        // Inicializa agendamentos
        scheduler.ScheduleRepeatEvery(new Func<Task>(RealizeBackup), null, 0, config.BackupInterval);

        return Task.CompletedTask;
    }    


    public async Task RealizeBackup()
    {        
        foreach (IModule module in serverContext.GetAllModules())
        {
            if (module == this)
                continue;

            Console.WriteLine((this as IModule).LogName + " backing up module " + module.LogName);

            try
            {
                if (await module.SaveData())
                {
                    Console.WriteLine((this as IModule).LogName + " backup of module " + module.LogName + " completed successfully.");
                }
                else
                {
                    Console.WriteLine((this as IModule).LogName + " backup of module " + module.LogName + " failed.");
                }
            }
            catch (Exception e)
            {
                await BaseModule<int, int>.DumpException(module, e, persistance);                
            }
        }
    }


    // COMANDOS
    [Command("Backup")]    
    public async Task RealizeBackupCmd(CommandContext ctx)
    {
        // TODO: Limitar isso à somente administradores do bot        
        if (!await CommandReadyPreCondition(ctx))
            return;

        try
        {
            if (!ctx.Member.Permissions.HasPermission(DiscordPermission.Administrator))
            {
                await CommandErrorResponse(ctx, "Você não tem permissão para usar esse comando");                
                return;
            }

            await RealizeBackup();
            await ctx.RespondAsync("Backup realizado");
        }
        catch (Exception e)
        {
            await DumpException(e);            
        }        
    }

}

public class CoreBackuperConfig
{
    [JsonInclude] public TimeSpan BackupInterval { get; set; } = new TimeSpan(1, 0, 0);
}

public class CoreBackuperData
{
}