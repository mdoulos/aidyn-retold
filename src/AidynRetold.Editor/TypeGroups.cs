using AidynRetold.Plugins;

namespace AidynRetold.Editor;

/// <summary>A record type as the editor names it: its ID's spelling, and a plain name.</summary>
public sealed record TypeName(string Type, string Name);

/// <summary>A heading the editor lists types under, such as World or Items.</summary>
public sealed record TypeGroup(string Name, IReadOnlyList<TypeName> Types);

/// <summary>
/// The record types under headings, as the start page lists them (editor.md#plugins), with a plain name for each, which a
/// one-of-a-kind record's page is titled by: a type the editor doesn't know, a newer one or a mod's, goes under Other by its ID's
/// spelling.
/// </summary>
public static class TypeGroups
{
    public static IReadOnlyList<TypeGroup> All { get; } =
    [
        new("World",
        [
            new(RecordTypes.MapSet, "Map sets"), new(RecordTypes.Map, "Map chunks"), new(RecordTypes.Voxel, "Map objects"),
            new(RecordTypes.Terrain, "Terrains"), new(RecordTypes.Minimap, "Minimaps"), new(RecordTypes.WorldMap, "World map"),
            new(RecordTypes.Sky, "Sky"), new(RecordTypes.Weather, "Weather"), new(RecordTypes.Trigger, "Triggers"),
        ]),
        new("Characters",
        [
            new(RecordTypes.Entity, "Characters and monsters"), new(RecordTypes.DialogEntity, "Simple speakers"),
            new(RecordTypes.Stat, "Attributes"), new(RecordTypes.Skill, "Skills"), new(RecordTypes.WeaponSkill, "Weapon skills"),
            new(RecordTypes.Progression, "Levels and training"),
        ]),
        new("Items",
        [
            new(RecordTypes.Weapon, "Weapons"), new(RecordTypes.Armor, "Armor"), new(RecordTypes.Item, "Items"),
            new(RecordTypes.Loot, "Loot tables"), new(RecordTypes.Shop, "Shops"), new(RecordTypes.Recipe, "Recipes"),
            new(RecordTypes.Inventory, "Inventory rules"),
        ]),
        new("Magic and combat",
        [
            new(RecordTypes.Spell, "Spells"), new(RecordTypes.Magic, "Magic rules"), new(RecordTypes.Combat, "Combat rules"),
            new(RecordTypes.Camping, "Camping"), new(RecordTypes.Explosion, "Trap explosion"),
        ]),
        new("Story",
        [
            new(RecordTypes.Dialogue, "Scripts"), new(RecordTypes.Flag, "Flags"), new(RecordTypes.Journal, "Journal entries"),
            new(RecordTypes.StateFlags, "Engine flags"), new(RecordTypes.Cinematic, "Cinematics"), new(RecordTypes.NewGame, "New game"),
            new(RecordTypes.Title, "Title screen"), new(RecordTypes.Credits, "Credits"), new(RecordTypes.Strings, "Text tables"),
        ]),
        new("Models and art",
        [
            new(RecordTypes.Actor, "Actors"), new(RecordTypes.Scene, "Models"), new(RecordTypes.Animation, "Animations"),
            new(RecordTypes.Texture, "Textures"), new(RecordTypes.ModelTexture, "Model textures"), new(RecordTypes.Image, "UI images"),
            new(RecordTypes.Font, "Fonts"), new(RecordTypes.Emitters, "Particle emitters"),
        ]),
        new("Sound",
        [
            new(RecordTypes.Sequence, "Sounds and music"), new(RecordTypes.Sample, "Samples"), new(RecordTypes.Sounds, "Engine sounds"),
        ]),
    ];

    private static readonly Dictionary<string, string> Names = All.SelectMany(g => g.Types).ToDictionary(t => t.Type, t => t.Name);

    private static readonly Dictionary<string, string> Ones = new()
    {
        [RecordTypes.Texture] = "Texture", [RecordTypes.ModelTexture] = "Model texture", [RecordTypes.Image] = "UI image",
        [RecordTypes.Animation] = "Animation", [RecordTypes.Sample] = "Sample",
    };

    /// <summary>
    /// What one of an asset type is called, as a page's title has it with the key, such as Texture 0x0001: for the types whose summary
    /// is only a size or a count, so says nothing of what it is; null for the rest.
    /// </summary>
    public static string? One(string type) => Ones.GetValueOrDefault(type);

    /// <summary>A type's plain name, or its ID's spelling if the editor doesn't know it.</summary>
    public static string Name(string type) => Names.GetValueOrDefault(type, type);

    /// <summary>
    /// The groups as they are for these types, the ones the plugins have: each group's types in its order, the empty groups left out,
    /// and the types no group has under Other.
    /// </summary>
    public static IReadOnlyList<TypeGroup> For(IEnumerable<string> types)
    {
        var present = types.ToHashSet();
        var groups = All.Select(g => g with { Types = [.. g.Types.Where(t => present.Contains(t.Type))] }).Where(g => g.Types.Count > 0).ToList();
        var other = present.Where(t => !Names.ContainsKey(t)).Order(StringComparer.Ordinal).Select(t => new TypeName(t, t)).ToList();
        if (other.Count > 0)
            groups.Add(new TypeGroup("Other", other));
        return groups;
    }
}
