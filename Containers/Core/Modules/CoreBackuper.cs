using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core.Modules;

public class CoreBackuper(IPersistance persistance, IConfigPersistance configPersistance, IScheduler scheduler) : IModule
{
    IServerContext serverContext;
    CoreBackuperConfig config;
    IPersistance persistance = persistance;
    IConfigPersistance configPersistance = configPersistance;
    IScheduler scheduler = scheduler;

    public string Name => "CoreBackuper";

    public Task ConfigureEventHandlers(EventHandlingBuilder ehb) => Task.CompletedTask;    
    public IEnumerable<CommandBuilder> GetDynamicCommands()
    {        
        var backupCmd = CommandBuilder.From(RealizeBackupCmd).WithDescription("Realiza um backup manual de todos os módulos do bot");
        return [backupCmd];
    }
    public List<Type> GetStaticCommands() => [];



    public async Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {
        IModule mod = this;

        if (persistance == null)
            throw new Exception(mod.LogName + "IPersistance is not assigned to the module");

        if (configPersistance == null)
            throw new Exception(mod.LogName + "IConfigPersistance is not assigned to the module");

        this.serverContext = serverContext;
        config = new();

        if (await configPersistance.ConfigExists(this))
        {
            config = await configPersistance.LoadConfig(this, typeof(CoreBackuperConfig)) as CoreBackuperConfig;
        }
        else
        {
            // Cria uma configuração inicial
            await configPersistance.WriteConfig(this, config);
            throw new Exception(mod.LogName + " config not found. Please modify the standard one.");
        }

        return true;
    }    

    public Task PreStart_0() => Task.CompletedTask;

    public Task Start()
    {
        // Inicializa agendamentos
        scheduler.ScheduleRepeatEvery(new Func<Task>(RealizeBackup), null, 0, config.BackupInterval);

        return Task.CompletedTask;
    }


    public Task<bool> SaveData() => Task.FromResult(true);



    public async Task RealizeBackup()
    {        
        foreach (IModule module in serverContext.GetAllModules())
        {
            if (module == this)
                continue;

            Console.WriteLine((this as IModule).LogName + " backing up module " + module.LogName);
            if (await module.SaveData())
            {
                Console.WriteLine((this as IModule).LogName + " backup of module " + module.LogName + " completed successfully.");
            }
            else
            {
                Console.WriteLine((this as IModule).LogName + " backup of module " + module.LogName + " failed.");
            }
        }
    }

    [Command("Backup")]    
    public async Task RealizeBackupCmd(CommandContext ctx)
    {
        // TODO: Limitar isso à somente administradores do bot        
        if (!serverContext.ReadyForCommands)
            return;

        try
        {
            if (!ctx.Member.Permissions.HasPermission(DiscordPermission.Administrator))
            {
                await ctx.RespondAsync("Você não tem permissão para usar esse comando.");
                return;
            }

            await RealizeBackup();
            await ctx.RespondAsync("Backup realizado");
        }
        catch (Exception e)
        {
            Console.WriteLine(((IModule)this).LogName + $" Error in RealizeBackup command: {e.Message}");
        }        
    }

}

public class CoreBackuperConfig
{
    [JsonInclude] public TimeSpan BackupInterval { get; set; } = new TimeSpan(1, 0, 0);
}