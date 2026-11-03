using System.Numerics;
using System.Text.Json.Nodes;

namespace AidynRetold.Plugins.Assets;

/// <summary>
/// What a collision triangle does besides block, its flags as the ROM stores them: the
/// Borg9 reader parses them into this and the importer writes them unchanged. The game sets
/// <see cref="Callback"/> on every triangle it loads.
/// </summary>
[Flags]
public enum CollisionFlags
{
    None = 0,
    /// <summary>
    /// Things pass through it, and only its callbacks run. A trigger
    /// (<see cref="Records.TriggerKind.ChangeCollision"/>) can set this on a toggle to open the way.
    /// </summary>
    Passable = 0x1,
    /// <summary>Whatever touches it takes on its surface's speed property (gravity and drag). Never set.</summary>
    TakesSpeed = 0x2,
    /// <summary>Spheres collide with the edge from A to B as well as the face. Every edge no other triangle shares has its bit.</summary>
    EdgeAB = 0x4,
    /// <summary>The edge from C to A.</summary>
    EdgeCA = 0x8,
    /// <summary>The edge from B to C.</summary>
    EdgeBC = 0x10,
    /// <summary>Spheres collide with the corner at A.</summary>
    VertexA = 0x20,
    VertexB = 0x40,
    VertexC = 0x80,
    /// <summary>Recompute the normal each time. Never set.</summary>
    RecomputeNormal = 0x100,
    /// <summary>Line-of-sight tests ignore it, so the camera passes through: the camera, combat markers and light occlusion. Invisible walls.</summary>
    SeeThrough = 0x200,
    /// <summary>Report touches to the zone engine, which sets footstep sounds and runs the <see cref="CollisionRole.Trigger"/>'s object.</summary>
    Callback = 0x1000,
    /// <summary>Touch it with no extra margin. Never set.</summary>
    NoMargin = 0x2000,
}

/// <summary>
/// What a collision triangle is for. In the ROM it's the top four bits of the triangle's ground type,
/// 0x1000 for a toggle, 0x2000 for a trigger and 0x4000 for an Expansion Pak triangle.
/// </summary>
public enum CollisionRole
{
    Normal,
    /// <summary>A trigger can make it passable.</summary>
    Toggle,
    /// <summary>Walking on it runs a map object: a camera, teleporter or dialogue.</summary>
    Trigger,
    /// <summary>Only there with the Expansion Pak, which the runtime always has. Without it, the game makes it passable.</summary>
    ExpansionPak,
}

/// <summary>A collision triangle, counter-clockwise seen from the front.</summary>
/// <param name="Surface">
/// The physics and footstep material, numbered as the original's surfaces: 0 Default, 1 blocks
/// the player and the camera, 2 blocks only the player, then Snow, Grass, Sand and so on.
/// </param>
/// <param name="Target">
/// For a <see cref="CollisionRole.Trigger"/>, the index in its map of the object it runs; for a
/// <see cref="CollisionRole.Toggle"/>, the id its triggers name. Otherwise 0.
/// </param>
public readonly record struct CollisionTriangle(Vector3 A, Vector3 B, Vector3 C, int Surface, CollisionRole Role, CollisionFlags Flags, int Target = 0);

/// <summary>
/// Reads a map's collision asset: a glTF mesh whose triangles don't share vertices, each vertex
/// carrying its triangle's surface, role, flags and target as floats (<see cref="SurfaceAttribute"/> and the others).
/// </summary>
public static class CollisionMesh
{
    /// <summary>The vertex attributes the exporter (<c>CollisionGltf</c>) writes, per vertex of a triangle and the same on its three: <see cref="CollisionTriangle.Surface"/>.</summary>
    public const string SurfaceAttribute = "_SURFACE";
    /// <summary><see cref="CollisionTriangle.Role"/>, as a <see cref="CollisionRole"/>'s number.</summary>
    public const string RoleAttribute = "_ROLE";
    /// <summary><see cref="CollisionTriangle.Flags"/>.</summary>
    public const string FlagsAttribute = "_FLAGS";
    /// <summary><see cref="CollisionTriangle.Target"/>; a file without it has none.</summary>
    public const string TargetAttribute = "_TARGET";

    /// <summary>
    /// Writes triangles as a collision asset in the importer's layout: one node and one mesh, the triangles' vertices
    /// unshared, each carrying its triangle's attributes. <paramref name="name"/> names the node and mesh.
    /// </summary>
    public static byte[] Write(IReadOnlyList<CollisionTriangle> triangles, string name, string generator = "acre")
    {
        var gltf = new GltfBuilder { Generator = generator };
        var node = new JsonObject { ["name"] = name };
        if (triangles.Count > 0)
            node["mesh"] = gltf.AddMesh(new JsonObject { ["name"] = name, ["primitives"] = new JsonArray(Primitive(gltf, triangles, Material(gltf))) });
        return gltf.ToGlb([gltf.AddNode(node)]);
    }

