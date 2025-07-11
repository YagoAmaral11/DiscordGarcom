using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts
{   
    public interface IModule
    {
        public Task Init();
        public Task Loop();
    }

    public interface IPersistantModule : IModule, IPersist {};

    public interface IPersist
    {
        public Task SaveInstance();
    }

    public interface IContainCommands 
    {
        public List<Type> GetCommands();
    }

}
