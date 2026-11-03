using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins;

/// <summary>A record or asset in a <see cref="PluginCatalog"/>.</summary>
/// <param name="HasRecord">Whether a plugin defines the record. Scenes, textures and UI images are assets alone.</param>
/// <param name="Assets">Its asset files, by extension.</param>
/// <param name="Summary">A line about it for the editor's list, such as a map set's name and size.</param>
public sealed record CatalogEntry(RecordId Id, bool HasRecord, IReadOnlyList<AssetFile> Assets, string Summary)
{
    /// <summary>
    /// The Borg a base-game record or asset was imported from, for those keyed by archive index, and a model texture's texture's
    /// (which a model's key leads, in any namespace).
    /// </summary>
    public int? Borg => RecordTypes.Info(Id.Type).Key switch
    {
        KeyKind.Borg when Id.Namespace == RecordId.RomNamespace => int.Parse(Id.Key.AsSpan(2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        KeyKind.ModelAndTexture => int.Parse(Id.Key.AsSpan(Id.Key.LastIndexOf('/') + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
        _ => null,
    };

    public AssetFile? Asset(string extension) => Assets.FirstOrDefault(a => a.Extension == extension);
}

/// <summary>Where a record names another: the naming record, and the field's path in it, such as <c>objects[3].models[0].model</c>.</summary>
public sealed record RecordReference(RecordId From, string Path);

/// <summary>
/// What the editor browses in a <see cref="PluginSet"/>: every record and asset, with a summary
/// line each, and which records name which (Bethesda's "use info"). Any string field that parses
/// as a record ID counts as a reference, so new record types need no code here.
/// </summary>
public sealed class PluginCatalog
{
    private readonly Dictionary<RecordId, CatalogEntry> _entries;
    private readonly Dictionary<RecordId, List<RecordReference>> _references;

    private PluginCatalog(PluginSet plugins, Dictionary<RecordId, CatalogEntry> entries, Dictionary<RecordId, List<RecordReference>> references)
    {
        Plugins = plugins;
        _entries = entries;
        _references = references;
        Entries = [.. entries.Values.OrderBy(e => e.Id.Type, StringComparer.Ordinal).ThenBy(e => e.Id.Namespace, StringComparer.Ordinal)
            .ThenBy(e => e.Id.Key.Length).ThenBy(e => e.Id.Key, StringComparer.Ordinal)];
    }

    public PluginSet Plugins { get; }

    /// <summary>By type, then namespace, then key.</summary>
    public IReadOnlyList<CatalogEntry> Entries { get; }

    public IEnumerable<string> Types => Entries.Select(e => e.Id.Type).Distinct();

    public CatalogEntry? Find(RecordId id) => _entries.GetValueOrDefault(id);

    /// <summary>The records that name <paramref name="id"/>, whether or not it exists.</summary>
    public IReadOnlyList<RecordReference> ReferencesTo(RecordId id) => _references.GetValueOrDefault(id) ?? [];

    /// <summary>Reads every record and lists every asset. The ROM plugin takes a second or two.</summary>
    public static PluginCatalog Build(PluginSet plugins)
    {
        var references = new Dictionary<RecordId, List<RecordReference>>();
        foreach (var id in plugins.Ids)
        {
            foreach (var (field, value) in plugins.GetJson(id)!)
            {
                if (field != "id")
                    FindReferences(id, field, value, references);
            }
        }

        var assets = plugins.Assets().ToLookup(a => a.Id);
        var ids = plugins.Ids.Concat(assets.Select(g => g.Key)).ToHashSet();
        var placements = MapPlacements.For(plugins);
        var whose = Whose(plugins);
        var entries = new Dictionary<RecordId, CatalogEntry>();
        foreach (var id in ids)
        {
            var files = assets[id].OrderBy(a => a.Extension, StringComparer.Ordinal).ToList();
            var json = plugins.GetJson(id);
            string summary;
            try
            {
                summary = Summary(plugins, id, json, files, placements, references.GetValueOrDefault(id)?.Count ?? 0, whose.GetValueOrDefault(id));
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException or IOException)
            {
                summary = $"‹{e.Message}›";
            }
            entries[id] = new CatalogEntry(id, json is not null, files, summary);
        }
        return new PluginCatalog(plugins, entries, references);
    }

    /// <summary>
    /// Who an actor or a model is, for its summary, so it can be found by name: an actor by the characters whose body it is (entities
    /// and dialogue entities), a scene by the actors it's the model or head of, under their characters' names, and by the items
    /// whose model it is. A name is given once, and after the first three the rest are counted.
    /// </summary>
    private static Dictionary<RecordId, string> Whose(PluginSet plugins)
    {
        static RecordId? Id(JsonNode? node) => (string?)node is { } s && RecordId.TryParse(s, out var id) ? id : null;
        var wearers = new Dictionary<RecordId, List<string>>();
        var models = new Dictionary<RecordId, List<string>>();
        void Add(Dictionary<RecordId, List<string>> to, RecordId? key, string? name)
        {
            if (key is not { } k || string.IsNullOrWhiteSpace(name))
                return;
            if (!to.TryGetValue(k, out var list))
                to[k] = list = [];
            if (!list.Contains(name))
                list.Add(name);
        }
        // Entities before dialogue entities, each by ID, so a name's place doesn't change from one load to the next.
        foreach (var id in plugins.Ids.OrderBy(i => i.Type == RecordTypes.Entity ? 0 : 1).ThenBy(i => i.ToString(), StringComparer.Ordinal))
        {
            var json = plugins.GetJson(id)!;
            switch (id.Type)
            {
                case RecordTypes.Entity or RecordTypes.DialogEntity:
                    Add(wearers, Id(json["body"]?["actor"]), (string?)json["name"]);
                    break;
                case RecordTypes.Weapon or RecordTypes.Armor or RecordTypes.Item:
                    Add(models, Id(json["model"]), (string?)json["name"]);
                    break;
            }
        }
        foreach (var id in plugins.Ids.Where(i => i.Type == RecordTypes.Actor))
        {
            var json = plugins.GetJson(id)!;
            var who = wearers.TryGetValue(id, out var names) ? Names(names) : $"actor {id.Key}";
            Add(models, Id(json["scene"]), who);
            Add(models, Id(json["head"]?["scene"]), $"{who}'s head");
        }
        return wearers.Select(w => (w.Key, Names(w.Value))).Concat(models.Select(m => (m.Key, Names(m.Value)))).ToDictionary(p => p.Key, p => p.Item2);
    }

    private static string Names(List<string> names) => names.Count <= 3 ? string.Join(", ", names) : $"{string.Join(", ", names.Take(3))} and {names.Count - 3} more";

    private static void FindReferences(RecordId from, string path, JsonNode? node, Dictionary<RecordId, List<RecordReference>> references)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, value) in o)
                    FindReferences(from, $"{path}.{key}", value, references);
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                    FindReferences(from, $"{path}[{i}]", a[i], references);
                break;
            case JsonValue v when v.TryGetValue(out string? text) && text.Contains(':', StringComparison.Ordinal) && RecordId.TryParse(text, out var to):
                if (!references.TryGetValue(to, out var list))
                    references[to] = list = [];
                list.Add(new RecordReference(from, path));
                break;
        }
    }

