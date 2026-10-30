using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Plugins.World;

/// <summary>What a chunk's import wrote, and what it found.</summary>
/// <param name="Report">A line per thing done or left, for the person running it.</param>
/// <param name="Patch">The map record's patch written, or null if the objects and grid came back as the base has them.</param>
public sealed record ChunkImportResult(IReadOnlyList<string> Report, bool SceneryWritten, bool AltSceneryWritten, bool CollisionWritten, JsonObject? Patch);

/// <summary>
/// Reads a chunk's glTF back (<see cref="ChunkExport"/>, after a modeller has had it) and writes what changed into a mod
/// (docs/editing-the-world.md): the scenery and the see-through scenery as the mod's own copies of the scene assets, when
/// their geometry moved; the collision as the mod's copy of the map's asset, its flags worked out again
/// (<see cref="CollisionRules"/>); and the map objects that moved, changed, came or went as a patch to the map record, with
/// the grid worked out again where anything it lists moved. A node's copy suffix (<c>.001</c>) is dropped from every name,
/// and a copied object node becomes a new object with the next free index.
/// </summary>
public static class ChunkImport
{
    private const float Epsilon = 1e-4f;

    public static ChunkImportResult Import(PluginSet plugins, byte[] glb, ModFolder mod, bool force = false)
    {
        using var file = GlbReader.Parse(glb);
        var root = file.Root;
        var nodes = root.GetProperty("nodes");
        var report = new List<string>();

        var chunkNode = GltfCopy.SceneRoots(file).Select(i => (Index: i, Node: nodes[i]))
            .FirstOrDefault(n => Extras(n.Node) is { } e && e.TryGetProperty(ChunkExport.KindKey, out var k) && k.ValueKind == JsonValueKind.String && k.GetString() == ChunkExport.ChunkKind);
        if (chunkNode.Node.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("This isn't a chunk export: no root node carries the chunk's extras. Keep the \"chunk\" node at the top of the file.");
        var extras = Extras(chunkNode.Node)!.Value;
        var setId = RecordId.Parse(extras.GetProperty(ChunkExport.MapSetKey).GetString()!);
        var set = plugins.TryGet<MapSetRecord>(setId) ?? throw new InvalidDataException($"No map set is {setId}.");
        var entry = JsonSerializer.Deserialize<ChunkEntry>(extras.GetRawText(), PluginJson.Options)!;
        var chunk = set.Chunk(entry.Row, entry.Column)
            ?? throw new InvalidDataException($"{set.Label} has no chunk {entry.Name}. Adding chunks to a set isn't supported yet: export a chunk the set has.");
        var map = plugins.TryGet<MapRecord>(chunk.Map) ?? new MapRecord(chunk.Map, []);
        report.Add($"{set.Label} {chunk.Name}: map {chunk.Map}");

        // The root may have been moved; everything is read relative to it, so its own transform doesn't count.
        var sceneryWritten = false;
        var altWritten = false;
        var collisionWritten = false;
        IReadOnlyList<CollisionTriangle>? collision = null;
        var objectNodes = new List<(JsonElement Node, Matrix4x4 World)>();

        foreach (var (child, world) in Children(nodes, chunkNode.Node, Matrix4x4.Identity))
        {
            var name = CollisionMaterials.StripCopySuffix(Name(child));
            switch (name)
            {
                case ChunkExport.SceneryNode when chunk.Scene is { } scene:
                    sceneryWritten = Scenery(plugins, mod, file, child, world, scene, force, report);
                    break;
                case ChunkExport.AltSceneryNode when chunk.AltScene is { } alt:
                    altWritten = Scenery(plugins, mod, file, child, world, alt, force, report);
                    break;
                case ChunkExport.CollisionNode:
                    collision = Collision(file, child, world, report);
                    break;
                case ChunkExport.ObjectsNode:
                    objectNodes.AddRange(Children(nodes, child, world));
                    break;
                default:
                    if (ObjectName(name) is not null)
                        objectNodes.Add((child, world));
                    else
                        report.Add($"  {Name(child)}: not part of a chunk, left alone");
                    break;
            }
        }

        var baseCollision = CollisionMesh.For(plugins, chunk.Map);
        if (collision is not null)
        {
            collision = CollisionRules.WithFlags(collision);
            if (!force && SameCollision(baseCollision, collision))
            {
                report.Add("  collision: as the base has it, not written");
                collision = null;
            }
            else
            {
                mod.WriteAsset(chunk.Map, "glb", CollisionMesh.Write(collision, $"{chunk.Name} collision", "aidyn-enhanced chunk import"));
                report.Add($"  collision: {collision.Count} triangles written to {Path.GetRelativePath(mod.Directory, mod.AssetPath(chunk.Map, "glb"))}");
                collisionWritten = true;
            }
        }

        var patch = Objects(map, objectNodes, report, out var objects);
        var grid = CollisionRules.Grid(collision ?? baseCollision, objects);
        if ((patch is not null || collision is not null) && !SameGrid(map.Grid, grid))
        {
            patch ??= new JsonObject { ["patch"] = map.Id.ToString() };
            patch["grid"] = grid is null ? null : JsonSerializer.SerializeToNode(grid, PluginJson.Options);
            report.Add("  grid: worked out again");
        }

        var patchFile = Path.Combine("maps", set.Label + ".json");
        if (patch is not null)
        {
            mod.WriteRecord(patchFile, patch);
            report.Add($"  patch written to records/{patchFile.Replace('\\', '/')}");
        }
        else
        {
            mod.RemoveRecord(patchFile, map.Id);
            report.Add("  objects: as the base has them, no patch");
        }
        return new ChunkImportResult(report, sceneryWritten, altWritten, collisionWritten, patch);
    }

    /// <summary>
    /// The scenery under <paramref name="node"/> as a scene asset of its own, written to the mod when its triangles differ from
    /// the base's or <paramref name="force"/> says to. The node's own transform, if it was moved, is kept over the scene.
    /// </summary>
    private static bool Scenery(PluginSet plugins, ModFolder mod, GlbReader file, JsonElement node, Matrix4x4 world, RecordId scene, bool force, List<string> report)
    {
        var gltf = new GltfBuilder { Generator = "aidyn-enhanced chunk import" };
        var children = node.TryGetProperty("children", out var c) ? c.EnumerateArray().Select(n => n.GetInt32()).ToList() : [];
        var roots = GltfCopy.Copy(file, gltf, children, CollisionMaterials.StripCopySuffix);
        if (!IsIdentity(world))
        {
            var wrapper = new JsonObject { ["name"] = "moved", ["children"] = new JsonArray(roots.Select(r => (JsonNode)r).ToArray()) };
            var m = world;
            wrapper["matrix"] = new JsonArray(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
            roots = [gltf.AddNode(wrapper)];
        }
        JsonObject? sceneExtras = null;
        if (Extras(node) is { } e && e.TryGetProperty(ChunkExport.SceneExtrasKey, out var text) && text.ValueKind == JsonValueKind.String)
            sceneExtras = JsonNode.Parse(text.GetString()!) as JsonObject;
        var bytes = gltf.ToGlb(roots, sceneExtras);
        var what = Name(node);
        if (!force && plugins.FindAsset(scene, "glb") is { } basePath && SameScenery(File.ReadAllBytes(basePath), bytes))
        {
            report.Add($"  {what}: as the base has it, not written");
            return false;
        }
        mod.WriteAsset(scene, "glb", bytes);
        report.Add($"  {what}: written to {Path.GetRelativePath(mod.Directory, mod.AssetPath(scene, "glb"))}");
        return true;
    }

    /// <summary>The same triangles, as a multiset of rounded positions, and the same number of images at the same sizes.</summary>
    private static bool SameScenery(byte[] a, byte[] b)
    {
        static IEnumerable<string> Keys(IReadOnlyList<SceneTriangle> t) =>
            t.Select(x => string.Create(CultureInfo.InvariantCulture, $"{Round(x.A)}|{Round(x.B)}|{Round(x.C)}")).Order(StringComparer.Ordinal);
        static IEnumerable<string> Images(byte[] glb)
        {
            using var reader = GlbReader.Parse(glb);
            if (!reader.Root.TryGetProperty("images", out var images))
                return [];
            return images.EnumerateArray().Select(i => i.TryGetProperty("bufferView", out var v) ? PngHeader.Size(reader.BufferViewBytes(v.GetInt32())) is { } s ? $"{s.Width}x{s.Height}" : "?" : "uri")
                .Order(StringComparer.Ordinal).ToList();
        }
        return Keys(SceneMesh.Read(a)).SequenceEqual(Keys(SceneMesh.Read(b))) && Images(a).SequenceEqual(Images(b));
    }

    private static string Round(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{MathF.Round(v.X, 3)},{MathF.Round(v.Y, 3)},{MathF.Round(v.Z, 3)}");

    /// <summary>The collision mesh's triangles, each with the surface, role and target its material names, and no flags yet.</summary>
    private static IReadOnlyList<CollisionTriangle> Collision(GlbReader file, JsonElement node, Matrix4x4 world, List<string> report)
    {
        var triangles = new List<CollisionTriangle>();
        var unnamed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (meshNode, meshWorld) in Meshes(file.Root.GetProperty("nodes"), node, world))
        {
            var mesh = file.Root.GetProperty("meshes")[meshNode.GetProperty("mesh").GetInt32()];
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                if (primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4)
                    continue;
                var kind = CollisionMaterials.Default;
                if (primitive.TryGetProperty("material", out var m))
                {
                    var material = file.Root.GetProperty("materials")[m.GetInt32()];
                    var name = material.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!CollisionMaterials.TryParse(name, out kind))
                    {
                        kind = CollisionMaterials.Default;
                        unnamed.Add(name);
                    }
                }
                var positions = file.Floats(primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32());
                var count = positions.Length / 3;
                var indices = primitive.TryGetProperty("indices", out var ix) ? file.Indices(ix.GetInt32()) : [.. Enumerable.Range(0, count)];
                for (var i = 0; i + 2 < indices.Length; i += 3)
                {
                    triangles.Add(new CollisionTriangle(
                        Vector3.Transform(Position(positions, indices[i]), meshWorld),
                        Vector3.Transform(Position(positions, indices[i + 1]), meshWorld),
                        Vector3.Transform(Position(positions, indices[i + 2]), meshWorld),
                        kind.Surface, kind.Role, CollisionFlags.None, kind.Target));
                }
            }
        }
        foreach (var name in unnamed)
            report.Add($"  collision: the material \"{name}\" isn't a collision material, so its faces are Default collision; name it \"collision Grass\", \"collision Stone trigger 12\" and so on");
        return triangles;
    }

