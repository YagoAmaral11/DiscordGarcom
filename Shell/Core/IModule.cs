using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IModule
{

    public string Name { get; } // Usado em logs e mensagens

    /*
     *  ORDEM DE CHAMADA DOS MÉTODOS:
     *  1. Initialize
     *  2. ConfigureEventHandlers
     *  3. Start
     * 
     */

    public Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider);
    public Task ConfigureEventHandlers(EventHandlingBuilder ehb);
    public Task Start();

    public Task SaveData();
    public Task<bool> Shutdown();
    public List<Type> GetCommands();    

}