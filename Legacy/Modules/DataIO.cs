using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.configs
{

    public static class DataIO
    {
        public static readonly string DataFolderPath = "data/";        
        public static readonly string TokenPath = $"{DataFolderPath}token.json";
        public static readonly string ConfigPath = $"{DataFolderPath}config.json";
        public static readonly string TaskDonePath = $"{DataFolderPath}mensagens.json";

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
        public List<string> Prefixs { get; set; }

        // Geral
        public TimeZoneInfo Program_UTC { get; set; }
        public CultureInfo Program_LocalCulture { get; set; }
        public ulong Program_AdminID { get; set; }

        // Timer
        public float Timers_TickTimerMs { get; set; } // O tempo, em milissegundos, que o loop principal do bot ocorrerá        
        
        // Frase Diária
        public int Frases_HoraDeEnvio { get; set; } // O hora do dia que o bot enviará uma frase
        public int Frases_MinsDeEnvio { get; set; } // Os minutos do dia que o bot enviará uma frase (é usado em conjunto com o HoraDeEnvio)
        // Quantas frases existem no canal de frases; Usar a pesquisa do discord para descobrir; É usado quando o bot é iniciado pela primeiro vez;
        // Esse valor é atualizado e guardado dentro do modulo de frase diária quando mensagens são excluidas e adicionadas no frases, mas somente quando o bot está online
        // Logo, é importante que, no futuro, o bot guarde esse valor (junto com todas as instâncias de módulos) em um local separado, para que ele seja carregado.
        public int Frases_totalInicial { get; set; } 
        public ulong Frases_CanalFetchID { get; set; } // O ID do canal de frases (de onde elas devem ser puxadas)
        public ulong Frases_CanalEnvioID { get; set; } // O ID do canal de frases para envio das frases

        // Backuper
        public double Backuper_BackupIntervalMs { get; set; } 

        // Jukebox        
        public ulong Jukebox_CommandChannel { get; set; } // Canal para receber comandos das músicas
        public float Jukebox_Timeout { get; set; } // Tempo até o bot desconectar se não tiver ninguém na call        

        // Gerenciador De Canais
        public ulong ChannelManager_Categoria { get; set; }
        public string ChannelManager_NameTemplate { get; set; } // Como um template de canal
        public int ChannelManager_MaxTempPerUser { get; set; }

        // Jogos
        public ulong Jogos_CanalDeLobby { get; set;}

        [JsonConstructor]
        public ConfigJSON()
        {
            Prefixs = new List<string>
            {
                "Garçom, ",
                "Garcom, ",
                "Garc ",
                "Garç ",
                "!g ",
                "!G ",
                "/g ",
                "/G "
            };

            Program_UTC = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); // Pega o horário de Brasília
            Program_LocalCulture = new CultureInfo("pt-BR"); // Usado para mostrar o tempo certo
            Program_AdminID = 0; // ID do admin, que poderá desligar o bot, etc.

            Timers_TickTimerMs = 100;            

            Frases_HoraDeEnvio = 12;
            Frases_MinsDeEnvio = 0;
            Frases_totalInicial = -1; // Faz com que o Bot use Fetch para descobrir quantia de mensagens 
            Frases_CanalFetchID = 935704934144434196;
            Frases_CanalEnvioID = 832773492738490452;

            Backuper_BackupIntervalMs = 30 * 60000; // A cada 30 minutos

            Jukebox_CommandChannel = 898676291010699264;
            Jukebox_Timeout = 5 * 60 * 1000;

            ChannelManager_Categoria = 1357794738744332489;
            ChannelManager_NameTemplate = "•᲼";
            ChannelManager_MaxTempPerUser = 3;

            Jogos_CanalDeLobby = 1360352934264705274;
        }
    }    

    public class TaskDone
    {
        public List<TaskDoneMsg> Msgs { get; set; }
    }

    public class TaskDoneMsg
    {
        public string Msg { get; set; }
        public uint Weight { get; set; }
    }

}