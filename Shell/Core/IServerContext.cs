using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace GarçomDoKitts.Shell.Core;

public interface IServerContext
{
    public DiscordGuild connectedServer { get; }
    public DiscordClient shellDiscordClient { get; }
    public DiscordUser shellDiscordUser { get; }
}