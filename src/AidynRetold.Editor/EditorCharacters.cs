using System.Globalization;
using AidynRetold.Core;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The characters the editor works with: an entity's sheet as its record starts it, or a copy of
/// the editor's game's party to try things on.
/// </summary>
public static class EditorCharacters
{
    /// <summary>The character rules, or null if the plugins are older than the characters' records.</summary>
    public static CharacterRules? Rules(PluginSet plugins)
    {
        try
        {
            return CharacterRules.From(plugins);
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The items, or null if the plugins are older than the item records.</summary>
    public static ItemCatalog? Items(PluginSet plugins)
    {
        try
        {
            return ItemCatalog.From(plugins);
        }
        catch (Exception e) when (e is KeyNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>An entity as its record starts it, with its True Name by the editor's flags, wearing its gear.</summary>
    public static Character? Create(PluginSet plugins, CharacterRules rules, RecordId entity, EditorGame? game, ItemCatalog? items) =>
        plugins.TryGet<EntityRecord>(entity) is { } record
            ? Character.Create(record, rules, plugins.Get<SpellRecord>,
                flag => game is not null && game.Network.IsSet(flag), items)
            : null;

    /// <summary>
    /// A copy of the editor's game's party to try things on, with its gold and inventory, or for plugins
    /// too old for a game, a new game's Alaron alone.
    /// </summary>
    public static Party CopyParty(PluginSet plugins, CharacterRules rules, EditorGame? game, ItemCatalog items)
    {
        if (game is not null)
            return game.CopyParty();
        var party = new Party(items);
        if (Create(plugins, rules, GameState.Alaron, null, items) is { } alaron)
            party.Add(alaron);
        return party;
    }

    /// <summary>A member of the editor's party as pages that try things on it name them: "New game Alaron", or a loaded game's member by name.</summary>
    public static string Label(EditorGame? game, Character member) => game is { IsNewGame: false } ? member.Name : $"New game {member.Name}";
}
