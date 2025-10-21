using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GarçomDoKitts.Shell;
using GarçomDoKitts.Shell.IO;

namespace GarçomDoKitts.Shell.Core;

public static class Init
{
    public static void Main()
    {

        FileSystem fileSystem = new();
        CoreShell garcKittsShell = new(fileSystem, new(), 832773492738490448);

    }
}