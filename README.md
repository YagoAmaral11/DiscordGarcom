# DiscordGarçom

DiscordGarçom is a personal modular Discord bot built in C# using DSharpPlus. The project is designed around a container that loads independent modules, each one responsible for a specific feature, such as chat phrases, temporary voice channels, party management, and music playback.

It was created as a bot framework for a specific Discord server, but the architecture is reusable: you can add new modules, configure persistence, and register commands without rewriting the whole bot lifecycle. DiscordGarçom contains:
- a central runtime container, `SimpleContainer`, that boots the bot and registers modules. 
- a modular architecture where each feature lives in its own class
- command registration via DSharpPlus commands and `CommandBuilder`
- JSON-based persistence for module data and configuration
- support for scheduling recurring tasks and background automation with `CoreScheduler`

## Some Modules

The default build includes these modules:

- `Frases`: reads quotes or messages from a source channel and periodically sends them to a broadcast channel.
- `Party`: manages custom matches, score tracking, and team-based match flows.
- `Utility`: provides convenience commands for voice channels, member counting, mention utilities, and user movement.
- `Jukebox`: plays music through with queue and playback controls, using Lavalink4Net.
- `CoreChannelManager`: creates and cleans up temporary voice channels.
- `CoreScheduler`: schedules callbacks and repeating work.
- `CoreBackuper`: performs backups of module data.

## Running the bot

## Creating custom modules

## Requirements

- .NET 9 SDK
- a Discord bot token
- a valid guild/server ID for `LinkedServerID`
- a connected Lavalink instance if you want to use the music module