    /// <summary>
    /// What a map object is, such as "Monster party: Male Commoner" or "Teleporter patioL2": its kind's name
    /// (<see cref="MapObjectKinds"/>), and the one detail that tells it from others of its kind.
    /// </summary>
    public string Describe(MapObject? o) => o switch
    {
        DialogueObject d => $"Dialogue object {d.Name}",
        MonsterPartyObject m => $"Monster party: {Find(m.Entity)?.Summary ?? m.Entity.ToString()}",
        TeleporterObject t => $"Teleporter {t.Name}",
        ContainerObject c => c.Kind.ToString(),
        TriggerObject t => $"{t.Kind} trigger",
        ReferencePointObject r => $"Reference point {r.Name}",
        SceneObject s => s.Models.Count > 0 ? $"Scene object: {s.Models[0].Model}" : "Scene object",
        { } other => MapObjectKinds.Name(other),
        null => "Object",
    };

    /// <summary>
    /// What a voxel ID names and where, such as "Teleporter patioL2, in JundarInteriors K07". Where is every chunk that
    /// places its map, joined by " / ": the battle arenas and a few overworld maps are placed by several.
    /// </summary>
    public string Describe(RecordId voxel)
    {
        var map = MapRecord.MapOf(voxel);
        var places = MapPlacements.For(Plugins).All(map);
        var where = places.Count > 0 ? string.Join(" / ", places) : $"map {map.Key}";
        return $"{Describe(MapRecord.Find(Plugins, voxel))}, in {where}";
    }

