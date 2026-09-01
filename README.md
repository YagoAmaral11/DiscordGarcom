# DiscordGarçom
DiscordGarçom is an simple personal discord bot made in C# based on DSharpPlus framework. You can use it as base for your own bot or use it directly. It features:
- a central runtime container, `SimpleContainer`, that boots the bot and manages the modules. 
- JSON-based persistence for data and configuration
- support for scheduling timed, future and recurring tasks with `CoreScheduler`
- support for temporary voice channels with `CoreChannelManager`
- an easy to use and extend modular architecture

## Some Modules

The default build includes these modules:

- `Frases`: reads quotes or messages from a source channel and periodically sends them to a broadcast channel.
- `Party`: manages custom matches, score tracking, and team-based match flows. You can use it to easily create and separate teams into different voice chats.
- `Utility`: provides commands for things like member counting, member mention, user movement, etc.
- `Jukebox`: plays music in a voice chat using Lavalink.

## Running the bot

First you need to get a discord bot token and connect it to your discord server. Create an folder called `data` in the running directory, then create a file called `BotToken` with the token as plaintext and `CommandPrefixes.json`, a json containing all your bot's command prefixes, for example: 
```json
[
    "!",
    "#",
    "$"    
]
``` 

Now all you need is an entry point to the bot. Create an main file (or use [Init](Containers\Init.cs)), instantiate all the modules the bot will be using and create SimpleContainer, passing an persistance like `FileSystem`, a array of modules and a ulong of the discord server id you want the bot to run on. 

**IMPORTANT**: At the moment, both the SimpleContainer and the default modules dont suport your bot to be on multiple server.

## Creating custom modules

You can create your own modules simply by creating an child class inheriting BaseModule. The BaseModule already handles persistance, configuration and also has helper methods for you. Give it an unique name, create your commands and return then 
If you dont want to inherit BaseModule, then you need to implement IModule.

See [ExampleModule](Containers/Core/Modules/Examples/ExampleModule.cs) for more details