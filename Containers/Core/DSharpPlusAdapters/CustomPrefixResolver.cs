using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GarçomDoKitts.Containers.Core.DSharpPlusAdapters;

/// <summary>
/// Adiciona um construtor extra para o DefaultPrefixResolver, permitindo que uma coleção de prefixes seja passada.
/// </summary>
public sealed class CustomPrefixResolver : IPrefixResolver
{

    /// <summary>
    /// Prefixes which will trigger command execution
    /// </summary>
    public string[] Prefixes { get; init; }

    /// <summary>
    /// Setting if a mention will trigger command execution
    /// </summary>
    public bool AllowMention { get; init; }

    /// <summary>
    /// Default prefix resolver
    /// </summary>
    /// <param name="allowMention">Set wether mentioning the bot will count as a prefix</param>
    /// <param name="prefix">A list of prefixes which will trigger commands</param>
    /// <exception cref="ArgumentException">Is thrown when no prefix is provided or any prefix is null or whitespace only</exception>
    public CustomPrefixResolver(bool allowMention, params string[] prefix)
    {
        if (prefix.Length == 0 || prefix.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Prefix cannot be null or whitespace.", nameof(prefix));
        }

        this.AllowMention = allowMention;
        this.Prefixes = prefix;
    }

    /// <summary>
    /// Extensão do construtor que aceita uma lista de prefixes
    /// </summary>
    /// <param name="allowMention">Set wether mentioning the bot will count as a prefix</param>
    /// <param name="prefix">A list of prefixes which will trigger commands</param>
    /// <exception cref="ArgumentException">Is thrown when no prefix is provided or any prefix is null or whitespace only</exception>
    public CustomPrefixResolver(bool allowMention, List<string> prefix)
    {
        if (prefix.Count == 0 || prefix.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Prefix cannot be null or whitespace.", nameof(prefix));
        }

        this.AllowMention = allowMention;
        this.Prefixes = prefix.ToArray();
    }

    public ValueTask<int> ResolvePrefixAsync(CommandsExtension extension, DiscordMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Content))
        {
            return ValueTask.FromResult(-1);
        }
        // Mention check
        else if (this.AllowMention && message.Content.StartsWith(extension.Client.CurrentUser.Mention, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(extension.Client.CurrentUser.Mention.Length);
        }

        foreach (string prefix in this.Prefixes)
        {
            if (message.Content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(prefix.Length);
            }
        }

        return ValueTask.FromResult(-1);
    }

}