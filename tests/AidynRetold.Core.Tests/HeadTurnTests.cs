using System.Numerics;
using AidynRetold.Core.MapObjects;
using AidynRetold.Core.Movement;
using AidynRetold.Core.Rng;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

public class HeadTurnTests
{
    private static readonly Vector2 Ahead = new(0, 1);
    private const float Degree = MathF.PI / 180;

    private static float Between(Vector2 a, Vector2 b) => MathF.Abs(MathF.Atan2(a.X * b.Y - a.Y * b.X, Vector2.Dot(a, b))) / Degree;

    [Fact]
    public void EasesThreeDegreesATickTowardWhatHeCanUse()
    {
        var head = new HeadTurn();
        var off = new Vector3(10, 0, 0);
        var first = head.Step(Ahead, Vector3.Zero, off);
        Assert.Equal(3, Between(Ahead, first), 0.01f);
        var second = head.Step(Ahead, Vector3.Zero, off);
        Assert.Equal(6, Between(Ahead, second), 0.01f);
    }

    [Fact]
    public void TurnsNoMoreThanEightyDegreesFromTheNeck()
    {
        var head = new HeadTurn();
        // Behind him and a little to the side: past what the head can reach.
        var behind = new Vector3(1, 0, -10);
        var way = Ahead;
        for (var i = 0; i < 120; i++)
            way = head.Step(Ahead, Vector3.Zero, behind);
        Assert.Equal(80, Between(Ahead, way), 0.01f);
    }

    [Fact]
    public void FacesItOnceWithinReach()
    {
        var head = new HeadTurn();
        var off = new Vector3(10, 3, 10);
        var way = Ahead;
        for (var i = 0; i < 60; i++)
            way = head.Step(Ahead, Vector3.Zero, off);
        Assert.Equal(45, Between(Ahead, way), 0.01f);
    }

    [Fact]
    public void EasesBackToTheNeckWithNothingToLookAt()
    {
        var head = new HeadTurn();
        for (var i = 0; i < 20; i++)
            head.Step(Ahead, Vector3.Zero, new Vector3(10, 0, 0));
        var way = head.Step(Ahead, Vector3.Zero, null);
        Assert.Equal(57, Between(Ahead, way), 0.01f);
        for (var i = 0; i < 30; i++)
            way = head.Step(Ahead, Vector3.Zero, null);
        Assert.Equal(0, Between(Ahead, way), 0.01f);
    }

    [Fact]
    public void FollowsTheNeckWhenTheBodyTurns()
    {
        var head = new HeadTurn();
        head.Step(Ahead, Vector3.Zero, null);
        var neck = new Vector2(1, 0);
        var way = head.Step(neck, Vector3.Zero, null);
        // The kept direction takes its 3 degrees toward the neck, and is more than 80 from it still.
        Assert.Equal(80, Between(neck, way), 0.01f);
    }

    [Fact]
    public void TurnAtMostHoldsForLargeAngles()
    {
        var turned = Facing.TurnAtMost(Ahead, new Vector2(1, 0), 70 * Degree);
        Assert.Equal(70, Between(Ahead, turned), 0.01f);
        Assert.Equal(20, Between(new Vector2(1, 0), turned), 0.01f);
        Assert.Equal(new Vector2(1, 0), Facing.TurnAtMost(Ahead, new Vector2(1, 0), 95 * Degree));
    }

    private static MapObject Point(int index, Vector3 at, float radius, bool reachable = true) =>
        new SavePointObject(index, at, radius, PakVariant.Any, null) { Reachable = reachable };

    private static LoadedMap Map(params MapObject[] objects) =>
        LoadedMap.Activate(new MapRecord(RecordId.Hex(RecordTypes.Map, 1), objects), Worlds.Chunk(0), ItemData.Flags(), new MapObjectMemory(), ItemData.NewParty(), new Lcg(), new Vector3(100, 0, 0));

    [Fact]
    public void LooksAtTheNearestReachableWithinItsRadius()
    {
        var near = Point(0, new Vector3(2, 0, 0), 5);
        var far = Point(1, new Vector3(4, 0, 0), 5);
        var map = Map(far, near);
        Assert.Same(near, MapObjectUse.NearestReachable(map, Vector3.Zero));
    }

    [Fact]
    public void IgnoresWhatHeCantReachOrIsOutsideOf()
    {
        var unreachable = Point(0, new Vector3(1, 0, 0), 5, reachable: false);
        var outside = Point(1, new Vector3(9, 0, 0), 5);
        var tooFar = Point(2, new Vector3(150, 0, 0), 200);
        Assert.Null(MapObjectUse.NearestReachable(Map(unreachable, outside, tooFar), Vector3.Zero));
    }
}
