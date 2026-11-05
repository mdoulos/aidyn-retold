using System.Numerics;
using AidynRetold.Core.Trek;

namespace AidynRetold.Core.Tests;

/// <summary>The collision byte a hunter used by A starts its battle with (<c>IMapObjectHost.Talk</c>, <c>0x80010AA8</c>–<c>0x80010B74</c>).</summary>
public sealed class TalkByteTests
{
    private static readonly Vector3 Party = new(10, 0, 10);
    private static readonly Vector2 Heading = new(0, 1);

    [Fact]
    public void ThePlayerAheadOfTheBodyIsCollisionZero()
    {
        Assert.Equal(0, TrekLoop.TalkByte(Party, Heading, Party + new Vector3(0, 0, 3)));
        Assert.Equal(0, TrekLoop.TalkByte(Party, Heading, Party + new Vector3(0.5f, 0, 3)));
    }

    [Fact]
    public void ThePlayerBehindOrBesideTheBodyIsCollisionOne()
    {
        Assert.Equal(1, TrekLoop.TalkByte(Party, Heading, Party + new Vector3(0, 0, -3)));
        Assert.Equal(1, TrekLoop.TalkByte(Party, Heading, Party + new Vector3(3, 0, 0)));
    }

    /// <summary>The cone is 60° either side: at 60° the point ahead is as far from the player as the body is, which isn't nearer.</summary>
    [Fact]
    public void TheConeIsSixtyDegreesEitherSideOfTheHeading()
    {
        var at59 = Party + 3 * new Vector3(MathF.Sin(59 * MathF.PI / 180), 0, MathF.Cos(59 * MathF.PI / 180));
        var at61 = Party + 3 * new Vector3(MathF.Sin(61 * MathF.PI / 180), 0, MathF.Cos(61 * MathF.PI / 180));
        Assert.Equal(0, TrekLoop.TalkByte(Party, Heading, at59));
        Assert.Equal(1, TrekLoop.TalkByte(Party, Heading, at61));
    }

    /// <summary>The cone is on the level: a player above or below the body counts by his place across the ground alone.</summary>
    [Fact]
    public void HeightDoesntTurnTheCone()
    {
        Assert.Equal(0, TrekLoop.TalkByte(Party, Heading, Party + new Vector3(0, 5, 3)));
    }
}
