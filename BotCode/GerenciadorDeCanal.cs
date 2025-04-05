using GarçomDoKitts;
using GarçomDoKitts.configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using DSharpPlus.Entities;
using DSharpPlus;
using System.IO;

namespace GarçomDoKitts
{
    public class GerenciadorDeCanal
    {
        [JsonIgnore] public static readonly string dataPath = $"{DataIO.DataFolderPath}channelManager.json";

        [JsonIgnore] public static ulong CategoriaCanaisTempId => Program.config.ChannelManager_Categoria;
        [JsonIgnore] public static string TemplateDeNome => Program.config.ChannelManager_NameTemplate;

        // Classes
        [JsonIgnore] DiscordChannel CategoriaCanaisTemp;

        // Runtime Data
        [JsonProperty] private List<RegistroDeCanal> canaisTemporários = new();



        public async Task<RegistroDeCanal> NovoCanalTemporário(DateTime lifespan, string nome = "🕙 Canal Temporário", DiscordUser pedinte = null)
        {
            RegistroDeCanal novo = new()
            {
                lifespan = lifespan
            };

            if (pedinte != null)
            {
                novo.pedinteId = pedinte.Id;
                novo.pedinte = pedinte;
            }
            else
            {
                novo.pedinteId = Program.client.CurrentUser.Id;
                novo.pedinte = Program.client.CurrentUser;                
            }

            // Limite de criação por usuário
            if (pedinte != null && VerificarUsuário(pedinte.Id) >= Program.config.ChannelManager_MaxTempPerUser)
            {
                return null; 
            }

            DiscordChannel createdChannel = await Program.servidor.CreateVoiceChannelAsync(TemplateDeNome + nome, CategoriaCanaisTemp);

            novo.canal = createdChannel;
            novo.canalId = createdChannel.Id;

            canaisTemporários.Add(novo);

            return novo;
        }

        public async Task<RegistroDeCanal> NovoCanalTemporárioPrivado(DateTime lifespan, DiscordUser pedinte, string nome = "🕙 Canal Temporário")
        {
            RegistroDeCanal novo = new()
            {
                lifespan = lifespan
            };

            novo.pedinteId = pedinte.Id;
            novo.pedinte = pedinte;

            // Limite de criação por usuário
            if (pedinte != null && VerificarUsuário(pedinte.Id) >= Program.config.ChannelManager_MaxTempPerUser)
            {
                return null;
            }

            DiscordMember member = await Program.servidor.GetMemberAsync(pedinte.Id);            

            DiscordOverwriteBuilder everyone = new(Program.servidor.EveryoneRole);

            everyone.Deny(Permissions.SendMessages);
            everyone.Deny(Permissions.UseVoice);

            DiscordOverwriteBuilder owner = new(member);

            owner.For(member);
            owner.Allow(Permissions.UseVoice);
            owner.Allow(Permissions.MoveMembers);            

            IEnumerable<DiscordOverwriteBuilder> overwrites = [everyone, owner];

            DiscordChannel createdChannel = await Program.servidor.CreateVoiceChannelAsync(TemplateDeNome + nome, CategoriaCanaisTemp, overwrites: overwrites);

            novo.canal = createdChannel;
            novo.canalId = createdChannel.Id;

            canaisTemporários.Add(novo);

            return novo;
        }

        // Verifica quantos canais temporarios o usuário enviado criou
        public int VerificarUsuário(ulong userId)
        {
            int retorno = 0;
            foreach (RegistroDeCanal reg in canaisTemporários)
            {
                if (reg.pedinteId == userId)
                {
                    retorno++;
                }
            }
            return retorno;
        }


        private async Task RemoverCanalTemporário(RegistroDeCanal regCanal)
        {            
            if (!regCanal.Initialized)
                await regCanal.Init();

            if (Program.servidor.GetChannel(regCanal.canalId) == null)
                return;

            if (regCanal.canal.Type == ChannelType.Voice && regCanal.canal.Users.Count == 0)
            {
                canaisTemporários.Remove(regCanal);
                await regCanal.canal.DeleteAsync("Removendo canal temporário");                
            }
        }

        
        public async void Loop()
        {
            // Remover canais temporários que já passaram da hora            
            foreach (RegistroDeCanal regCanal in canaisTemporários.ToList())
            {
                if (Program.GetTime().Ticks >= regCanal.lifespan.Ticks)
                {
                    await RemoverCanalTemporário(regCanal);                    
                }
            }
        }

        public async Task Init()
        {
            Console.WriteLine("(ChannelManager) Inicializando");

            canaisTemporários = new();
            CategoriaCanaisTemp = await Program.client.GetChannelAsync(CategoriaCanaisTempId);

            // Carregar dados salvos
            if (File.Exists(dataPath))
            {
                Console.WriteLine("(ChannelManager) Dados salvos encontrados");

                GerenciadorDeCanal tmp = await DataIO.Load(dataPath, typeof(GerenciadorDeCanal)) as GerenciadorDeCanal;

                canaisTemporários = tmp.canaisTemporários;

                Console.WriteLine("(ChannelManager) Carregando dados salvos");
            }
            else
            {
                Console.WriteLine("(ChannelManager) Dados salvos não foram encontrados, indo com dados padrão de acordo com a configuração");
            }

            // Inicializar canais no registro
            foreach (RegistroDeCanal canal in canaisTemporários)
            {
                await canal.Init();
            }

            Console.WriteLine("(ChannelManager) Fim da inicialização");
        }

        public async void SaveInstance()
        {
            Console.WriteLine("(ChannelManager) Inicializando gravação dos dados");

            await DataIO.Write(dataPath, this);

            Console.WriteLine("(ChannelManager) Dados gravados");
        }

        public class RegistroDeCanal
        {
            public ulong canalId;
            public ulong pedinteId; // Quem pediu para criar o canal temporário. Caso seja o próprio Bot, foi por outro comando
            public DateTime lifespan; // Quando esse canal deve ser destruído de novo.            

            [JsonIgnore] public DiscordChannel canal = null;
            [JsonIgnore] public DiscordUser pedinte = null;

            [JsonIgnore] public bool Initialized => canal != null && pedinte != null;
            [JsonIgnore] public bool IsValid => Program.servidor.GetChannel(canalId) != null;

            public async Task Init()
            {
                if (Initialized)
                    return;

                canal = await Program.client.GetChannelAsync(canalId);
                pedinte = await Program.client.GetUserAsync(pedinteId);
            }
        }

    }
}