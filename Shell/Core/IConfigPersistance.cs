using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IConfigPersistance
{

    public Task WriteConfig(object ToWrite, Type objectType, IModule module);
    public Task<object> LoadConfig(Type objectType, IModule module);

}
