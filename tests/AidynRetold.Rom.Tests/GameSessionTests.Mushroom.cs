using System.Numerics;
using AidynRetold.Core.Actors;
using AidynRetold.Core.Chunks;
using AidynRetold.Core.Movement;
using AidynRetold.Core.Physics;
using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using AidynRetold.Rom.Maps;

namespace AidynRetold.Rom.Tests;

/// <summary>
/// The Roog Cave's mushroom (docs/hardcoded-content.md#corrections-to-the-roms-maps): where the ROM puts it, and where the import
/// leaves it, in the neck of D01's passage, Alaron wedges against it walking either way and often running north to south; the
/// ACRE plugin moves it with its collision box, the user's choice (ADR 0043), and he passes it every way he moves.
/// </summary>
public sealed partial class GameSessionTests
{
    /// <summary>The mushroom: object 12 of D01's map, which draws scene 0x0BCB.</summary>
    private const int Mushroom = 12, MushroomScene = 0x0BCB;

    /// <summary>
    /// What the ACRE plugin moves, with the mushroom: its collision box, 8 of the chunk's triangles (926 to 933), slid 0.1 toward the
    /// rock east of it and 0.7 north along the rock's face.
    /// </summary>
    private static readonly CollisionMove MushroomBox = new(926, 8, new Vector3(-0.124f, 0, 0.696f));

    /// <summary>The way through the neck from the south, in D01's coordinates, west of the mushroom wherever it is.</summary>
    private static readonly Vector2[] RoogNeck =
        [new(12.4f, 20.0f), new(11.2f, 20.5f), new(10.85f, 21.5f), new(10.5f, 22.4f), new(10.0f, 23.0f), new(9.3f, 23.9f), new(9.0f, 24.6f), new(9.0f, 25.6f)];

    [Fact]
    public void TheImportLeavesTheRoogCavesMushroomAndItsBoxWhereTheRomHasThem()
    {
        var (chunk, rom) = RoogD01();
        var map = RecordId.Hex(RecordTypes.Map, chunk.Collision);
        var mushroom = (SceneObject)Data.Set.Get<MapRecord>(map).Objects.Single(o => o.Index == Mushroom);
        Assert.Equal(RecordId.Hex(RecordTypes.Scene, MushroomScene), mushroom.Models[0].Model);
        Assert.Equal(rom.Objects[Mushroom].Position, mushroom.Position);
        Assert.Null(Data.Set.Get<MapRecord>(map).CollisionMoves);

        // Its box is 8 triangles of its own, whose vertices no other triangle uses, centred under it.
        var collision = CollisionMesh.For(Data.Set, map);
        Assert.Equal(rom.Triangles.Count, collision.Count);
        for (var t = 0; t < collision.Count; t++)
        {
            var expected = rom.Triangles[t];
            Assert.Equal((rom.Vertices[expected.A], rom.Vertices[expected.B], rom.Vertices[expected.C]), (collision[t].A, collision[t].B, collision[t].C));
        }
        var box = Enumerable.Range(MushroomBox.First, MushroomBox.Count).ToList();
        var vertices = box.SelectMany(t => new[] { rom.Triangles[t].A, rom.Triangles[t].B, rom.Triangles[t].C }).ToHashSet();
        Assert.DoesNotContain(rom.Triangles.Index(), t => !box.Contains(t.Index) && (vertices.Contains(t.Item.A) || vertices.Contains(t.Item.B) || vertices.Contains(t.Item.C)));
        var centre = vertices.Aggregate(Vector3.Zero, (sum, v) => sum + rom.Vertices[v]) / vertices.Count;
        Assert.InRange(new Vector2(centre.X - mushroom.Position.X, centre.Z - mushroom.Position.Z).Length(), 0, 0.01f);
    }

