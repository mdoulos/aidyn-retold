using AidynRetold.Core.Camp;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Tests;

/// <summary>The poses a modelled camp figure passes through between two of its poses (the user's design, Nov 9, 2026).</summary>
public sealed class CampMovesTests
{
    private static CampModelPose Pose(int clip) => new(RecordId.Parse($"acre:animation/0x{clip:x4}"));

    private static readonly CampModelPose Sit1 = Pose(12), Sit2 = Pose(13), Sit4 = Pose(15), Stand1 = Pose(9703), Stand2 = Pose(22), Lean1 = Pose(23), Lean2 = Pose(24), Lean3 = Pose(25);

    private static readonly CampFigureModel Model = new(RecordId.Parse("aidyn:actor/0x25F9"), [Stand1, Stand2], [Sit1, Sit2, Sit4], SitDown: [Lean1, Lean2, Lean3]);

    [Fact]
    public void BetweenTwoSitsItGoesThroughTheFirstUnlessItsAlreadyOneEnd()
    {
        Assert.Equal([Sit1, Sit4], CampMoves.Path(Model, Sit2, true, Sit4, true));
        Assert.Equal([Sit4], CampMoves.Path(Model, Sit1, true, Sit4, true));
        Assert.Equal([Sit1], CampMoves.Path(Model, Sit2, true, Sit1, true));
    }

    [Fact]
    public void SittingDownItLowersItselfThroughItsSitDownPosesAndStandingUpItComesBackThroughThem()
    {
        Assert.Equal([Lean1, Lean2, Lean3, Sit1, Sit4], CampMoves.Path(Model, Stand2, false, Sit4, true));
        Assert.Equal([Lean1, Lean2, Lean3, Sit1], CampMoves.Path(Model, Stand1, false, Sit1, true));
        Assert.Equal([Sit1, Lean3, Lean2, Lean1, Stand2], CampMoves.Path(Model, Sit2, true, Stand2, false));
        Assert.Equal([Lean3, Lean2, Lean1, Stand1], CampMoves.Path(Model, Sit1, true, Stand1, false));
    }

    [Fact]
    public void BetweenTwoStandsItGoesStraightThereAndSoDoesAModelWithoutSitDownPoses()
    {
        Assert.Equal([Stand2], CampMoves.Path(Model, Stand1, false, Stand2, false));
        Assert.Equal([Sit1, Sit4], CampMoves.Path(Model with { SitDown = null }, Stand1, false, Sit4, true));
    }
}
