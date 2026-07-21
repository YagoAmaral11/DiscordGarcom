using DiscordGarçom.GarcModules;
using DiscordGarçom.Containers.Core.Modules;
using DiscordGarçom.Containers.IO;
using System.Threading.Tasks;
using DiscordGarçom.Containers.Core.Modules.Examples;
using System;

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
        
        SimpleContainer garcKittsShell = new(persistance: fileSystem, modules: [garcKFrasesModule, scheduler, backuper, channelManager], LinkedServerID: 832773492738490448);        

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