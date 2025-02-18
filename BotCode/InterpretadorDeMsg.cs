using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace GarçomDoKitts
{
    public class InterpretadorDeMsg
    {
        public static readonly string DataPath = $"{DataIO.DataFolderPath}interpretador.json";
        public List<Comandos> comandos;


        public void Init()
        {

        }

        public void Dispose()
        {

        }

        public void Loop()
        {

        }

        public void MsgReceived(DiscordClient sender, DSharpPlus.EventArgs.MessageCreateEventArgs args)
        {
            // Código podre, mas tava tarde da noite :)
            // Verifica se a mensagem recebida é um comando registrado 

            string prefix = Program.config.Prefix.ToLower();
            string message = args.Message.Content.ToLower();

            if (!message.StartsWith(prefix))
                return;

            foreach (var comando in comandos)
            {
                // Verificar se começa com prefixo do bot. Caso contrário, não é comando                

                // Verificar se, para esse comando em específico, todas as frases (e variações delas) estão na mensagem.
                bool éComando = true;

                foreach (var fraseVariante in comando.frasesDoComando)
                {
                    bool variaçãoDeFraseExiste = false; 

                    foreach (var variação in fraseVariante.varations)
                    {
                        if (message.Contains(variação))
                        {
                            variaçãoDeFraseExiste = true;
                        }                        
                    }               
                    
                    if (!variaçãoDeFraseExiste)
                    {
                        éComando = false;
                    }
                }

                // Se é realmente um comando, acioná-lo.
                if (éComando)
                {


                    return;
                }
            }
        }
        
        public class Comandos
        {            
            public List<VariaçãoDeFrase> frasesDoComando = new List<VariaçãoDeFrase>(); // As partes que ditam que essa mensagem é um comando. Ex.: (frase, frases) e (do dia, diária, diaria)
            public Command comando { get; set; } // O comando que deve ser chamado            
        }

        public class VariaçãoDeFrase
        {
            public List<string> varations = new List<string>();
        }

        public delegate Task Command(CommandContext context);

    }
}
