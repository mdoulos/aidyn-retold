using System.Numerics;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Plugins.Tests;

/// <summary>
/// A map's <see cref="MapRecord.CollisionMoves"/>, which a plugin's patch slides a run of the original's collision triangles by
/// (ADR 0043), as the ACRE plugin moves the Roog Cave's mushroom's box: they move the ROM plugin's asset, and a later plugin's own
/// asset is taken as it is.
/// </summary>
public sealed class CollisionMoveTests : IDisposable
{
    private static readonly RecordId Map = RecordId.Parse("aidyn:map/0x0001");
    private const string MapRecordJson = """[{ "id": "aidyn:map/0x0001", "objects": [] }]""";

    private readonly TempPlugins _temp = new();

    public void Dispose() => _temp.Dispose();

    private static CollisionTriangle Triangle(float x) =>
        new(new Vector3(x, 0, 0), new Vector3(x + 1, 0, 0), new Vector3(x, 0, 1), 3, CollisionRole.Trigger, CollisionFlags.Callback, 7);

    private static readonly CollisionTriangle[] Original = [Triangle(0), Triangle(10), Triangle(20)];

    /// <summary>A plugin with these record files and, if given, its own collision asset for <see cref="Map"/>.</summary>
    private string Plugin(string id, IReadOnlyList<CollisionTriangle>? collision, params (string File, string Json)[] records)
    {
        var dir = _temp.Add(id, null, records);
        if (collision is not null)
        {
            var path = Path.Combine(dir, "assets", "aidyn", "map", "0x0001.glb");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, CollisionMesh.Write(collision, "test"));
        }
        return dir;
    }

    private static (string, string) Moves(string moves) => ("fix.json", $$"""[{ "patch": "aidyn:map/0x0001", "collisionMoves": {{moves}} }]""");

    [Fact]
    public void AMoveSlidesItsRunOfTheOriginalsTrianglesAndKeepsWhatTheyAre()
    {
        var set = PluginSet.Load([Plugin("aidyn", Original, ("maps.json", MapRecordJson)),
            Plugin("acre", null, Moves("""[{ "first": 1, "count": 2, "by": [0.5, 0, -0.25] }]"""))]);
        var by = new Vector3(0.5f, 0, -0.25f);
        Assert.Equal(new[] { new CollisionMove(1, 2, by) }, set.Get<MapRecord>(Map).CollisionMoves);
        Assert.Equal(
            [Original[0], Original[1] with { A = Original[1].A + by, B = Original[1].B + by, C = Original[1].C + by },
                Original[2] with { A = Original[2].A + by, B = Original[2].B + by, C = Original[2].C + by }],
            CollisionMesh.For(set, Map));
    }

    [Fact]
    public void WithoutMovesTheCollisionIsTheAssetsAsItIs()
    {
        var set = PluginSet.Load([Plugin("aidyn", Original, ("maps.json", MapRecordJson))]);
        Assert.Equal(Original, CollisionMesh.For(set, Map));
    }

    /// <summary>A mod's own collision, such as a chunk import writes from an export the moves were made in, isn't moved again.</summary>
    [Fact]
    public void ALaterPluginsOwnCollisionIsTakenAsItIs()
    {
        CollisionTriangle[] own = [Triangle(100), Triangle(200)];
        var set = PluginSet.Load([Plugin("aidyn", Original, ("maps.json", MapRecordJson)),
            Plugin("acre", null, Moves("""[{ "first": 0, "count": 1, "by": [1, 0, 0] }]""")),
            Plugin("my_village", own)]);
        Assert.Equal(own, CollisionMesh.For(set, Map));
    }

    [Fact]
    public void AMovePastTheCollisionsEndIsRefusedWithTheMap()
    {
        var set = PluginSet.Load([Plugin("aidyn", Original, ("maps.json", MapRecordJson)),
            Plugin("acre", null, Moves("""[{ "first": 2, "count": 2, "by": [1, 0, 0] }]"""))]);
        var e = Assert.Throws<InvalidDataException>(() => CollisionMesh.For(set, Map));
        Assert.Equal("aidyn:map/0x0001's collisionMoves move triangles 2 to 3, but its collision has 3.", e.Message);
    }
}
