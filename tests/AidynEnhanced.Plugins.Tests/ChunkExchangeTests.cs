using System.Numerics;
using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;
using AidynEnhanced.Plugins.World;

namespace AidynEnhanced.Plugins.Tests;

/// <summary>A chunk out to a glTF and back into a mod, over a small hand-written world (docs/editing-the-world.md).</summary>
public sealed class ChunkExchangeTests : IDisposable
{
    private readonly TempPlugins _temp = new();
    private readonly PluginSet _base;
    private readonly MapSetRecord _set;
    private readonly ChunkEntry _chunk;

    public ChunkExchangeTests()
    {
        var dir = _temp.Add("aidyn", null,
            ("mapsets.json", """
                [{ "id": "aidyn:mapset/0", "label": "Test", "cellSize": 100,
                   "chunks": [{ "row": 1, "column": 1, "scene": "aidyn:scene/0x0001", "map": "aidyn:map/0x0001", "terrain": 0 }] }]
                """),
            ("maps.json", """
                [{ "id": "aidyn:map/0x0001", "objects": [
                  { "type": "container", "index": 0, "position": [2, 0, 2], "radius": 10, "pak": "Any", "kind": "Chest",
                    "lockLevel": 0, "trapLevel": 0, "gold": 5, "reach": 2, "link": "aidyn:voxel/0x0001/1" },
                  { "type": "scene", "index": 1, "position": [2, 0, 2], "radius": 30, "pak": "Any",
                    "visible": { "flag": "aidyn:flag/0", "invert": true },
                    "models": [{ "model": "aidyn:scene/0x0002", "drawDistance": 30 }], "lods": 1,
                    "rotation": [0, 1.5, 0], "scale": [1, 1, 1], "tint": [0, 0, 0, 0], "flags": "None" },
                  { "type": "light", "index": 2, "position": [8, 1, 8], "radius": 6, "pak": "Any",
                    "visible": { "flag": "aidyn:flag/0", "invert": true },
                    "from": [255, 200, 100, 255], "to": [255, 200, 100, 255], "kind": "Static", "speed": 1 }
                ] }]
                """));
        var collision = CollisionRules.WithFlags(
        [
            new CollisionTriangle(new(0, 0, 0), new(0, 0, 10), new(10, 0, 10), 4, CollisionRole.Normal, CollisionFlags.None),
            new CollisionTriangle(new(0, 0, 0), new(10, 0, 10), new(10, 0, 0), 4, CollisionRole.Normal, CollisionFlags.None),
        ]);
        Write(dir, "map", "0x0001", CollisionMesh.Write(collision, "test"));
        Write(dir, "scene", "0x0001", GltfCopyTests.SampleGlb());
        Write(dir, "scene", "0x0002", GltfCopyTests.SampleGlb());
        _base = PluginSet.Load([dir]);
        _set = _base.Get<MapSetRecord>(RecordId.Parse("aidyn:mapset/0"));
        _chunk = _set.Chunks[0];
    }

