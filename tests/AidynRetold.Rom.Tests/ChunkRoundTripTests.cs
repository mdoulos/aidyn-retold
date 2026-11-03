using System.Text.Json;
using System.Text.Json.Nodes;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using AidynRetold.Plugins.World;

namespace AidynRetold.Rom.Tests;

/// <summary>
/// Editing the world (docs/editing-the-world.md): the rules a chunk's import works out again hold against every base map,
/// and a chunk exported and imported untouched writes nothing to the mod.
/// </summary>
[Collection(ImportedPluginCollection.Name)]
public sealed class ChunkRoundTripTests(Na10Import import)
{
    private PluginSet Set => import.Data.Set;

    [Fact]
    public void Edge_and_corner_flags_go_to_the_first_triangle_that_uses_them()
    {
        var total = 0;
        var same = 0;
        var seeThrough = 0;
        var callback = 0;
        foreach (var map in import.Data.Maps)
        {
            var triangles = CollisionMesh.For(Set, map.Id);
            var recomputed = CollisionRules.WithFlags(triangles);
            for (var i = 0; i < triangles.Count; i++)
            {
                total++;
                if (recomputed[i].Flags == triangles[i].Flags)
                    same++;
                const CollisionFlags Rest = CollisionFlags.SeeThrough | CollisionFlags.Callback | CollisionFlags.Passable;
                if ((recomputed[i].Flags & CollisionFlags.SeeThrough) == (triangles[i].Flags & CollisionFlags.SeeThrough))
                    seeThrough++;
                if ((recomputed[i].Flags & (Rest & ~CollisionFlags.SeeThrough)) == (triangles[i].Flags & (Rest & ~CollisionFlags.SeeThrough)))
                    callback++;
            }
        }
        Assert.True(total > 160_000, $"{total} triangles");
        // The ROM's bits follow its own vertex indices, and a few maps have two vertices at one position, so welding by
        // position misses a few: 99.7% of 168,317 when the rule was settled (Oct 30, 2026).
        Assert.True(same >= total * 0.995, $"{same} of {total} triangles' flags match");
        Assert.Equal(total, seeThrough);
        Assert.Equal(total, callback);
    }

    [Fact]
    public void The_grid_is_laid_out_as_the_original_s_and_lists_everything_its_cells_do()
    {
        var maps = 0;
        var cells = 0;
        var exactCells = 0;
        var under = new List<string>();
        foreach (var map in import.Data.Maps.Where(m => m.Grid is not null))
        {
            var triangles = CollisionMesh.For(Set, map.Id);
            var grid = CollisionRules.Grid(triangles, map.Objects);
            Assert.NotNull(grid);
            var expected = map.Grid!;
            Assert.Equal(expected.Origin, grid.Origin);
            Assert.Equal(expected.Columns, grid.Columns);
            Assert.Equal(expected.Rows, grid.Rows);
            Assert.Equal(expected.CellSize.X, grid.CellSize.X, 1e-3f);
            Assert.Equal(expected.CellSize.Y, grid.CellSize.Y, 1e-3f);
            maps++;
            for (var i = 0; i < expected.Cells.Count; i++)
            {
                cells++;
                if (expected.Cells[i].SequenceEqual(grid.Cells[i]))
                    exactCells++;
                foreach (var index in expected.Cells[i].Except(grid.Cells[i]))
                    under.Add($"{map.Id} cell {i} object {index}");
            }
        }
        Assert.True(maps > 500, $"{maps} maps");
        Assert.Empty(under);
        // 74,122 of 74,346 cells when the rule was settled (Oct 30, 2026); the rest list an object or two more than the original's.
        Assert.True(exactCells >= cells * 0.99, $"{exactCells} of {cells} cells match exactly");
    }

    [Fact]
    public void A_chunk_exported_and_imported_untouched_writes_nothing()
    {
        using var temp = new TempPlugins();
        var set = import.Data.MapSets.Single(s => s.Label == "Overworld");
        // Gwernia, two more of the Overworld and three interiors.
        var chunks = new[] { ChunkExport.FindChunk(set, "M07")! }.Concat(set.Chunks.Where(c => c.Name != "M07").Take(2)).ToList();
        var interiors = import.Data.MapSets.Single(s => s.Label == "GwerniaInteriors");
        var cases = chunks.Select(c => (set, c)).Concat(interiors.Chunks.Take(3).Select(c => (interiors, c))).ToList();
        foreach (var (mapSet, chunk) in cases)
        {
            var glb = ChunkExport.Export(Set, mapSet, chunk);
            var mod = ModFolder.OpenOrCreate(temp.Root, "round_trip");
            var result = ChunkImport.Import(Set, glb, mod);
            Assert.Null(result.Patch);
            Assert.False(result.SceneryWritten, string.Join("\n", result.Report));
            Assert.False(result.AltSceneryWritten, string.Join("\n", result.Report));
            Assert.False(result.CollisionWritten, string.Join("\n", result.Report));
            Assert.False(Directory.Exists(Path.Combine(mod.Directory, "assets")), $"{mapSet.Label} {chunk.Name} wrote assets");
            Assert.False(Directory.Exists(Path.Combine(mod.Directory, "records")), $"{mapSet.Label} {chunk.Name} wrote records");
        }
    }

    [Fact]
    public void Moving_an_object_patches_its_position_and_the_grid_alone()
    {
        using var temp = new TempPlugins();
        var set = import.Data.MapSets.Single(s => s.Label == "Overworld");
        var chunk = ChunkExport.FindChunk(set, "M07")!;
        var map = Set.Get<MapRecord>(chunk.Map);
        var container = map.Objects.OfType<ContainerObject>().First();

        var glb = ChunkExport.Export(Set, set, chunk);
        var json = TestGlb.Json(glb);
        var node = json["nodes"]!.AsArray().OfType<JsonObject>().Single(n => n["name"]?.GetValue<string>() == ChunkExport.ObjectName(container));
        node["translation"] = new JsonArray(container.Position.X + 3, container.Position.Y, container.Position.Z);
        var moved = TestGlb.WithJson(glb, json);

        var mod = ModFolder.OpenOrCreate(temp.Root, "moved");
        var result = ChunkImport.Import(Set, moved, mod);
        Assert.NotNull(result.Patch);
        var objects = result.Patch!["objects"]!.AsArray();
        var patch = Assert.Single(objects)!.AsObject();
        Assert.Equal(container.Index, patch["index"]!.GetValue<int>());
        Assert.Equal(container.Position.X + 3, patch["position"]![0]!.GetValue<float>(), 1e-3f);
        Assert.Equal(2, patch.Count);
        Assert.False(result.CollisionWritten);
        Assert.False(result.SceneryWritten);

        // The mod loads over the base, and the game sees the chest where it was put.
        var loaded = PluginSet.Load([import.Data.Dir, mod.Directory]);
        var after = loaded.Get<MapRecord>(chunk.Map).Objects.OfType<ContainerObject>().Single(o => o.Index == container.Index);
        Assert.Equal(container.Position.X + 3, after.Position.X, 1e-3f);
        Assert.Equal(container.Loot, after.Loot);
        Assert.Empty(loaded.Validate().Problems);
    }
}
