using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts
{
    public class Backuper
    {        
        public double timeToBackup;
        public double BackupInterval => Program.config.Backuper_BackupIntervalMs;

        public void Init()
        {
            timeToBackup = BackupInterval;
        }

        public async Task Loop()
        {
            timeToBackup -= Program.config.Timers_TickTimerMs;

            if (timeToBackup <= 0)
            {
                timeToBackup = BackupInterval;
                await Backup();
            }
        }

        private async Task Backup()
        {
            Console.WriteLine($"(Backuper) Inicializando Backup em {Program.GetTime()}");

            await Program.SaveModules();

            Console.WriteLine($"(Backuper) Backup finalizado");
        }
    }
}
