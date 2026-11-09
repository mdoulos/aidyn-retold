using System.Numerics;

namespace AidynRetold.Core.Movement;

/// <summary>
/// How the player's head turns toward the nearest thing he could use (<c>Bodies.Render</c>, <c>0x800142d4</c>–<c>0x800144b8</c>): a direction it
/// keeps on the ground (the body's <c>+0x48</c>) eases toward that thing, or back to where the neck faces when there's none, by 3° a tick, and
/// the head turns from the neck toward it by at most 80°. Only the player's body turns its head.
/// </summary>
public sealed class HeadTurn
{
    /// <summary>How far the kept direction eases in a tick: 1° (<c>0x800cdc00</c>) times 3, times the frame's ticks.</summary>
    public const float EasePerTick = 3 * MathF.PI / 180;

    /// <summary>How far the head turns from the neck (<c>0x3fb2b7fe</c>), in radians: 80°.</summary>
    public const float MostFromNeck = 1.39624f;

    private Vector2? _kept;

    /// <summary>
    /// One tick: eases the kept direction and returns the way the head faces, on the ground, in X and Z.
    /// </summary>
    /// <param name="neck">The way the neck faces, on the ground: its locator's direction as the animation has posed it.</param>
    /// <param name="head">Where the head is, in the world.</param>
    /// <param name="goal">What it turns toward, in the world, or null to ease back to the neck.</param>
    public Vector2 Step(Vector2 neck, Vector3 head, Vector3? goal)
    {
        var way = goal is { } g ? new Vector2(g.X - head.X, g.Z - head.Z) : neck;
        // Standing right on what it looks at has no way to look.
        if (way.LengthSquared() < 1e-8f)
            way = neck;
        var kept = Facing.TurnAtMost(_kept ?? neck, way, EasePerTick);
        _kept = kept;
        return Facing.TurnAtMost(neck, kept, MostFromNeck);
    }
}
