using System;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core;

public interface IConfigPersistance
{

    public Task<bool> ConfigExists(IModule module);

    public Task WriteConfig(object module, object config);

    public Task<object> LoadConfig(object module, Type configType);    
    
}
