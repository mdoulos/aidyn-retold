using System.Text.Json.Nodes;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Plugins.World;

/// <summary>
/// The map objects as the editor has changed them since the plugins were loaded: a working copy of each touched map's
/// objects, by map, which the editor's map view shows in place of the record's and <see cref="Save"/> writes into a mod as patches
/// (<see cref="MapPatch"/>, docs/editing-the-world.md). The edits outlive a reload of the plugins until they're saved or
/// dropped, and are applied again to the records by map and index.
/// </summary>
public sealed class WorldEdits
{
    private readonly Dictionary<RecordId, Dictionary<int, MapObject?>> _changes = [];

    /// <summary>Whether anything has been changed and not saved.</summary>
    public bool Dirty => _changes.Count > 0;

    /// <summary>How many objects have been changed, added or removed, over every map.</summary>
    public int Count => _changes.Values.Sum(m => m.Count);

    /// <summary>The maps touched.</summary>
    public IEnumerable<RecordId> Maps => _changes.Keys;

    /// <summary>The map's objects with the edits applied: changed ones in place, removed ones gone, added ones after.</summary>
    public IReadOnlyList<MapObject> Objects(MapRecord map)
    {
        if (!_changes.TryGetValue(map.Id, out var changes))
            return map.Objects;
        var result = new List<MapObject>(map.Objects.Count);
        foreach (var o in map.Objects)
        {
            if (!changes.TryGetValue(o.Index, out var changed))
                result.Add(o);
            else if (changed is not null)
                result.Add(changed);
        }
        var present = map.Objects.Select(o => o.Index).ToHashSet();
        result.AddRange(changes.Where(c => !present.Contains(c.Key) && c.Value is not null).Select(c => c.Value!).OrderBy(o => o.Index));
        return result;
    }

    /// <summary>The object at an index with the edits applied, or null if it's removed or isn't there.</summary>
    public MapObject? Object(MapRecord map, int index) => Objects(map).FirstOrDefault(o => o.Index == index);

    /// <summary>Whether the object is one of the edits: changed, or added.</summary>
    public bool IsEdited(RecordId map, int index) => _changes.TryGetValue(map, out var changes) && changes.TryGetValue(index, out var o) && o is not null;

    /// <summary>Puts an object, changed or new, in its map. One that's back as the record has it is no longer an edit.</summary>
    public void Set(MapRecord map, MapObject o)
    {
        var original = map.Objects.FirstOrDefault(x => x.Index == o.Index);
        if (original is not null && JsonNode.DeepEquals(MapPatch.ToJson(original), MapPatch.ToJson(o)))
        {
            Forget(map.Id, o.Index);
            return;
        }
        Changes(map.Id)[o.Index] = o;
    }

    /// <summary>Takes an object out of its map. One the record never had is simply forgotten.</summary>
    public void Remove(MapRecord map, int index)
    {
        if (map.Objects.Any(x => x.Index == index))
            Changes(map.Id)[index] = null;
        else
            Forget(map.Id, index);
    }

    /// <summary>Adds a copy of an object to its map, at the next free index, and returns it.</summary>
    public MapObject Duplicate(MapRecord map, MapObject o)
    {
        var index = Objects(map).Select(x => x.Index).Concat(map.Objects.Select(x => x.Index)).DefaultIfEmpty(-1).Max() + 1;
        var copy = o with { Index = index };
        Changes(map.Id)[index] = copy;
        return copy;
    }

    /// <summary>Drops every edit.</summary>
    public void Clear() => _changes.Clear();

    /// <summary>
    /// Writes every touched map's patch into the mod, each against the record as the plugins below the mod have it, with the
    /// grid worked out again where anything it lists moved, and drops the edits. Returns a line per map.
    /// </summary>
    public IReadOnlyList<string> Save(PluginSet plugins, ModFolder mod)
    {
        var below = mod.Without(plugins);
        var placements = MapPlacements.For(plugins);
        var report = new List<string>();
        foreach (var mapId in _changes.Keys.ToList())
        {
            var merged = plugins.Get<MapRecord>(mapId);
            var baseMap = below.TryGet<MapRecord>(mapId) ?? new MapRecord(mapId, []);
            var result = MapPatch.Build(baseMap, Objects(merged), CollisionMesh.For(plugins, mapId));
            var file = Path.Combine("maps", (placements.First(mapId)?.Set.Label ?? "maps") + ".json");
            if (result.Patch is not null)
                mod.WriteRecord(file, result.Patch);
            else
                mod.RemoveRecord(file, mapId);
            report.Add($"{placements.First(mapId)?.ToString() ?? mapId.ToString()}: {result.Summary}");
        }
        _changes.Clear();
        return report;
    }

    private Dictionary<int, MapObject?> Changes(RecordId map)
    {
        if (!_changes.TryGetValue(map, out var changes))
            _changes[map] = changes = [];
        return changes;
    }

    private void Forget(RecordId map, int index)
    {
        if (!_changes.TryGetValue(map, out var changes))
            return;
        changes.Remove(index);
        if (changes.Count == 0)
            _changes.Remove(map);
    }
}
