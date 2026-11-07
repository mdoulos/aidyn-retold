using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.Tests;

/// <summary>The remade game's places, which name where the player is (the user's design, Nov 7, 2026): a chunk's own place, its region's, its map set's.</summary>
public sealed class PlaceRecordsTests
{
    private const string MapSets = """
        [{ "id": "aidyn:mapset/0", "label": "Overworld", "cellSize": 100, "chunks": [
            { "row": 13, "column": 7, "map": "aidyn:map/0x0001", "terrain": 19, "region": "aidyn:flag/788" },
            { "row": 12, "column": 8, "map": "aidyn:map/0x0001", "terrain": 21, "region": "aidyn:flag/788" },
            { "row": 2, "column": 9, "map": "aidyn:map/0x0001", "terrain": 19, "region": "aidyn:flag/789" },
            { "row": 1, "column": 4, "map": "aidyn:map/0x0001", "terrain": 5, "region": "aidyn:flag/790" },
            { "row": 8, "column": 20, "map": "aidyn:map/0x0001", "terrain": 7 } ] },
         { "id": "aidyn:mapset/7", "label": "Erromon", "cellSize": 100, "chunks": [
            { "row": 1, "column": 1, "map": "aidyn:map/0x0001", "terrain": 8 } ] }]
        """;

    private const string Maps = """[{ "id": "aidyn:map/0x0001", "objects": [] }]""";

    private const string Flags = """
        [{ "id": "aidyn:flag/788", "label": "World map region 1" }, { "id": "aidyn:flag/789", "label": "World map region 2" },
         { "id": "aidyn:flag/790", "label": "World map region 3" }]
        """;

    private const string Places = """
        [{ "id": "acre:place/gwernia", "name": "Gwernia", "chunks": [{ "mapSet": "aidyn:mapset/0", "chunks": ["M06", "m07"] }], "mapSets": ["aidyn:mapset/4"] },
         { "id": "acre:place/erromon", "name": "Erromon", "chunks": [{ "mapSet": "aidyn:mapset/0", "chunks": ["B09"] }], "mapSets": ["aidyn:mapset/7"] },
         { "id": "acre:place/oisin_forest", "name": "Oisin Forest", "regions": ["aidyn:flag/788", "aidyn:flag/789"] }]
        """;

    [Fact]
    public void AChunksOwnPlaceWinsOverItsRegionsWhichWinsOverItsMapSetsAndAnUnnamedChunkIsNowhere()
    {
        using var temp = new TempPlugins();
        var plugins = PluginSet.Load([
            temp.Add("aidyn", null, ("mapsets.json", MapSets), ("maps.json", Maps), ("flags.json", Flags)),
            temp.Add("acre", ["aidyn"], ("places.json", Places)),
        ]);
        var overworld = plugins.Get<MapSetRecord>(RecordId.Decimal(RecordTypes.MapSet, 0));
        var erromon = plugins.Get<MapSetRecord>(RecordId.Decimal(RecordTypes.MapSet, 7));

        // The town's chunk, named in any case, over the forest's region it carries.
        Assert.Equal("Gwernia", PlaceRecord.At(plugins, overworld.Id, overworld.Chunk(13, 7)!)?.Name);
        // The road outside, by its region.
        Assert.Equal("Oisin Forest", PlaceRecord.At(plugins, overworld.Id, overworld.Chunk(12, 8)!)?.Name);
        Assert.Equal("Erromon", PlaceRecord.At(plugins, overworld.Id, overworld.Chunk(2, 9)!)?.Name);
        // A region nothing names, and a chunk with none.
        Assert.Null(PlaceRecord.At(plugins, overworld.Id, overworld.Chunk(1, 4)!));
        Assert.Null(PlaceRecord.At(plugins, overworld.Id, overworld.Chunk(8, 20)!));
        // A town's interiors, by their map set.
        Assert.Equal("Erromon", PlaceRecord.At(plugins, erromon.Id, erromon.Chunk(1, 1)!)?.Name);

        var gwernia = plugins.Get<PlaceRecord>(RecordId.Parse("acre:place/gwernia"));
        Assert.Equal(["M06", "m07"], gwernia.Chunks![0].Chunks);
        Assert.True(gwernia.NamesMapSet(RecordId.Decimal(RecordTypes.MapSet, 4)));
        Assert.False(gwernia.NamesRegion(overworld.Chunk(13, 7)!));
    }
}
