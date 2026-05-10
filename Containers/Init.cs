using GarçomDoKitts.GarcModules;
using GarçomDoKitts.Containers.Core.Modules;
using GarçomDoKitts.Containers.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core;

public static class Init
{
    public static async Task Main(string[] args)
    {
        FileSystem fileSystem = new();
        CoreScheduler scheduler = new(fileSystem);
        Frases garcKFrasesModule = new(fileSystem, fileSystem, scheduler);                

        SimpleContainer garcKittsShell = new(persistance: fileSystem, modules: [garcKFrasesModule, scheduler], LinkedServerID: 832773492738490448);        

        if (await garcKittsShell.Start())
        {            
            await Task.Delay(-1);
        }
        else
        {
            Debug.WriteLine("Container could not start correctly; Discord server not linked");
        }        
    }
}