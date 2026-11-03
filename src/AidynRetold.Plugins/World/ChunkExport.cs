using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.World;

/// <summary>
/// Writes one chunk of a map set as a single glTF a modeller opens (docs/editing-the-world.md): under a root node named
/// for the chunk, the chunk's scenery and its see-through scenery as they are in their assets, the collision as a mesh
/// whose materials are its surfaces and roles (<see cref="CollisionMaterials"/>), and a node per map object at its place,
/// carrying the object's fields in its extras, with the model a scene object places under it to look at.
/// <see cref="ChunkImport"/> reads the file back into a mod.
/// </summary>
public static class ChunkExport
{
    /// <summary>The root node's name is this and the chunk's name: <c>chunk M07</c>.</summary>
    public const string RootPrefix = "chunk ";

    /// <summary>The nodes under the root, by name.</summary>
    public const string SceneryNode = "scenery", AltSceneryNode = "scenery alt", CollisionNode = "collision", ObjectsNode = "objects";

    /// <summary>A scene object's model, under its node: <c>model aidyn:scene/0x0CF6</c>.</summary>
    public const string ModelPrefix = "model ";

    /// <summary>The root's extras: which chunk of which set this is, with the <see cref="ChunkEntry"/>'s fields beside them.</summary>
    public const string MapSetKey = "mapSet", KindKey = "aidyn", ChunkKind = "chunk";

    /// <summary>A scenery node's extras: the scene's own extras, as JSON text, which the import puts back on the scene it writes.</summary>
    public const string SceneExtrasKey = "sceneExtras", SceneIdKey = "scene";

