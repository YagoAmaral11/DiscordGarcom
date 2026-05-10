using System;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core;

public interface IPersistance
{

    public Task<bool> KeyExists(string acessKey);

    public Task WriteJSON(string ToWrite, string acessKey);
    public Task WriteObject(object ToWrite, Type objectType, string acessKey);
    public Task WriteRaw(string ToWrite, string acessKey, string fileExtension = "");

    public Task<string> ReadJSON(string acessKey);
    public Task<object> ReadObject(string acessKey, Type objectType);
    public Task<string> ReadRaw(string acessKey, string fileExtension = "");

}