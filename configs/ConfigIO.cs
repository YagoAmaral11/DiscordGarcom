using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GarçomDoKitts.configs
{
    public static class ConfigIO
    {

        public static async Task LoadConfig()
        {
            using (StreamReader sr = new StreamReader("token.json"))
            {
                string json = await sr.ReadToEndAsync();
                TokenJSON token = JsonConvert.DeserializeObject<TokenJSON>(json);

                Program.token = token;
            }

            using (StreamReader sr = new StreamReader("config.json"))
            {
                string json = await sr.ReadToEndAsync();
                ConfigJSON config = JsonConvert.DeserializeObject<ConfigJSON>(json);

                Program.config = config;
            }
        }

        public static async Task WriteConfig()
        {
            string json = JsonConvert.SerializeObject(Program.config);

            using (StreamWriter sw = new StreamWriter("config.json"))
            {
                await sw.WriteAsync(json);
            }
        }

    }

    public class TokenJSON
    {
        public string token { get; set; }
    }

    public class ConfigJSON
    {        
        public bool logTicks { get; set; }
        public bool logFrasesDoDia { get; set; }

        public string prefix { get; set; }
        public float mainTimerIntervalMs { get; set; }        
        public float minFraseIntervalSec { get; set; }
        public int quantiaInicialDeFrases { get; set; } 
        public ulong canalFrasesId { get; set; }
        public ulong canalFrasesEnvioId { get; set; }
    }

}
