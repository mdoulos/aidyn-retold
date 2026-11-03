using System.Numerics;
using System.Text.Json.Nodes;
using AidynRetold.Plugins.Assets;

namespace AidynRetold.Rom.Maps;

/// <summary>
/// Exports a map set's collision as glTF: a node per chunk at
/// its <see cref="MapSets.ChunkOrigin"/>, with one mesh of its triangles. The triangles don't share
/// vertices, so each vertex carries its triangle's <c>_SURFACE</c> (<see cref="CollisionSurface"/>),
/// <c>_ROLE</c> (<see cref="CollisionRole"/>), <c>_FLAGS</c> (<see cref="CollisionFlags"/>) and <c>_TARGET</c>, under the
/// names <see cref="CollisionMesh"/> reads them by. With no normals, viewers shade them flat.
/// </summary>
/// <remarks>The importer exports each chunk alone, in its own coordinates, with <c>placeInSet</c> off.</remarks>
public static class CollisionGltf
{
    public static byte[] Export(IEnumerable<(MapChunk Chunk, Borg9Map Map)> chunks, bool placeInSet = true)
    {
        var gltf = new GltfBuilder { Generator = "acre AidynRetold.Rom" };
        var material = CollisionMesh.Material(gltf);

        var roots = new List<int>();
        foreach (var (chunk, map) in chunks)
        {
            var node = new JsonObject
            {
                ["name"] = $"{chunk.Name} borg9 {chunk.Collision}",
                ["extras"] = new JsonObject { ["chunk"] = chunk.Name, ["borg9"] = chunk.Collision },
            };
            var origin = placeInSet ? MapSets.ChunkOrigin(chunk) : Vector3.Zero;
            if (origin != Vector3.Zero)
                node["translation"] = new JsonArray(origin.X, origin.Y, origin.Z);

            if (map.Triangles.Count > 0)
            {
                var triangles = map.Triangles.Select(t => new CollisionTriangle(map.Vertices[t.A], map.Vertices[t.B], map.Vertices[t.C],
                    (int)t.Surface, t.Role, t.Flags, t.ToggleId ?? t.TriggerObject ?? 0)).ToList();
                node["mesh"] = gltf.AddMesh(new JsonObject
                {
                    ["name"] = $"collision {chunk.Collision}",
                    ["primitives"] = new JsonArray(CollisionMesh.Primitive(gltf, triangles, material)),
                });
            }
            roots.Add(gltf.AddNode(node));
        }
        return gltf.ToGlb(roots);
    }
}
