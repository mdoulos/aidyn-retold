using System.Numerics;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.World;

/// <summary>
/// What the ROM stores about collision that an edited mesh has to have worked out again (docs/formats/maps.md): the
/// triangles' edge and corner flags, which follow from the triangles' order, and the grid of the objects each cell
/// lists, which pressing A reads. Each rule was settled against every base map (docs/editing-the-world.md says how well),
/// and <c>ChunkRoundTripTests</c> keeps it so.
/// </summary>
public static class CollisionRules
{
    /// <summary>
    /// The flags of every triangle, from the mesh and the triangles' order: an edge or a corner collides for the first
    /// triangle, by index, that uses it, and for no later one (<see cref="CollisionFlags.EdgeAB"/>, <see cref="CollisionFlags.VertexA"/>
    /// and the rest), so that a shared edge is tested once. A triangle of surface 2, which blocks the player alone, is
    /// <see cref="CollisionFlags.SeeThrough"/>, and one with a role has <see cref="CollisionFlags.Callback"/>, as the ROM
    /// stores them. <see cref="CollisionFlags.Passable"/> is kept from what the triangle had. Vertices are one where their
    /// positions are exactly equal, as a modeller's file shares them.
    /// </summary>
    public static IReadOnlyList<CollisionTriangle> WithFlags(IReadOnlyList<CollisionTriangle> triangles)
    {
        var indexOf = new Dictionary<Vector3, int>();
        int Index(Vector3 v)
        {
            if (!indexOf.TryGetValue(v, out var i))
                indexOf[v] = i = indexOf.Count;
            return i;
        }
        var edgesClaimed = new HashSet<(int, int)>();
        var verticesClaimed = new HashSet<int>();
        bool ClaimEdge(int a, int b) => edgesClaimed.Add(a < b ? (a, b) : (b, a));
        var result = new List<CollisionTriangle>(triangles.Count);
        foreach (var t in triangles)
        {
            int a = Index(t.A), b = Index(t.B), c = Index(t.C);
            var flags = t.Flags & CollisionFlags.Passable;
            if (ClaimEdge(a, b))
                flags |= CollisionFlags.EdgeAB;
            if (ClaimEdge(b, c))
                flags |= CollisionFlags.EdgeBC;
            if (ClaimEdge(c, a))
                flags |= CollisionFlags.EdgeCA;
            if (verticesClaimed.Add(a))
                flags |= CollisionFlags.VertexA;
            if (verticesClaimed.Add(b))
                flags |= CollisionFlags.VertexB;
            if (verticesClaimed.Add(c))
                flags |= CollisionFlags.VertexC;
            if (t.Surface == 2)
                flags |= CollisionFlags.SeeThrough;
            if (t.Role != CollisionRole.Normal)
                flags |= CollisionFlags.Callback;
            result.Add(t with { Flags = flags });
        }
        return result;
    }

    /// <summary>The kinds of object the grid's cells list; the rest are handled per chunk.</summary>
    public static bool Listed(MapObject o) =>
        o is SceneObject or ContainerObject or LightObject or TeleporterObject or CameraObject or DialogueObject or TriggerObject or SavePointObject;

    /// <summary>The most cells a grid has along either axis.</summary>
    public const int MaxCells = 24;

    /// <summary>The cell size the grid aims for; its cells are this or larger, as the extent divides.</summary>
    public const float CellSize = 5;

    /// <summary>
    /// The grid of the objects each cell lists, as the original lays it out: from the collision's least X and Z, as many
    /// cells each way as the extent holds of <see cref="CellSize"/>, 1 to <see cref="MaxCells"/>, each the extent's share. A
    /// cell lists every listed object whose radius, grown by half the cell's diagonal, reaches the cell in X and Z, except a
    /// camera or dialogue a trigger triangle runs, which no cell lists; the lists are by index. This lists everything the
    /// original's cells list and a few more, which is harmless. Null for a chunk with no collision, which has no grid.
    /// </summary>
    public static ObjectGrid? Grid(IReadOnlyList<CollisionTriangle> triangles, IReadOnlyList<MapObject> objects)
    {
        if (triangles.Count == 0)
            return null;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var t in triangles)
        {
            foreach (var v in new[] { t.A, t.B, t.C })
            {
                min = Vector2.Min(min, new Vector2(v.X, v.Z));
                max = Vector2.Max(max, new Vector2(v.X, v.Z));
            }
        }
        var extent = max - min;
        var columns = Math.Clamp((int)MathF.Floor(extent.X / CellSize), 1, MaxCells);
        var rows = Math.Clamp((int)MathF.Floor(extent.Y / CellSize), 1, MaxCells);
        var cellSize = new Vector2(extent.X / columns, extent.Y / rows);
        var margin = cellSize.Length() / 2;

        var runByTrigger = triangles.Where(t => t.Role == CollisionRole.Trigger).Select(t => t.Target).ToHashSet();
        var listed = objects.Where(o => Listed(o) && !(o is CameraObject or DialogueObject && runByTrigger.Contains(o.Index))).OrderBy(o => o.Index).ToList();

        var cells = new List<IReadOnlyList<int>>(columns * rows);
        for (var z = 0; z < rows; z++)
        {
            for (var x = 0; x < columns; x++)
            {
                var low = min + new Vector2(x, z) * cellSize;
                var high = low + cellSize;
                var inCell = new List<int>();
                foreach (var o in listed)
                {
                    var p = new Vector2(o.Position.X, o.Position.Z);
                    var reach = o.Radius + margin;
                    if (Vector2.DistanceSquared(p, Vector2.Clamp(p, low, high)) <= reach * reach)
                        inCell.Add(o.Index);
                }
                cells.Add(inCell);
            }
        }
        return new ObjectGrid(min, cellSize, columns, rows, cells);
    }
}
