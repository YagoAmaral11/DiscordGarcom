using DSharpPlus;
using DSharpPlus.Commands.Trees;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core;

/*  ORDEM DE CHAMADA DOS MÉTODOS:     
 *  1. Initialize (Usado para inicializar as variáveis do módulo; Não usar outro módulo aqui)
 *  2. ConfigureEventHandlers (Usado para receber eventos do bot)
 *  3. Start (Usado assim que o bot se conecta ao servidor e está prestes a executar; Se precisar usar outro módulo, use aqui;)
 * 
 */

public interface IModule
{

    public string Name { get; } // Usado em logs e mensagens
    public string LogName => "[" + Name + "]"; // Usado em logs e mensagens

    public List<Type> GetStaticCommands(); // Usado para registrar comandos de texto e slash commands, que não dependem de dados da instância
    public IEnumerable<CommandBuilder> GetDynamicCommands(); // Usado para registrar comandos de texto e slash commands, que dependem de dados da instância

    public Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider);
    public Task ConfigureEventHandlers(EventHandlingBuilder ehb);

    public Task Start();
    public async Task<bool> Shutdown() => await SaveData();

    public Task<bool> SaveData();    

}