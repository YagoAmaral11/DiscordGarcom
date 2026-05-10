using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GarçomDoKitts.Shell.Core;

namespace GarçomDoKitts.Shell.IO;

// Salva dados e configurações em arquivos JSON
// TODO: Verificar se o arquivo existe antes de ler; No momento, pode gerar exceções caso não exista
// OBS: Ou adicionar uma verificação mais rígida aqui ou depois criar um gerador de configuração padrão
public class FileSystem : IPersistance, IConfigPersistance
{
    public static readonly string DataFolderPath = "data/";
    public static readonly string ConfigFolderPath = "config/";
    public static readonly JsonSerializerOptions serializerOptions = new()
    {
        WriteIndented = true,                          
        Converters = { new TimeZoneInfoConverter() } 
    };

    private async Task Write(string ToWrite, string acessKey, string fileExtension = ".json")
    {
        if (!Directory.Exists(Path.GetDirectoryName(acessKey + fileExtension)))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(acessKey + fileExtension));
        }

        using (StreamWriter sw = new(acessKey + fileExtension))
        {
            await sw.WriteAsync(ToWrite);
        }
    }

    private async Task<string> Read(string acessKey, string fileExtension = ".json")
    {
        if (!File.Exists(acessKey + fileExtension))
        {
            return "File not found";
        }

        string content;
        using (StreamReader sr = new(acessKey + fileExtension))
        {
            content = await sr.ReadToEndAsync();
        }
        return content;
    }

    public async Task WriteObject(object ToWrite, Type objectType, string acessKey)
    {
        string json = JsonSerializer.Serialize(ToWrite, objectType, serializerOptions);
        await WriteJSON(json, acessKey);
    }

    public async Task WriteJSON(string ToWrite, string acessKey) => await Write(ToWrite, DataFolderPath + acessKey);    

    public async Task<object> ReadObject(string acessKey, Type objectType)
    {
        string json = await ReadJSON(acessKey);
        return JsonSerializer.Deserialize(json, objectType, serializerOptions);
    }

    public async Task<string> ReadJSON(string acessKey) => await Read(DataFolderPath + acessKey);        

    public async Task WriteConfig(object module, object config)
    {
        if (module is not IModule)
        {
            throw new ArgumentException("Module must implement IModule interface");
        }

        IModule moduleInterface = module as IModule;
        string json = JsonSerializer.Serialize(config, config.GetType(), serializerOptions);
        await Write(json, ConfigFolderPath + moduleInterface.Name);
    }

    public async Task<object> LoadConfig(object module, Type configType)
    {
        if (module is not IModule)
        {
            throw new ArgumentException("Module must implement IModule interface");
        }

        IModule moduleInterface = module as IModule;
        string json = await Read(ConfigFolderPath + moduleInterface.Name);
        return JsonSerializer.Deserialize(json, configType, serializerOptions);
    }

    public async Task WriteRaw(string ToWrite, string acessKey) => await Write(ToWrite, acessKey, String.Empty);    

    public async Task<string> ReadRaw(string acessKey) => await Read(acessKey, String.Empty);

    public Task<bool> KeyExists(string acessKey) => Task.FromResult(File.Exists(DataFolderPath + acessKey));

    public Task<bool> ConfigExists(IModule module) => Task.FromResult(File.Exists(ConfigFolderPath + module.Name + ".json"));

}