    /// <summary>The map set's chunk at <paramref name="name"/>, such as <c>M07</c> or <c>13,7</c>, or null.</summary>
    public static ChunkEntry? FindChunk(MapSetRecord set, string name)
    {
        var text = name.Trim();
        if (text.Length >= 2 && char.IsAsciiLetter(text[0]) && int.TryParse(text.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var column))
            return set.Chunk(char.ToUpperInvariant(text[0]) - 'A' + 1, column);
        var parts = text.Split(',');
        return parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var col) ? set.Chunk(row, col) : null;
    }

    /// <summary>The map set <paramref name="name"/> names: its record ID, its number, or its label (<c>Overworld</c>, case aside), or null.</summary>
    public static MapSetRecord? FindMapSet(PluginSet plugins, string name)
    {
        var text = name.Trim();
        if (RecordId.TryParse(text, out var id, out _) && plugins.TryGet<MapSetRecord>(id) is { } byId)
            return byId;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return plugins.TryGet<MapSetRecord>(RecordId.Parse($"aidyn:{RecordTypes.MapSet}/{number}"));
        return plugins.All<MapSetRecord>(RecordTypes.MapSet).FirstOrDefault(s => string.Equals(s.Label, text, StringComparison.OrdinalIgnoreCase));
    }

    public static byte[] Export(PluginSet plugins, MapSetRecord set, ChunkEntry chunk)
    {
        var gltf = new GltfBuilder { Generator = "acre chunk export" };
        var children = new JsonArray();

        foreach (var (id, name) in new[] { (chunk.Scene, SceneryNode), (chunk.AltScene, AltSceneryNode) })
        {
            if (id is { } scene && plugins.FindAsset(scene, "glb") is { } path)
                children.Add(Scenery(gltf, File.ReadAllBytes(path), scene, name));
        }

        var collision = CollisionMesh.For(plugins, chunk.Map);
        children.Add(Collision(gltf, collision));

        var map = plugins.TryGet<MapRecord>(chunk.Map);
        var objects = new JsonArray();
        var copiers = new Dictionary<RecordId, GltfCopy?>();
        try
        {
            foreach (var o in map?.Objects ?? [])
                objects.Add(Object(gltf, plugins, o, copiers));
        }
        finally
        {
            foreach (var copier in copiers.Values)
                copier?.Source.Dispose();
        }
        children.Add(gltf.AddNode(new JsonObject { ["name"] = ObjectsNode, ["children"] = objects }));

        var extras = JsonSerializer.SerializeToNode(chunk, PluginJson.Options)!.AsObject();
        extras[KindKey] = ChunkKind;
        extras[MapSetKey] = set.Id.ToString();
        var root = gltf.AddNode(new JsonObject { ["name"] = RootPrefix + chunk.Name, ["children"] = children, ["extras"] = extras });
        return gltf.ToGlb([root], new JsonObject { [KindKey] = ChunkKind, ["cellSize"] = set.CellSize });
    }

    /// <summary>The scene's whole file under one node, with the scene's own extras kept as text on it.</summary>
    private static int Scenery(GltfBuilder gltf, byte[] glb, RecordId scene, string name)
    {
        using var reader = GlbReader.Parse(glb);
        var roots = GltfCopy.Copy(reader, gltf, GltfCopy.SceneRoots(reader));
        var extras = new JsonObject { [SceneIdKey] = scene.ToString() };
        if (reader.Scene is { } s && s.TryGetProperty("extras", out var sceneExtras))
            extras[SceneExtrasKey] = sceneExtras.GetRawText();
        return gltf.AddNode(new JsonObject
        {
            ["name"] = name,
            ["children"] = new JsonArray(roots.Select(r => (JsonNode)r).ToArray()),
            ["extras"] = extras,
        });
    }

    /// <summary>
    /// The collision as one mesh of shared vertices, a primitive per material, the materials being the triangles' surfaces
    /// and roles. Without normals, so a viewer shades it flat; the import takes none.
    /// </summary>
    private static int Collision(GltfBuilder gltf, IReadOnlyList<CollisionTriangle> triangles)
    {
        var node = new JsonObject { ["name"] = CollisionNode };
        if (triangles.Count == 0)
            return gltf.AddNode(node);

        var vertices = new List<Vector3>();
        var indexOf = new Dictionary<Vector3, int>();
        int Vertex(Vector3 v)
        {
            if (!indexOf.TryGetValue(v, out var i))
            {
                indexOf[v] = i = vertices.Count;
                vertices.Add(v);
            }
            return i;
        }
        var byKind = new Dictionary<CollisionMaterials.Kind, List<int>>();
        foreach (var t in triangles)
        {
            var kind = CollisionMaterials.Kind.Of(t);
            if (!byKind.TryGetValue(kind, out var indices))
                byKind[kind] = indices = [];
            indices.Add(Vertex(t.A));
            indices.Add(Vertex(t.B));
            indices.Add(Vertex(t.C));
        }
        var positions = gltf.AddPositions(vertices);
        var primitives = new JsonArray();
        foreach (var (kind, indices) in byKind.OrderBy(k => k.Key.Surface).ThenBy(k => k.Key.Role).ThenBy(k => k.Key.Target))
        {
            primitives.Add(new JsonObject
            {
                ["attributes"] = new JsonObject { ["POSITION"] = positions },
                ["indices"] = gltf.AddIndices(indices, vertices.Count),
                ["material"] = gltf.AddMaterial(CollisionMaterials.Material(kind)),
            });
        }
        node["mesh"] = gltf.AddMesh(new JsonObject { ["name"] = CollisionNode, ["primitives"] = primitives });
        return gltf.AddNode(node);
    }

    /// <summary>The node's name for a map object: its kind's discriminator and its index, <c>scene 12</c>.</summary>
    public static string ObjectName(MapObject o) => $"{Discriminator(o)} {o.Index.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The <c>type</c> a map object is written with.</summary>
    public static string Discriminator(MapObject o) => MapObjectKinds.All.First(k => k.Type == o.GetType()).Discriminator;

    /// <summary>
    /// A map object as a node at its position, a scene object turned and scaled as the game draws it, with every other field
    /// in its extras and, for a scene object, its nearest model that draws anything under a <see cref="ModelPrefix"/> node.
    /// </summary>
    private static int Object(GltfBuilder gltf, PluginSet plugins, MapObject o, Dictionary<RecordId, GltfCopy?> copiers)
    {
        var json = JsonSerializer.SerializeToNode(o, typeof(MapObject), PluginJson.Options)!.AsObject();
        json.Remove("index");
        json.Remove("position");
        var node = new JsonObject
        {
            ["name"] = ObjectName(o),
            ["translation"] = new JsonArray(o.Position.X, o.Position.Y, o.Position.Z),
        };
        if (o is SceneObject scene)
        {
            json.Remove("rotation");
            json.Remove("scale");
            var q = scene.Orientation();
            if (q != Quaternion.Identity)
                node["rotation"] = new JsonArray(q.X, q.Y, q.Z, q.W);
            if (scene.Scale != Vector3.One)
                node["scale"] = new JsonArray(scene.Scale.X, scene.Scale.Y, scene.Scale.Z);
            if (scene.Models.Take(Math.Max(1, scene.Lods)).FirstOrDefault(m => !m.Empty) is { } model && Model(gltf, plugins, model.Model, copiers) is { } modelNode)
                node["children"] = new JsonArray(modelNode);
        }
        node["extras"] = json;
        return gltf.AddNode(node);
    }

    private static int? Model(GltfBuilder gltf, PluginSet plugins, RecordId model, Dictionary<RecordId, GltfCopy?> copiers)
    {
        if (!copiers.TryGetValue(model, out var copier))
        {
            // The readers stay open for the export: a copier reads its file's buffers as each copy asks.
            copiers[model] = copier = plugins.FindAsset(model, "glb") is { } path ? new GltfCopy(GlbReader.Parse(File.ReadAllBytes(path)), gltf) : null;
        }
        if (copier is null)
            return null;
        var roots = copier.Copy(copier.SceneRoots());
        return gltf.AddNode(new JsonObject
        {
            ["name"] = ModelPrefix + model,
            ["children"] = new JsonArray(roots.Select(r => (JsonNode)r).ToArray()),
        });
    }
}
