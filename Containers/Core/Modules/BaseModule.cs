using DSharpPlus;
using DSharpPlus.Commands.Trees;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core.Modules;

/*
 *  Um módulo base que implementa IModule e fornece funcionalidades básicas de configuração e persistência de dados.
 *  Cria uma configuração padrão se não houver uma existente e carrega os dados salvos do módulo.
 *  O usuário deve fornecer métodos para criar a instância de config e data default e definir o comportameno do módulo para as configs iniciais
 */

public abstract class BaseModule<Config, SavedData>(IPersistance persistance, IConfigPersistance configPersistance) : IModule
{
    protected Config config;
    protected SavedData data;

    protected IPersistance persistance = persistance;
    protected IConfigPersistance configPersistance = configPersistance;

    protected IServiceProvider services;
    protected IServerContext serverContext;

    public abstract string Name { get; }
    protected abstract bool ThrowExceptionOnMissingConfig { get; }

    public abstract Task ConfigureEventHandlers(EventHandlingBuilder ehb);

    public abstract IEnumerable<CommandBuilder> GetDynamicCommands();
    public abstract List<Type> GetStaticCommands();
    public abstract Task Start();


    public virtual async Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider)
    {
        if (persistance == null)
            throw new Exception(((IModule) this).LogName + " IPersistance is not assigned to the module");

        if (configPersistance == null)
            throw new Exception(((IModule) this).LogName + " IConfigPersistance is not assigned to the module");

        this.serverContext = serverContext;
        this.services = serviceProvider;

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

        data = InitializeData();
        await LoadData();

        return true;
    }       

    public virtual async Task<bool> SaveData()
    {
        await persistance.WriteObject(data, typeof(SavedData), Name + "Data");
        return true;
    }

    protected virtual async Task LoadData()
    {
        if (await persistance.KeyExists(Name + "Data.json"))
        {
            SavedData loadedData = (SavedData) await persistance.ReadObject(Name + "Data", typeof(SavedData));
            data = loadedData;
        }
    }

    protected virtual async Task LoadConfig()
    {
        Config loadedConfig = (Config) await configPersistance.LoadConfig(this, typeof(Config));
        config = loadedConfig;
    }


    // Usados para inicializar data e config; Data quando não nenhuma data é carregada. Config quando não existe nenhum config inicial.
    protected abstract SavedData InitializeData();
    protected abstract Config InitializeConfig();
}