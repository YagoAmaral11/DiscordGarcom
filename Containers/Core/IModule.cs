using DSharpPlus;
using DSharpPlus.Commands.Trees;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.Core;

/*  ORDEM DE CHAMADA DOS MÉTODOS:      
 *  Initialize (Usado para inicializar as variáveis do módulo; Não usar outro módulo aqui)
 *  ConfigureEventHandlers (Usado para receber eventos do bot)
 *  ConfigureServices (Usado para adicionar IServices que o módulo usa) 
 *  ReceiveServices (Recebe o ServiceProvider construído)
 *  Pre-Start 0 (Um tipo de segundo Initialize, usado para inicializar outras variáveis de módulo)
 *  Pre-Start 1 (Um tipo de segundo Initialize, usado para inicializar outras variáveis de módulo)
 *  Pre-Start 2 (Um tipo de segundo Initialize, usado para inicializar outras variáveis de módulo)
 *  Start (Usado assim que o bot se conecta ao servidor e está prestes a executar; Se precisar usar outro módulo, use aqui)
 */

public interface IModule
{

    public string Name { get; } // Usado em logs e mensagens
    public string LogName => "[" + Name + "]"; // Usado em logs e mensagens

    public List<Type> GetStaticCommands(); // Usado para registrar comandos de texto e slash commands, que não dependem de dados da instância
    public IEnumerable<CommandBuilder> GetDynamicCommands(); // Usado para registrar comandos de texto e slash commands, que dependem de dados da instância

    public Task ConfigureServices(IServiceCollection services) => Task.CompletedTask; // Permite ao módulo registrar serviços
    public Task<bool> Initialize(IServerContext serverContext);
    public Task ConfigureEventHandlers(EventHandlingBuilder ehb);
    public Task ReceiveServices(IServiceProvider serviceProvider);

    public Task PreStart_0() => Task.CompletedTask;
    public Task PreStart_1() => Task.CompletedTask;
    public Task PreStart_2() => Task.CompletedTask;

    public Task Start();

    public async Task<bool> Shutdown()
    {
        Console.WriteLine(LogName + " shutting down, trying to save data");
        if (await SaveData())
        {
            Console.WriteLine(LogName + " saved successfully.");
            return true;
        }
        else
        {
            Console.WriteLine(LogName + " failed to save data.");
            return false;
        }
    }

    public Task<bool> SaveData();            

}