    /// <summary>The flat grey material the importer gives collision, so that viewers shade it.</summary>
    public static int Material(GltfBuilder gltf) => gltf.AddMaterial(new JsonObject
    {
        ["name"] = "collision",
        ["doubleSided"] = true,
        ["pbrMetallicRoughness"] = new JsonObject { ["metallicFactor"] = 0, ["roughnessFactor"] = 1 },
    });

    /// <summary>
    /// A primitive of <paramref name="triangles"/> in the layout <see cref="Read"/> reads: unshared vertices, each with its
    /// triangle's <see cref="SurfaceAttribute"/>, <see cref="RoleAttribute"/>, <see cref="FlagsAttribute"/> and <see cref="TargetAttribute"/>.
    /// </summary>
    public static JsonObject Primitive(GltfBuilder gltf, IReadOnlyList<CollisionTriangle> triangles, int material)
    {
        var positions = new List<Vector3>(triangles.Count * 3);
        List<float> surfaces = new(triangles.Count * 3), roles = new(triangles.Count * 3), flags = new(triangles.Count * 3), targets = new(triangles.Count * 3);
        foreach (var t in triangles)
        {
            positions.Add(t.A);
            positions.Add(t.B);
            positions.Add(t.C);
            for (var i = 0; i < 3; i++)
            {
                surfaces.Add(t.Surface);
                roles.Add((float)t.Role);
                flags.Add((float)t.Flags);
                targets.Add(t.Target);
            }
        }
        return new JsonObject
        {
            ["attributes"] = new JsonObject
            {
                ["POSITION"] = gltf.AddPositions(positions),
                [SurfaceAttribute] = gltf.AddScalars(surfaces),
                [RoleAttribute] = gltf.AddScalars(roles),
                [FlagsAttribute] = gltf.AddScalars(flags),
                [TargetAttribute] = gltf.AddScalars(targets),
            },
            ["material"] = material,
        };
    }

    /// <summary>
    /// A map's collision, from the plugin that provides its asset, or none if no plugin has one: the original's with the map record's
    /// <see cref="Records.MapRecord.CollisionMoves"/> made, which number its triangles, or a later plugin's as it is.
    /// </summary>
    public static IReadOnlyList<CollisionTriangle> For(PluginSet plugins, RecordId map)
    {
        if (plugins.FindAsset(map, "glb") is not { } path)
            return [];
        var triangles = Read(File.ReadAllBytes(path));
        return plugins.TryGet<Records.MapRecord>(map)?.CollisionMoves is { Count: > 0 } moves && path == plugins.FindOriginalAsset(map, "glb")
            ? Move(triangles, moves, map)
            : triangles;
    }

    /// <summary>
    /// <paramref name="triangles"/> with each of <paramref name="moves"/>' runs moved by its offset, their surfaces, roles, flags and
    /// targets kept: a slide keeps a triangle's normal, and which of two triangles claims a shared edge.
    /// </summary>
    public static IReadOnlyList<CollisionTriangle> Move(IReadOnlyList<CollisionTriangle> triangles, IReadOnlyList<Records.CollisionMove> moves, RecordId map)
    {
        var moved = triangles.ToArray();
        foreach (var move in moves)
        {
            if (move.First < 0 || move.Count < 1 || move.First + move.Count > moved.Length)
                throw new InvalidDataException($"{map}'s collisionMoves move triangles {move.First} to {move.First + move.Count - 1}, but its collision has {moved.Length}.");
            for (var t = move.First; t < move.First + move.Count; t++)
                moved[t] = moved[t] with { A = moved[t].A + move.By, B = moved[t].B + move.By, C = moved[t].C + move.By };
        }
        return moved;
    }

    public static IReadOnlyList<CollisionTriangle> Read(byte[] glb)
    {
        using var file = GlbReader.Parse(glb);
        var root = file.Root;

        var triangles = new List<CollisionTriangle>();
        if (!root.TryGetProperty("meshes", out var meshes))
            return triangles;
        foreach (var mesh in meshes.EnumerateArray())
        {
            foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
            {
                var attributes = primitive.GetProperty("attributes");
                var positions = file.Floats(attributes.GetProperty("POSITION").GetInt32());
                var surfaces = file.Floats(attributes.GetProperty(SurfaceAttribute).GetInt32());
                var roles = file.Floats(attributes.GetProperty(RoleAttribute).GetInt32());
                var flags = file.Floats(attributes.GetProperty(FlagsAttribute).GetInt32());
                var targets = attributes.TryGetProperty(TargetAttribute, out var target) ? file.Floats(target.GetInt32()) : null;
                for (var v = 0; v + 2 < surfaces.Length; v += 3)
                {
                    triangles.Add(new CollisionTriangle(
                        Position(positions, v), Position(positions, v + 1), Position(positions, v + 2),
                        (int)surfaces[v], (CollisionRole)(int)roles[v], (CollisionFlags)(int)flags[v], targets is null ? 0 : (int)targets[v]));
                }
            }
        }
        return triangles;
    }

    private static Vector3 Position(float[] p, int v) => new(p[3 * v], p[3 * v + 1], p[3 * v + 2]);
}
