using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IConfigPersistance
{

    public Task<bool> ConfigExists(IModule module);
    public Task WriteConfig(object module, object config);
    public Task<object> LoadConfig(object module, Type configType);    
    
}
