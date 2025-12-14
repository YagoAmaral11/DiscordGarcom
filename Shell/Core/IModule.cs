using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IModule
{

    public string Name { get; } // Usado em logs e mensagens

    /*  ORDEM DE CHAMADA DOS MÉTODOS:     
     *  1. Initialize (Usado para inicializar as variáveis do módulo; Não usar outro módulo aqui)
     *  2. ConfigureEventHandlers (Usado para receber eventos do bot)
     *  3. Start (Usado para iniciar o bot em si; Se precisar usar outro módulo, use aqui)
     * 
     */

    public Task<bool> Initialize(IServerContext serverContext, IServiceProvider serviceProvider);
    public Task ConfigureEventHandlers(EventHandlingBuilder ehb);
    public Task Start();

    public Task SaveData();
    public Task<bool> Shutdown();
    public List<Type> GetCommands(); // Usado para registrar comandos de texto e slash commands

}