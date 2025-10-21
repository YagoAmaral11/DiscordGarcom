using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Shell.Core;

public interface IPersistance
{

    public Task WriteJSON(string ToWrite, string acessKey);
    public Task WriteObject(object ToWrite, Type objectType, string acessKey);
    public Task WriteRaw(string ToWrite, string acessKey);
    public Task<string> ReadJSON(string acessKey);
    public Task<object> ReadObject(string acessKey, Type objectType);
    public Task<string> ReadRaw(string acessKey);    

}