    private static void Write(string dir, string type, string key, byte[] bytes)
    {
        var path = Path.Combine(dir, "assets", "aidyn", type, key + ".glb");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void The_export_holds_the_scenery_the_collision_by_material_and_a_node_per_object()
    {
        var glb = ChunkExport.Export(_base, _set, _chunk);
        var json = TestGlb.Json(glb);
        var nodes = json["nodes"]!.AsArray();
        var root = nodes[json["scenes"]![0]!["nodes"]![0]!.GetValue<int>()]!;
        Assert.Equal("chunk A01", root["name"]!.GetValue<string>());
        Assert.Equal("aidyn:mapset/0", root["extras"]![ChunkExport.MapSetKey]!.GetValue<string>());
        var children = root["children"]!.AsArray().Select(c => nodes[c!.GetValue<int>()]!.AsObject()).ToList();
        Assert.Equal([ChunkExport.SceneryNode, ChunkExport.CollisionNode, ChunkExport.ObjectsNode], children.Select(c => c["name"]!.GetValue<string>()));

        // The scenery is the sample file: its two roots, with the scene's extras kept as text.
        Assert.Equal(2, children[0]["children"]!.AsArray().Count);
        Assert.Contains(ExtrasKeys.AmbientLight, children[0]["extras"]![ChunkExport.SceneExtrasKey]!.GetValue<string>());

        // One collision primitive, its material Grass, four welded vertices.
        var mesh = json["meshes"]![children[1]["mesh"]!.GetValue<int>()]!;
        var primitive = Assert.Single(mesh["primitives"]!.AsArray())!;
        Assert.Equal("collision Grass", json["materials"]![primitive["material"]!.GetValue<int>()]!["name"]!.GetValue<string>());
        using var reader = GlbReader.Parse(glb);
        Assert.Equal(4 * 3, reader.Floats(primitive["attributes"]!["POSITION"]!.GetValue<int>()).Length);
        Assert.Equal(6, reader.Indices(primitive["indices"]!.GetValue<int>()).Length);

        // The objects: the scene object turned, with its model under it; the container's fields in its extras.
        var objects = children[2]["children"]!.AsArray().Select(c => nodes[c!.GetValue<int>()]!.AsObject()).ToList();
        Assert.Equal(["container 0", "scene 1", "light 2"], objects.Select(o => o["name"]!.GetValue<string>()));
        Assert.Equal(5, objects[0]["extras"]!["gold"]!.GetValue<int>());
        Assert.Equal("Chest", objects[0]["extras"]!["kind"]!.GetValue<string>());
        Assert.Null(objects[0]["extras"]!["position"]);
        Assert.NotNull(objects[1]["rotation"]);
        Assert.Null(objects[1]["extras"]!["rotation"]);
        var model = nodes[objects[1]["children"]![0]!.GetValue<int>()]!;
        Assert.Equal("model aidyn:scene/0x0002", model["name"]!.GetValue<string>());
    }

    [Fact]
    public void An_untouched_file_writes_nothing()
    {
        var mod = ModFolder.OpenOrCreate(_temp.Root, "untouched");
        var result = ChunkImport.Import(_base, ChunkExport.Export(_base, _set, _chunk), mod);
        Assert.Null(result.Patch);
        Assert.False(result.SceneryWritten);
        Assert.False(result.CollisionWritten);
        Assert.False(Directory.Exists(Path.Combine(mod.Directory, "assets")));
        Assert.False(Directory.Exists(Path.Combine(mod.Directory, "records")));
    }

    [Fact]
    public void Edits_come_back_as_the_assets_and_the_patch_that_changed()
    {
        var glb = ChunkExport.Export(_base, _set, _chunk);
        var json = TestGlb.Json(glb);
        var nodes = json["nodes"]!.AsArray();
        JsonObject Node(string name) => nodes.OfType<JsonObject>().Single(n => n["name"]?.GetValue<string>() == name);

        // The ground becomes stone, with the material renamed as a modeller would, and a copy's suffix on it.
        json["materials"]!.AsArray().OfType<JsonObject>().Single(m => m["name"]?.GetValue<string>() == "collision Grass")["name"] = "collision Stone.001";
        // The chest moves, the light goes, and a second tree is a copy of the first, placed elsewhere.
        Node("container 0")["translation"] = new JsonArray(7f, 0f, 2f);
        var objects = Node(ChunkExport.ObjectsNode)["children"]!.AsArray();
        var light = nodes.IndexOf(Node("light 2"));
        objects.Remove(objects.Single(c => c!.GetValue<int>() == light));
        var copy = (JsonObject)Node("scene 1").DeepClone();
        copy["name"] = "scene 1.001";
        copy["translation"] = new JsonArray(5f, 0f, 5f);
        copy.Remove("children");
        nodes.Add(copy);
        objects.Add(nodes.Count - 1);

        var mod = ModFolder.OpenOrCreate(_temp.Root, "edited");
        var result = ChunkImport.Import(_base, TestGlb.WithJson(glb, json), mod);

        Assert.True(result.CollisionWritten);
        Assert.False(result.SceneryWritten);
        var collision = CollisionMesh.Read(File.ReadAllBytes(mod.AssetPath(_chunk.Map, "glb")));
        Assert.Equal(2, collision.Count);
        Assert.All(collision, t => Assert.Equal(11, t.Surface));
        // The first triangle claims the shared edge and every vertex it uses; the second gets its own two edges and corner.
        var claimed = collision.Select(t => t.Flags).ToList();
        Assert.Contains(CollisionFlags.EdgeAB | CollisionFlags.EdgeBC | CollisionFlags.EdgeCA | CollisionFlags.VertexA | CollisionFlags.VertexB | CollisionFlags.VertexC, claimed);
        Assert.Contains(claimed, f => (f & (CollisionFlags.EdgeAB | CollisionFlags.EdgeBC | CollisionFlags.EdgeCA)) is not (CollisionFlags.EdgeAB | CollisionFlags.EdgeBC | CollisionFlags.EdgeCA)
            && (f & (CollisionFlags.VertexA | CollisionFlags.VertexB | CollisionFlags.VertexC)) != CollisionFlags.None);

        var patch = result.Patch!;
        Assert.Equal("aidyn:map/0x0001", patch["patch"]!.GetValue<string>());
        var changes = patch["objects"]!.AsArray().OfType<JsonObject>().ToList();
        var moved = changes.Single(c => c["index"]!.GetValue<int>() == 0);
        Assert.Equal(2, moved.Count);
        Assert.Equal(7f, moved["position"]![0]!.GetValue<float>());
        var removed = changes.Single(c => c["index"]!.GetValue<int>() == 2);
        Assert.True(removed[MergePatch.RemoveField]!.GetValue<bool>());
        var added = changes.Single(c => c["index"]!.GetValue<int>() == 3);
        Assert.Equal("scene", added["type"]!.GetValue<string>());
        Assert.Equal(5f, added["position"]![0]!.GetValue<float>());
        Assert.Equal(1.5f, added["rotation"]![1]!.GetValue<float>(), 1e-4f);
        Assert.NotNull(patch["grid"]);

        // The mod loads over the base and reads clean, with the world as edited.
        var loaded = PluginSet.Load([Path.Combine(_temp.Root, "aidyn"), mod.Directory]);
        Assert.Empty(loaded.Validate().Problems);
        var map = loaded.Get<MapRecord>(_chunk.Map);
        Assert.Equal([0, 1, 3], map.Objects.Select(o => o.Index).Order());
        Assert.Equal(7f, map.Objects.Single(o => o.Index == 0).Position.X);
        Assert.Equal(new Vector3(5, 0, 5), map.Objects.Single(o => o.Index == 3).Position);
        Assert.All(CollisionMesh.For(loaded, _chunk.Map), t => Assert.Equal(11, t.Surface));
        Assert.NotNull(map.Grid);
        Assert.Contains(3, map.Grid!.At(new Vector3(5, 0, 5)));

        // Importing again replaces the earlier patch rather than adding to it.
        var again = ChunkImport.Import(_base, TestGlb.WithJson(glb, json), mod);
        Assert.NotNull(again.Patch);
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(mod.Directory, "records", "maps", "Test.json")))!.AsArray();
        Assert.Single(file);
    }

    [Fact]
    public void A_field_the_game_can_t_read_names_its_node()
    {
        var glb = ChunkExport.Export(_base, _set, _chunk);
        var json = TestGlb.Json(glb);
        var chest = json["nodes"]!.AsArray().OfType<JsonObject>().Single(n => n["name"]?.GetValue<string>() == "container 0");
        chest["extras"]!["kind"] = "Wardrobe";
        var mod = ModFolder.OpenOrCreate(_temp.Root, "bad");
        var error = Assert.Throws<InvalidDataException>(() => ChunkImport.Import(_base, TestGlb.WithJson(glb, json), mod));
        Assert.StartsWith("container 0:", error.Message);
    }

    [Fact]
    public void Loosened_numbers_read_as_the_fields_want()
    {
        var glb = ChunkExport.Export(_base, _set, _chunk);
        var json = TestGlb.Json(glb);
        var chest = json["nodes"]!.AsArray().OfType<JsonObject>().Single(n => n["name"]?.GetValue<string>() == "container 0");
        chest["extras"]!["gold"] = 9.0;
        chest["extras"]!["reachable"] = 1;
        var light = json["nodes"]!.AsArray().OfType<JsonObject>().Single(n => n["name"]?.GetValue<string>() == "light 2");
        light["extras"]!["from"] = new JsonArray(10.0, 20.0, 30.0, 255.0);
        var mod = ModFolder.OpenOrCreate(_temp.Root, "loose");
        var result = ChunkImport.Import(_base, TestGlb.WithJson(glb, json), mod);
        var changes = result.Patch!["objects"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(9, changes.Single(c => c["index"]!.GetValue<int>() == 0)["gold"]!.GetValue<int>());
        Assert.True(changes.Single(c => c["index"]!.GetValue<int>() == 0)["reachable"]!.GetValue<bool>());
        Assert.Equal(10, changes.Single(c => c["index"]!.GetValue<int>() == 2)["from"]![0]!.GetValue<int>());
    }
}
