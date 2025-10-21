using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IModule
{

    public string Name { get; } // Usado em logs e mensagens

    public Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider);    
    public Task SaveData();
    public Task<bool> Shutdown();
    public List<Type> GetCommands();

}