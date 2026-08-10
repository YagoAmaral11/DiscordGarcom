using DiscordGarçom.Containers.Core.Modules;
using DiscordGarçom.Containers.Core.Modules.Examples;
using DiscordGarçom.Containers.IO;
using DiscordGarçom.Garc_Modules;
using DiscordGarçom.GarcModules;
using System;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core;

public static class Init
{
    public static async Task Main(string[] args)
    {
        FileSystem fileSystem = new();

        CoreScheduler scheduler = new(fileSystem);
        CoreBackuper backuper = new(fileSystem, fileSystem, scheduler);

        Frases garcKFrasesModule = new(fileSystem, fileSystem, scheduler);            
        
        CoreChannelManager channelManager = new(fileSystem, fileSystem, scheduler);

        Party garcPartyModule = new(fileSystem, fileSystem, channelManager, scheduler);
        Utility garcUtilityModule = new(fileSystem, fileSystem);
        Jukebox garcJukeboxModule = new(fileSystem, fileSystem);

        SimpleContainer garcKittsShell = new(persistance: fileSystem, 
            modules: [garcKFrasesModule, scheduler, backuper, channelManager, garcPartyModule, garcUtilityModule, garcJukeboxModule], LinkedServerID: 832773492738490448);        

        if (await garcKittsShell.Start())
        {            
            await Task.Delay(-1);
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Container could not start correctly; Discord server not linked");
            Console.ResetColor();
        }        
    }
}