using DiscordGarçom.Containers.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace DiscordGarçom.Containers.IO;

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



    public Task<bool> KeyExists(string acessKey) => Task.FromResult(File.Exists(DataFolderPath + acessKey));
    public Task<bool> ConfigExists(IModule module) => Task.FromResult(File.Exists(ConfigFolderPath + module.Name + ".json"));



    // Escreve a string ToWrite em um arquivo acessKey.fileExtension (padrão .json); Se a pasta não existir, ela é criada
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

    // Serializa o objeto ToWrite e o escreve em um arquivo data/acessKey.json; Se a pasta não existir, ela é criada
    public async Task WriteObject(object ToWrite, Type objectType, string acessKey)
    {
        string json = JsonSerializer.Serialize(ToWrite, objectType, serializerOptions);
        await WriteJSON(json, acessKey);
    }

    // Escreve a string ToWrite em um arquivo data/acessKey.json; Se a pasta não existir, ela é criada
    public async Task WriteJSON(string ToWrite, string acessKey) => await Write(ToWrite, DataFolderPath + acessKey);

    // Escreve a string ToWrite em um arquivo acessKey.fileExtension; Se a pasta não existir, ela é criada
    public async Task WriteRaw(string ToWrite, string acessKey, string fileExtension = "") => await Write(ToWrite, acessKey, fileExtension);

    // Usado para salvar as configurações de um módulo; Serializa o objeto config e o escreve em um arquivo config/ModuleName.json; Se a pasta não existir, ela é criada
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



    // Lê a string contida em um arquivo acessKey.fileExtension (padrão .json); Se o arquivo não existir, retorna "File not found"
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

    // Lê a string contida em um arquivo data/acessKey.json e a desserializa em um objeto do tipo objectType; Se o arquivo não existir, retorna null
    public async Task<object> ReadObject(string acessKey, Type objectType)
    {
        string json = await ReadJSON(acessKey);

        if (json == "File not found")
            return null;

        return JsonSerializer.Deserialize(json, objectType, serializerOptions);
    }

    // Lê a string contida em um arquivo data/acessKey.json; Se o arquivo não existir, retorna "File not found"
    public async Task<string> ReadJSON(string acessKey) => await Read(DataFolderPath + acessKey);

    // Lê a string contida em um arquivo acessKey.fileExtension; Se o arquivo não existir, retorna "File not found"
    public async Task<string> ReadRaw(string acessKey, string fileExtension = "") => await Read(acessKey, fileExtension);

    // Usado para carregar as configurações de um módulo; Lê a string contida em um arquivo config/ModuleName.json e a desserializa em um objeto do tipo configType; Se o arquivo não existir, retorna null
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
    
}