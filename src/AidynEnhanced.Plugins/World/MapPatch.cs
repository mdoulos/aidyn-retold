using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Plugins.World;

/// <summary>What a map's patch holds, and how many objects it moved, changed, added or removed.</summary>
/// <param name="Patch">The patch, or null when the objects and the grid are as the base has them.</param>
public sealed record MapPatchResult(JsonObject? Patch, int Moved, int Changed, int Added, int Removed, bool GridChanged)
{
    public string Summary => $"{Moved} moved, {Changed} changed, {Added} added, {Removed} removed" + (GridChanged ? ", grid worked out again" : "");
}

/// <summary>
/// A map record's patch from the objects as a tool has them against the record as the plugins below the mod have it:
/// the fields that differ, object by object (a JSON Merge Patch inside each keyed element), a whole object where the
/// base has none, a removal where the base has one the list lacks, and the grid worked out again where anything it lists
/// changed. The chunk import and the editor write their edits through it.
/// </summary>
public static class MapPatch
{
    private const float Epsilon = 1e-4f;

    /// <summary>
    /// The patch to <paramref name="baseMap"/> that makes its objects <paramref name="objects"/>. An object's position,
    /// rotation or scale within a hair of the base's is kept as the base's, so a float's drift doesn't patch. The grid is
    /// worked out from <paramref name="collision"/> when an object moved, came or went, or when <paramref name="collisionChanged"/>,
    /// and goes in the patch when it differs from the base's.
    /// </summary>
    public static MapPatchResult Build(MapRecord baseMap, IReadOnlyList<MapObject> objects, IReadOnlyList<CollisionTriangle> collision, bool collisionChanged = false)
    {
        var baseObjects = baseMap.Objects.ToDictionary(o => o.Index);
        var patches = new JsonArray();
        var kept = new List<MapObject>(objects.Count);
        int moved = 0, changed = 0, added = 0;
        foreach (var o in objects.OrderBy(o => o.Index))
        {
            if (baseObjects.TryGetValue(o.Index, out var before))
            {
                var now = KeepUnmoved(o, before);
                kept.Add(now);
                var diff = Diff(ToJson(before), ToJson(now));
                if (diff.Count == 0)
                    continue;
                diff["index"] = o.Index;
                patches.Add(diff);
                if (diff.Count == 2 && diff.ContainsKey("position"))
                    moved++;
                else
                    changed++;
            }
            else
            {
                kept.Add(o);
                patches.Add(ToJson(o));
                added++;
            }
        }
        var present = objects.Select(o => o.Index).ToHashSet();
        var removed = 0;
        foreach (var index in baseObjects.Keys.Where(i => !present.Contains(i)).Order())
        {
            patches.Add(new JsonObject { ["index"] = index, [MergePatch.RemoveField] = true });
            removed++;
        }

        var patch = patches.Count == 0 ? null : new JsonObject { ["patch"] = baseMap.Id.ToString(), ["objects"] = patches };
        var gridChanged = false;
        if (patches.Count > 0 || collisionChanged)
        {
            var grid = CollisionRules.Grid(collision, kept);
            if (!SameGrid(baseMap.Grid, grid))
            {
                patch ??= new JsonObject { ["patch"] = baseMap.Id.ToString() };
                patch["grid"] = grid is null ? null : JsonSerializer.SerializeToNode(grid, PluginJson.Options);
                gridChanged = true;
            }
        }
        return new MapPatchResult(patch, moved, changed, added, removed, gridChanged);
    }

    /// <summary>An object as the plugin writes it, for comparing and patching.</summary>
    public static JsonObject ToJson(MapObject o) => JsonSerializer.SerializeToNode(o, typeof(MapObject), PluginJson.Options)!.AsObject();

    /// <summary>The object with the base's position, rotation and scale where its own are within a hair of them.</summary>
    public static MapObject KeepUnmoved(MapObject o, MapObject before)
    {
        if (Vector3.Distance(o.Position, before.Position) < Epsilon)
            o = o with { Position = before.Position };
        if (o is SceneObject s && before is SceneObject b)
        {
            if (MathF.Abs(Quaternion.Dot(s.Orientation(), b.Orientation())) > 1 - 1e-6f)
                s = s with { Rotation = b.Rotation };
            if (Vector3.Distance(s.Scale, b.Scale) < Epsilon)
                s = s with { Scale = b.Scale };
            o = s;
        }
        return o;
    }

    /// <summary>
    /// A JSON Merge Patch from <paramref name="was"/> to <paramref name="now"/>: the fields that differ, null for those gone. A
    /// changed object is a patch of its own, as the merge merges it into the old one, so a field gone from it is gone.
    /// </summary>
    public static JsonObject Diff(JsonObject was, JsonObject now)
    {
        var diff = new JsonObject();
        foreach (var (field, value) in now)
        {
            if (!was.TryGetPropertyValue(field, out var old) || !JsonNode.DeepEquals(old, value))
                diff[field] = old is JsonObject oldObject && value is JsonObject newObject ? Diff(oldObject, newObject) : value?.DeepClone();
        }
        foreach (var (field, _) in was)
        {
            if (!now.ContainsKey(field))
                diff[field] = null;
        }
        return diff;
    }

    private static bool SameGrid(ObjectGrid? a, ObjectGrid? b) =>
        a is null ? b is null : b is not null && JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a, PluginJson.Options), JsonSerializer.SerializeToNode(b, PluginJson.Options));
}