    private static bool SameCollision(IReadOnlyList<CollisionTriangle> a, IReadOnlyList<CollisionTriangle> b)
    {
        if (a.Count != b.Count)
            return false;
        static IEnumerable<string> Keys(IReadOnlyList<CollisionTriangle> t) =>
            // Not the flags: which triangle claims a shared edge follows the triangles' order, which a modeller's file reorders.
            t.Select(x => string.Create(CultureInfo.InvariantCulture, $"{Round(x.A)}|{Round(x.B)}|{Round(x.C)}|{x.Surface}|{(int)x.Role}|{x.Target}")).Order(StringComparer.Ordinal);
        return Keys(a).SequenceEqual(Keys(b));
    }

    /// <summary>
    /// The objects the nodes hold, each read from its node's name, transform and extras, as a patch to the map's list: the
    /// fields that differ from the base's object of that index, a whole object where the base has none, and a removal where
    /// the base has one no node is. Null if nothing differs. <paramref name="objects"/> is the list as the nodes have it.
    /// </summary>
    private static JsonObject? Objects(MapRecord map, List<(JsonElement Node, Matrix4x4 World)> nodes, List<string> report, out List<MapObject> objects)
    {
        var baseObjects = map.Objects.ToDictionary(o => o.Index);
        var baseJson = map.Objects.ToDictionary(o => o.Index, ToJson);
        var nextIndex = map.Objects.Count == 0 ? 0 : map.Objects.Max(o => o.Index) + 1;
        var patches = new JsonArray();
        var seen = new HashSet<int>();
        objects = [];
        var moved = 0;
        var changed = 0;
        var added = 0;
        foreach (var (node, world) in nodes.OrderBy(n => ObjectName(CollisionMaterials.StripCopySuffix(Name(n.Node)))?.Index ?? int.MaxValue))
        {
            var rawName = Name(node);
            var name = CollisionMaterials.StripCopySuffix(rawName);
            var parsed = ObjectName(name);
            if (parsed is null)
            {
                report.Add($"  {rawName}: not a map object's name (such as \"scene 12\"), left alone");
                continue;
            }
            var (discriminator, index) = parsed.Value;
            var copy = name != rawName || seen.Contains(index);
            if (copy)
                index = nextIndex++;
            seen.Add(index);

            var json = Extras(node) is { } e ? JsonNode.Parse(e.GetRawText())!.AsObject() : new JsonObject();
            json["type"] ??= discriminator;
            json["index"] = index;
            Matrix4x4.Decompose(world, out var scale, out var rotation, out var translation);
            json["position"] = new JsonArray(translation.X, translation.Y, translation.Z);
            var type = RecordSchema.Concrete(typeof(MapObject), json);
            if (type == typeof(SceneObject))
            {
                var r = SceneObject.RotationOf(rotation);
                json["rotation"] = new JsonArray(r.X, r.Y, r.Z);
                json["scale"] = new JsonArray(scale.X, scale.Y, scale.Z);
            }
            JsonShape.Normalise(json, typeof(MapObject));

            MapObject o;
            try
            {
                o = json.Deserialize<MapObject>(PluginJson.Strict) ?? throw new JsonException("empty");
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"{rawName}: {ex.Message.Split(" Path:")[0]}", ex);
            }
            if (!copy && baseObjects.TryGetValue(index, out var before))
                o = KeepUnmoved(o, before);
            objects.Add(o);

            var after = ToJson(o);
            if (!copy && baseJson.TryGetValue(index, out var was))
            {
                var diff = Diff(was, after);
                if (diff.Count > 0)
                {
                    diff["index"] = index;
                    patches.Add(diff);
                    if (diff.Count == 2 && diff.ContainsKey("position"))
                        moved++;
                    else
                        changed++;
                }
            }
            else
            {
                patches.Add(after);
                added++;
                report.Add($"  {rawName}: added as {ChunkExport.Discriminator(o)} {index}");
            }
        }
        var removed = 0;
        foreach (var index in baseObjects.Keys.Where(i => !seen.Contains(i)).Order())
        {
            patches.Add(new JsonObject { ["index"] = index, [MergePatch.RemoveField] = true });
            removed++;
            report.Add($"  {ChunkExport.ObjectName(baseObjects[index])}: removed");
        }
        objects = [.. objects.OrderBy(o => o.Index)];
        report.Add($"  objects: {moved} moved, {changed} changed, {added} added, {removed} removed");
        return patches.Count == 0 ? null : new JsonObject { ["patch"] = map.Id.ToString(), ["objects"] = patches };
    }

    /// <summary>An object as the plugin writes it, for comparing and patching.</summary>
    private static JsonObject ToJson(MapObject o) => JsonSerializer.SerializeToNode(o, typeof(MapObject), PluginJson.Options)!.AsObject();

    /// <summary>The object with the base's position, rotation and scale where its own are within a hair of them, so a float's drift doesn't patch.</summary>
    private static MapObject KeepUnmoved(MapObject o, MapObject before)
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

    /// <summary>A JSON Merge Patch from <paramref name="was"/> to <paramref name="now"/>: the fields that differ, null for those gone.</summary>
    private static JsonObject Diff(JsonObject was, JsonObject now)
    {
        var diff = new JsonObject();
        foreach (var (field, value) in now)
        {
            if (!was.TryGetPropertyValue(field, out var old) || !JsonNode.DeepEquals(old, value))
                diff[field] = value?.DeepClone();
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

    /// <summary>A map object node's name read: its kind's discriminator and its index, or null for any other name.</summary>
    public static (string Discriminator, int Index)? ObjectName(string name)
    {
        var space = name.LastIndexOf(' ');
        if (space <= 0 || !int.TryParse(name.AsSpan(space + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            return null;
        var discriminator = name[..space];
        return MapObjectKinds.All.Any(k => k.Discriminator == discriminator) ? (discriminator, index) : null;
    }

    private static string Name(JsonElement node) => node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";

    private static JsonElement? Extras(JsonElement node) => node.TryGetProperty("extras", out var e) && e.ValueKind == JsonValueKind.Object ? e : null;

    /// <summary>A node's children with their world matrices, the parent's being <paramref name="parentWorld"/>.</summary>
    private static IEnumerable<(JsonElement Node, Matrix4x4 World)> Children(JsonElement nodes, JsonElement parent, Matrix4x4 parentWorld)
    {
        if (!parent.TryGetProperty("children", out var children))
            yield break;
        foreach (var c in children.EnumerateArray())
        {
            var child = nodes[c.GetInt32()];
            yield return (child, Local(child) * parentWorld);
        }
    }

    /// <summary>Every node with a mesh at or under <paramref name="node"/>, with its world matrix.</summary>
    private static IEnumerable<(JsonElement Node, Matrix4x4 World)> Meshes(JsonElement nodes, JsonElement node, Matrix4x4 world)
    {
        if (node.TryGetProperty("mesh", out _))
            yield return (node, world);
        foreach (var (child, childWorld) in Children(nodes, node, world))
        {
            foreach (var m in Meshes(nodes, child, childWorld))
                yield return m;
        }
    }

    /// <summary>A node's own transform, from its matrix or its translation, rotation and scale (row vectors: scale, then rotate, then move).</summary>
    private static Matrix4x4 Local(JsonElement node)
    {
        if (node.TryGetProperty("matrix", out var m))
        {
            var v = m.EnumerateArray().Select(x => x.GetSingle()).ToArray();
            return new Matrix4x4(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
        }
        var result = Matrix4x4.Identity;
        if (node.TryGetProperty("scale", out var s))
            result *= Matrix4x4.CreateScale(s[0].GetSingle(), s[1].GetSingle(), s[2].GetSingle());
        if (node.TryGetProperty("rotation", out var r))
            result *= Matrix4x4.CreateFromQuaternion(new Quaternion(r[0].GetSingle(), r[1].GetSingle(), r[2].GetSingle(), r[3].GetSingle()));
        if (node.TryGetProperty("translation", out var t))
            result *= Matrix4x4.CreateTranslation(t[0].GetSingle(), t[1].GetSingle(), t[2].GetSingle());
        return result;
    }

    private static bool IsIdentity(Matrix4x4 m)
    {
        var d = m - Matrix4x4.Identity;
        return MathF.Abs(d.M11) + MathF.Abs(d.M12) + MathF.Abs(d.M13) + MathF.Abs(d.M14) + MathF.Abs(d.M21) + MathF.Abs(d.M22) + MathF.Abs(d.M23) + MathF.Abs(d.M24)
            + MathF.Abs(d.M31) + MathF.Abs(d.M32) + MathF.Abs(d.M33) + MathF.Abs(d.M34) + MathF.Abs(d.M41) + MathF.Abs(d.M42) + MathF.Abs(d.M43) + MathF.Abs(d.M44) < Epsilon;
    }

    private static Vector3 Position(float[] p, int v) => new(p[3 * v], p[3 * v + 1], p[3 * v + 2]);
}

/// <summary>
/// Puts JSON that came back through a modeller into the shape a record type reads: a whole number written as <c>3.0</c>
/// becomes <c>3</c> where the field is an integer, <c>0</c> and <c>1</c> become false and true where it's a boolean, and a
/// colour's channels become integers. Blender keeps custom properties' types, but not every tool does.
/// </summary>
public static class JsonShape
{
    public static void Normalise(JsonObject json, Type type)
    {
        type = RecordSchema.Concrete(type, json);
        foreach (var (field, value) in json.ToList())
        {
            if (RecordSchema.Find(type, field) is not { } property || value is null)
                continue;
            var shaped = Normalise(value, property.Type);
            if (!ReferenceEquals(shaped, value))
                json[field] = shaped;
        }
    }

    private static JsonNode Normalise(JsonNode value, Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        switch (value)
        {
            case JsonValue v when v.TryGetValue(out double number):
                if (underlying == typeof(bool) && number is 0 or 1)
                    return JsonValue.Create(number == 1);
                if (IsInteger(underlying) && Math.Floor(number) == number && Math.Abs(number) < 1e15)
                    return JsonValue.Create((long)number);
                return value;
            case JsonObject o:
                Normalise(o, underlying);
                return o;
            case JsonArray a:
                if (underlying == typeof(Rgba))
                {
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (a[i] is JsonValue c && c.TryGetValue(out double channel) && Math.Floor(channel) != channel)
                            a[i] = JsonValue.Create((long)Math.Round(channel));
                    }
                    return a;
                }
                if (RecordSchema.ElementType(underlying) is { } element)
                {
                    for (var i = 0; i < a.Count; i++)
                    {
                        if (a[i] is { } item && Normalise(item, element) is var shaped && !ReferenceEquals(shaped, item))
                            a[i] = shaped;
                    }
                }
                return a;
            default:
                return value;
        }
    }

    private static bool IsInteger(Type t) => t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(uint) || t == typeof(ushort) || t == typeof(sbyte);
}
