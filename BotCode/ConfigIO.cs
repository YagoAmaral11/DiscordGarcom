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
        public static readonly string TokenPath = "token.json";
        public static readonly string ConfigPath = "config.json";
        public static readonly string DataPath = "data.json";

        public static async Task LoadConfig()
        {
            using (StreamReader sr = new StreamReader(TokenPath))
            {
                string json = await sr.ReadToEndAsync();
                TokenJSON token = JsonConvert.DeserializeObject<TokenJSON>(json);

                Program.token = token;
            }

            using (StreamReader sr = new StreamReader(ConfigPath))
            {
                string json = await sr.ReadToEndAsync();
                ConfigJSON config = JsonConvert.DeserializeObject<ConfigJSON>(json);

                Program.config = config;
            }
        }

        public static async Task WriteConfig()
        {
            string json = JsonConvert.SerializeObject(Program.config, Formatting.Indented);

            using (StreamWriter sw = new StreamWriter(ConfigPath))
            {
                await sw.WriteAsync(json);
            }
        }

        public static async Task Write(string location, object toWrite)
        {
            string json = JsonConvert.SerializeObject(toWrite, Formatting.Indented);

            using (StreamWriter sw = new StreamWriter(location))
            {
                await sw.WriteAsync(json);
            }
        }

        public static async Task<object> Load(string location, Type type)
        {
            string json;

            using (StreamReader sr = new StreamReader(location))
            {
                json = await sr.ReadToEndAsync();
                return JsonConvert.DeserializeObject(json, type);
            }
        }

    }

    public class TokenJSON
    {
        public string Token { get; set; }
    }

    public class ConfigJSON
    {
        // prefixo
        public string Prefix { get; set; }

        // logs
        public bool Log_Ticks { get; set; } // Se os ticks principais do bot devem ser logados
        public bool Log_LogTicks { get; set; } // Se os ticks de Logging (ticks secundários) devem ser logados

        // Timer
        public float Timers_TickTimerMs { get; set; } // O tempo, em milissegundos, que o loop principal do bot ocorrerá
        public float Timers_LogTimerMs { get; set; } // O tempo, em milissegundos, que o loop de logging do bot ocorrerá
        
        // Frase Diária
        public int FraseDiaria_HoraDeEnvio { get; set; } // O hora do dia que o bot enviará uma frase
        public int FraseDiaria_MinsDeEnvio { get; set; } // Os minutos do dia que o bot enviará uma frase (é usado em conjunto com o HoraDeEnvio)
        // Quantas frases existem no canal de frases; Usar a pesquisa do discord para descobrir; É usado quando o bot é iniciado pela primeiro vez;
        // Esse valor é atualizado e guardado dentro do modulo de frase diária quando mensagens são excluidas e adicionadas no frases, mas somente quando o bot está online
        // Logo, é importante que, no futuro, o bot guarde esse valor (junto com todas as instâncias de módulos) em um local separado, para que ele seja carregado.
        public int FraseDiaria_total { get; set; } 
        public ulong FraseDiaria_CanalFetchID { get; set; } // O ID do canal de frases (de onde elas devem ser puxadas)
        public ulong FraseDiaria_CanalEnvioID { get; set; } // O ID do canal de frases para envio das frases


        public ConfigJSON()
        {
            Prefix = "Garçom, ";

            Log_Ticks = false;   
            Log_LogTicks = true;

            Timers_TickTimerMs = 100;
            Timers_LogTimerMs = 1000;

            FraseDiaria_HoraDeEnvio = 12;
            FraseDiaria_MinsDeEnvio = 0;
            FraseDiaria_total = 1168; // 07/02/2025 18:50
            FraseDiaria_CanalFetchID = 935704934144434196;
            FraseDiaria_CanalEnvioID = 832773492738490452;
        }
    }

}