    [Fact]
    public void AlaronWalksRunsAndSneaksPastTheRoogCavesMushroomBothWaysOnceItsMovedWhereTheROMsWedgesHim()
    {
        var set = Data.Set.Get<MapSetRecord>(RecordId.Decimal(RecordTypes.MapSet, (int)MapSet.RoogCave));
        var entry = set.Chunks.Single(c => c.Name == "D01");
        CollisionWorld World(IEnumerable<CollisionTriangle> triangles)
        {
            var world = new CollisionWorld();
            world.Add(new ChunkKey(set.Id, entry.Row, entry.Column), triangles, set.Origin(entry.Row, entry.Column));
            return world;
        }
        var original = CollisionMesh.For(Data.Set, entry.Map);
        var moved = World(CollisionMesh.Move(original, [MushroomBox], entry.Map));
        var rom = World(original);

        var gaits = new (string Name, MoveInput Input)[]
        {
            ("run", new MoveInput(new Vector2(0, 1))), ("walk", new MoveInput(new Vector2(0, 0.6f))), ("sneak", new MoveInput(new Vector2(0, 1), Sneak: true)),
        };
        var stuck = new List<string>();
        var romStuck = new List<string>();
        foreach (var (name, input) in gaits)
        foreach (var northward in new[] { true, false })
        foreach (var side in new[] { -0.2f, -0.1f, 0f, 0.1f, 0.2f })
        {
            var way = northward ? RoogNeck : [.. RoogNeck.Reverse()];
            var what = $"{name} {(northward ? "north" : "south")} from {side}";
            if (ThroughRoogNeck(moved, set.Origin(entry.Row, entry.Column), way, side, input) is { } at)
                stuck.Add($"{what}: held at {at}");
            if (ThroughRoogNeck(rom, set.Origin(entry.Row, entry.Column), way, side, input) is not null)
                romStuck.Add(what);
        }
        Assert.Empty(stuck);
        // Where the ROM has it, walking and sneaking wedge him beside it every way, and running south from most starts.
        foreach (var gait in new[] { "walk north", "walk south", "sneak north", "sneak south" })
            Assert.Equal(5, romStuck.Count(s => s.StartsWith(gait, StringComparison.Ordinal)));
        Assert.InRange(romStuck.Count(s => s.StartsWith("run south", StringComparison.Ordinal)), 3, 5);
    }

    /// <summary>
    /// The owner's ACRE plugin, from <c>ACRE_PLUGIN</c>, over the import: it moves the mushroom and its box by the same
    /// offset, and nothing else of the chunk. Skipped without it, as a clone of this repository alone has none.
    /// </summary>
    [Fact]
    public void TheAcrePluginMovesTheMushroomWithItsBox()
    {
        var fixes = DataPaths.AcrePlugin;
        if (fixes is null || !File.Exists(Path.Combine(fixes, PluginManifest.FileName)))
        {
            Assert.Skip($"No ACRE plugin ({DataPaths.AcrePluginVariable}).");
            return;
        }
        var (chunk, rom) = RoogD01();
        var map = RecordId.Hex(RecordTypes.Map, chunk.Collision);
        var set = PluginSet.Load([Data.Dir, fixes]);

        var record = set.Get<MapRecord>(map);
        Assert.Equal(new[] { MushroomBox }, record.CollisionMoves);
        Assert.Equal(rom.Objects[Mushroom].Position + MushroomBox.By, record.Objects.Single(o => o.Index == Mushroom).Position);
        // Only the mushroom moves: every other object is the import's.
        static IEnumerable<string> Others(MapRecord m) =>
            m.Objects.Where(o => o.Index != Mushroom).Select(o => System.Text.Json.JsonSerializer.Serialize(o, typeof(MapObject), PluginJson.Options));
        Assert.Equal(Others(Data.Set.Get<MapRecord>(map)), Others(record));
        Assert.Equal(CollisionMesh.Move(CollisionMesh.For(Data.Set, map), [MushroomBox], map), CollisionMesh.For(set, map));
    }

    /// <summary>
    /// Alaron, with his run animation, set down at the way's start moved <paramref name="side"/> across it, steering for each of its
    /// points in turn: null once he's through, or where he's held after 15 seconds.
    /// </summary>
    private Vector3? ThroughRoogNeck(CollisionWorld world, Vector3 origin, Vector2[] way, float side, MoveInput input)
    {
        var toLocal = new Vector2(origin.X, origin.Z);
        var heading = Vector2.Normalize(way[1] - way[0]);
        var start = way[0] + new Vector2(-heading.Y, heading.X) * side;
        var player = new PlayerMotion(origin + new Vector3(start.X, PlayerMotion.Radius + 0.01f, start.Y), heading);
        var animation = ActorMotion.For(Data.Set, Data.Set.Get<ActorRecord>(AlaronActor), new Lcg(0))!;
        for (var i = 0; i < 10; i++)
            player.Step(world, new MoveInput(Vector2.Zero), heading, animation);
        var next = 1;
        for (var i = 0; i < 900; i++)
        {
            var at = new Vector2(player.Sphere.Position.X, player.Sphere.Position.Z) - toLocal;
            if (Vector2.Distance(at, way[^1]) < 0.5f)
                return null;
            while (next < way.Length - 1 && Vector2.Distance(at, way[next]) < 0.5f)
                next++;
            // Up on the stick, the camera looking at the next point.
            player.Step(world, input, Vector2.Normalize(way[next] - at), animation);
            Assert.False(player.FellOut);
        }
        return player.Sphere.Position - origin;
    }

    /// <summary>The Roog Cave's D01 as the ROM has it: its chunk and its Borg9, parsed.</summary>
    private static (MapChunk Chunk, Borg9Map Map) RoogD01()
    {
        var rom = TestRoms.Get(RomVersionId.Na10);
        var chunk = MapChunks.Read(rom).Single(c => c.Set == MapSet.RoogCave && c.Name == "D01");
        return (chunk, Borg9Map.Parse(Borg.BorgArchive.Open(rom).Read(chunk.Collision)));
    }
}
