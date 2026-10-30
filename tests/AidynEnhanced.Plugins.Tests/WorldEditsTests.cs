using System.Numerics;
using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.Records;
using AidynEnhanced.Plugins.World;

namespace AidynEnhanced.Plugins.Tests;

/// <summary>The editor's edits to a map's objects, and their saving into a mod (docs/editing-the-world.md).</summary>
public sealed class WorldEditsTests
{
    private static readonly RecordId MapId = RecordId.Parse("aidyn:map/0x0001");

    private static MapRecord Map() => new(MapId,
    [
        new LightObject(0, new Vector3(1, 0, 1), 5, PakVariant.Any, null, new Rgba(255, 255, 255, 255), new Rgba(255, 255, 255, 255), LightKind.Static, 1, false),
        new SavePointObject(2, new Vector3(4, 0, 4), 3, PakVariant.Any, null),
    ]);

    [Fact]
    public void Changes_show_in_place_removals_go_and_copies_come_after()
    {
        var map = Map();
        var edits = new WorldEdits();
        Assert.False(edits.Dirty);
        Assert.Same(map.Objects, edits.Objects(map));

        edits.Set(map, map.Objects[0] with { Position = new Vector3(2, 0, 2) });
        var copy = edits.Duplicate(map, map.Objects[1]);
        Assert.Equal(3, copy.Index);
        edits.Remove(map, 2);

        var objects = edits.Objects(map);
        Assert.Equal([0, 3], objects.Select(o => o.Index));
        Assert.Equal(2, objects[0].Position.X);
        Assert.True(edits.IsEdited(MapId, 0));
        Assert.True(edits.IsEdited(MapId, 3));
        Assert.False(edits.IsEdited(MapId, 2));
        Assert.Equal(3, edits.Count);

        // Put back as the record has it, it's no longer an edit; a copy removed is simply gone.
        edits.Set(map, map.Objects[0]);
        edits.Remove(map, 3);
        Assert.Equal([0], edits.Objects(map).Select(o => o.Index));
        Assert.Single(edits.Maps);
        Assert.Equal(1, edits.Count);
    }

    [Fact]
    public void Saving_writes_each_map_s_patch_against_the_plugins_below_the_mod_and_clears()
    {
        using var temp = new TempPlugins();
        var dir = temp.Add("aidyn", null,
            ("mapsets.json", """[{ "id": "aidyn:mapset/0", "label": "Test", "cellSize": 100, "chunks": [{ "row": 1, "column": 1, "map": "aidyn:map/0x0001", "terrain": 0 }] }]"""),
            ("maps.json", """
                [{ "id": "aidyn:map/0x0001", "objects": [
                  { "type": "light", "index": 0, "position": [1, 0, 1], "radius": 5, "pak": "Any", "from": [255, 255, 255, 255], "to": [255, 255, 255, 255], "kind": "Static", "speed": 1 },
                  { "type": "savePoint", "index": 2, "position": [4, 0, 4], "radius": 3, "pak": "Any" }
                ] }]
                """));
        var mod = ModFolder.OpenOrCreate(temp.Root, "edited");
        var plugins = PluginSet.Load([dir]);
        var map = plugins.Get<MapRecord>(MapId);

        var edits = new WorldEdits();
        edits.Set(map, map.Objects[0] with { Position = new Vector3(2, 0, 2) });
        edits.Remove(map, 2);
        var report = edits.Save(plugins, mod);
        Assert.Single(report);
        Assert.Contains("1 moved", report[0]);
        Assert.Contains("1 removed", report[0]);
        Assert.False(edits.Dirty);

        var loaded = PluginSet.Load([dir, mod.Directory]);
        Assert.Empty(loaded.Validate().Problems);
        var after = loaded.Get<MapRecord>(MapId);
        Assert.Equal([0], after.Objects.Select(o => o.Index));
        Assert.Equal(2, after.Objects[0].Position.X);
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(mod.Directory, "records", "maps", "Test.json")))!.AsArray();
        Assert.Single(file);

        // A second round of edits, made over the installed mod, keeps the first's.
        var again = new WorldEdits();
        var merged = loaded.Get<MapRecord>(MapId);
        again.Set(merged, merged.Objects[0] with { Radius = 9 });
        again.Save(loaded, mod);
        var twice = PluginSet.Load([dir, mod.Directory]).Get<MapRecord>(MapId);
        Assert.Equal([0], twice.Objects.Select(o => o.Index));
        Assert.Equal(2, twice.Objects[0].Position.X);
        Assert.Equal(9, twice.Objects[0].Radius);
    }
}
