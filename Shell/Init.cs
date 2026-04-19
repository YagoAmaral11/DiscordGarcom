using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using GarçomDoKitts.GarcModules;
using GarçomDoKitts.Shell;
using GarçomDoKitts.Shell.Core.Modules;
using GarçomDoKitts.Shell.IO;

namespace GarçomDoKitts.Shell.Core;

public static class Init
{
    public static async Task Main(string[] args)
    {
        FileSystem fileSystem = new();
        CoreScheduler scheduler = new(fileSystem);
        Frases garcKFrasesModule = new(fileSystem, fileSystem, scheduler);                

        CoreShell garcKittsShell = new(persistance: fileSystem, modules: [garcKFrasesModule, scheduler], LinkedServerID: 832773492738490448);        

        if (await garcKittsShell.Start())
        {            
            await Task.Delay(-1);
        }
        else
        {
            Debug.WriteLine("Shell could not start correctly; Discord server not linked");
        }        
    }
}