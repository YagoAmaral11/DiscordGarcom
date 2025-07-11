using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts
{
    public class BotConsole
    {
        public BotConsole()
        {
            Init();
        }

        private void Init()
        {
            Console.ResetColor();
            Console.CursorVisible = false;
        }

        // Atualiza o console por tick
        public void ConsoleTick()
        {

        }

        public static void WriteWithColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        // Atualiza o console por minuto (Limpa os logs de evento)
        public void ConsoleMinute()
        {
            Console.Clear();
            WriteWithColor($"(Program) Bot Time: {Program.PrintTimeNow()} (Local Time: {DateTime.UtcNow})\n", ConsoleColor.Magenta);

            if (Program.modulo_Jukebox.connectedEndpoint != null)
            {
                WriteWithColor($"(Lavalink) Online! Connected to endpoint {Program.modulo_Jukebox.connectedEndpoint.Hostname}\n", ConsoleColor.Green);
            }
            else
            {
                WriteWithColor($"(Lavalink) Offline!\n", ConsoleColor.Red);
            }

            if (Program.modulo_Jukebox.IsConnected)
            {
                WriteWithColor($"(Jukebox) Connected to {Program.modulo_Jukebox.lavalinkPlayback.Channel.Name}\n", ConsoleColor.Green);

                if (Program.modulo_Jukebox.songCurrent != null)
                    WriteWithColor($"(Jukebox) Playing {Program.modulo_Jukebox.songCurrent.Title}\n", ConsoleColor.DarkGray);

                if (Program.modulo_Jukebox.ThereIsQueue)
                    WriteWithColor($"(Jukebox) With {Program.modulo_Jukebox.songQueue.Count} in queue\n", ConsoleColor.DarkGray);
            }
        }
    }
}
