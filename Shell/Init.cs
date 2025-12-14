using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GarçomDoKitts.Shell;
using GarçomDoKitts.Shell.IO;

namespace GarçomDoKitts.Shell.Core;

public static class Init
{
    public static async Task Main(string[] args)
    {
        FileSystem fileSystem = new();
        CoreShell garcKittsShell = new(fileSystem, new(), 832773492738490448);        
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