    /// <param name="whose">Who the actor or model is (<see cref="Whose"/>), which leads its summary.</param>
    private static string Summary(PluginSet plugins, RecordId id, JsonObject? json, List<AssetFile> files, MapPlacements placements, int usedBy, string? whose)
    {
        // A record named in this one, by its name or label, or its key.
        string Name(JsonNode? named) => (string?)named is { } s && RecordId.TryParse(s, out var r)
            ? (string?)plugins.GetJson(r)?["name"] ?? (string?)plugins.GetJson(r)?["label"] ?? r.Key : "?";
        var parts = new List<string>();
        if (whose is not null && id.Type is RecordTypes.Actor or RecordTypes.Scene)
            parts.Add(whose);
        switch (id.Type)
        {
            case RecordTypes.MapSet when json is not null:
                parts.Add($"{(string?)json["label"]}, {Count(json["chunks"], "chunk")}");
                break;
            case RecordTypes.Map:
                parts.AddRange(placements.All(id).Select(p => p.ToString()));
                if (json?["objects"] is JsonArray objects)
                {
                    parts.Add(objects.Count == 0 ? "no objects" : string.Join(", ", objects.OfType<JsonObject>()
                        .GroupBy(o => (string?)o["type"] ?? "?").Select(g => Plural(g.Count(), MapObjectKinds.Name(g.Key).ToLowerInvariant()))));
                }
                break;
            case RecordTypes.Actor when json is not null:
                parts.Add($"{Count(json["states"], "state")}, scene {Key(json["scene"])}");
                if (json["head"] is JsonObject head)
                    parts.Add($"head {Key(head["scene"])}");
                break;
            case RecordTypes.Terrain when json is not null:
                parts.Add($"sky {Key(json["sky"])}" + ((bool?)json["skyDrawn"] == false ? ", not drawn" : ""));
                break;
            case RecordTypes.Flag when json is not null:
                if (json["formula"] is JsonObject formula)
                    parts.Add("derived: " + formula.Deserialize<Records.FlagFormula>(PluginJson.Options)!.Describe());
                else if ((bool?)json["initial"] == true)
                    parts.Add("starts set");
                if ((string?)json["label"] is { } label)
                    parts.Add(label);
                if (json["journal"] is { } journal)
                    parts.Add($"journal entry {Key(journal)}");
                // A flag nothing names by a label is known by what reads or sets it.
                if (parts.Count == 0)
                    parts.Add(usedBy == 0 ? "used by nothing" : $"used by {Plural(usedBy, "record")}");
                break;
            case RecordTypes.Journal when json is not null:
                parts.Add($"flag {Key(json["flag"])}: {Truncate((string?)json["text"] ?? "", 80)}");
                break;
            case RecordTypes.Dialogue when json is not null:
                var nodes = json["nodes"] as JsonArray ?? [];
                var first = nodes.OfType<JsonObject>().Select(n => (string?)n["text"]).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
                parts.Add($"{(string?)json["mode"]}, {Count(nodes, "node")}" + (first is null ? "" : $": {Truncate(first.Replace("\n", " ", StringComparison.Ordinal), 60)}"));
                break;
            case RecordTypes.Entity or RecordTypes.DialogEntity or RecordTypes.Weapon or RecordTypes.Armor or RecordTypes.Spell
                or RecordTypes.Item or RecordTypes.Loot when json is not null:
                parts.Add((string?)json["name"] ?? "");
                break;
            case RecordTypes.Stat when json is not null:
                parts.Add($"{(string?)json["name"]}, cap {(int?)json["baseCap"]}/{(int?)json["cap"]}");
                break;
            case RecordTypes.Skill or RecordTypes.WeaponSkill when json is not null:
                parts.Add($"{(string?)json["name"]}, training cost {(int?)json["trainingCost"]}");
                break;
            case RecordTypes.Progression when json is not null:
                parts.Add(Count(json["levels"], "level"));
                break;
            case RecordTypes.Sky when json is not null:
                parts.Add($"{Count(json["stars"], "star")}, {Count(json["clouds"], "cloud")}, {Count(json["lensFlare"], "flare piece")}");
                break;
            case RecordTypes.Cinematic when json is not null:
                parts.Add($"{(string?)json["title"]}, {Count(json["shots"], "shot")}");
                break;
            case RecordTypes.Shop when json is not null:
                parts.Add($"{Name(json["shopkeeper"])}, {Count(json["stock"], "item")} in stock");
                break;
            case RecordTypes.Trigger when json is not null:
                var effects = (json["do"] as JsonArray ?? []).OfType<JsonObject>().Select(e => (string?)e["type"]).Distinct();
                parts.Add($"on {(string?)json["on"]}: {string.Join(", ", effects)}");
                break;
            case RecordTypes.Recipe when json is not null:
                parts.Add($"{Name(json["product"])}, {Name(json["skill"])} {(int?)json["level"]}, {Count(json["ingredients"], "ingredient")}");
                break;
            case RecordTypes.Strings when json is not null:
                parts.Add($"{(string?)json["name"]}, {Count(json["lines"], "line")}");
                break;
            case RecordTypes.Font when json is not null:
                parts.Add($"{(json["glyphs"] as JsonObject)?.Count ?? 0} characters, cells {(int?)json["cellWidth"]}×{(int?)json["cellHeight"]}");
                break;
            case RecordTypes.Minimap when json is not null:
                parts.Add($"{Name(json["mapSet"])}, {Count(json["sections"], "section")}");
                break;
            case RecordTypes.Emitters when json is not null:
                parts.Add(Count(json["emitters"], "emitter"));
                break;
            case RecordTypes.Sequence when json is not null:
                parts.Add($"{Plural((int?)json["tracks"] ?? 0, "track")}, {Count(json["instruments"], "instrument")}");
                break;
            case RecordTypes.Combat when json is not null:
                parts.Add($"allies, bystanders, fleeing, missiles, {Count(json["bosses"], "boss")}");
                break;
            case RecordTypes.Magic when json is not null:
                parts.Add($"casting chance, resisting, spells up to level {(int?)json["maxLevel"]}");
                break;
            case RecordTypes.Camping when json is not null:
                parts.Add($"ambushes, heals {(double?)json["heal"] * 100:0}%, camp scenes");
                break;
            case RecordTypes.Credits when json is not null:
                parts.Add(Count(json["cards"], "card"));
                break;
            case RecordTypes.Weather when json is not null:
                parts.Add("the seasons' rain, fog and wind");
                break;
            case RecordTypes.NewGame when json is not null:
                parts.Add($"{Name(json["leader"])} in {Name(json["chunk"]?["mapSet"])}, {Plural((int?)json["gold"] ?? 0, "gold")}");
                break;
            case RecordTypes.Title when json is not null:
                parts.Add($"{Count(json["flycams"], "fly-through")} behind the menu");
                break;
            case RecordTypes.WorldMap when json is not null:
                parts.Add($"{Count(json["pieces"], "piece")}, {Count(json["towns"], "town")}");
                break;
            case RecordTypes.Inventory when json is not null:
                parts.Add($"{(int?)json["generalSlots"]} slots, stacks of {(int?)json["stackLimit"]}, sells at {(double?)json["sellShare"] * 100:0}%");
                break;
            case RecordTypes.Sounds when json is not null:
                parts.Add("menus, scripts, gold, containers, weather");
                break;
            case RecordTypes.StateFlags when json is not null:
                parts.Add("time, season, weather, terrain");
                break;
            case RecordTypes.Explosion when json is not null:
                parts.Add($"texture {Key(json["texture"])}");
                break;
        }
        if (files.FirstOrDefault(f => f.Extension == "png") is { } png && Assets.PngHeader.Size(png.Path) is var (w, h))
            parts.Add($"{w}×{h}");
        if (json is null && (parts.Count == 0 || id.Type == RecordTypes.Scene))
            parts.Add(usedBy == 0 ? "used by nothing" : $"used by {Plural(usedBy, "record")}");
        return string.Join(", ", parts);
    }

    /// <summary>The text cut to <paramref name="length"/> characters, with an ellipsis where it was.</summary>
    public static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";

    private static string Key(JsonNode? id) => (string?)id is { } s && RecordId.TryParse(s, out var r) ? r.Key : "?";

    private static string Count(JsonNode? list, string one) => Plural((list as JsonArray)?.Count ?? 0, one);

    private static string Plural(int n, string one) => string.Create(CultureInfo.InvariantCulture,
        $"{n:N0} {(n == 1 || one == "gold" ? one : one.EndsWith('y') ? one[..^1] + "ies" : one.EndsWith('s') ? one + "es" : one + "s")}");
}
