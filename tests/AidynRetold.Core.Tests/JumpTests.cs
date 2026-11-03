using System.Numerics;
using AidynRetold.Core.Movement;
using AidynRetold.Core.Physics;
using AidynRetold.Plugins.Assets;

namespace AidynRetold.Core.Tests;

/// <summary>The player's jump, the port's (<see cref="PlayerMotion.Jumping"/>): docs/systems/movement.md.</summary>
public sealed class JumpTests
{
    private static readonly Vector2 East = new(1, 0);

    /// <summary>He stands on the flat at the origin, facing east, settled.</summary>
    private static PlayerMotion Standing(CollisionWorld world, IRootMotion motion)
    {
        var player = new PlayerMotion(new Vector3(0, 0.5f, 0), East);
        for (var i = 0; i < 10; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        // On plain collision he touches the floor every other tick, pressed down and set back, so it's his height that says he stands.
        Assert.InRange(player.Sphere.Position.Y, 0.49f, 0.54f);
        return player;
    }

    [Fact]
    public void AStandingJumpRisesItsHeightAndComesBackDownWhereItLeft()
    {
        var world = Worlds.Flat();
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var start = player.Sphere.Position;

        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        Assert.True(player.Jumping);
        var top = start.Y;
        var ticks = 1;
        for (; player.Jumping && ticks < 200; ticks++)
        {
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
            top = MathF.Max(top, player.Sphere.Position.Y);
        }
        // The flea cheat's lift: 1.134, about 0.93 s in the air.
        Assert.Equal(1.134f, PlayerMotion.JumpHeight, 0.001f);
        Assert.InRange(top - start.Y, PlayerMotion.JumpHeight - 0.02f, PlayerMotion.JumpHeight + 0.01f);
        Assert.InRange(ticks, 52, 60);
        for (var i = 0; i < 10; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        Assert.InRange(Vector3.Distance(player.Sphere.Position, start), 0, 0.02f);
    }

    [Theory]
    [InlineData(Worlds.Grass)]
    [InlineData(1)]
    [InlineData(2)]
    public void HeJumpsOffEveryKindOfGround(int surface)
    {
        // Plain collision (1 and 2) keeps velocity and presses him down each frame, so he can stand there falling by his velocity.
        var world = new CollisionWorld();
        world.Add(Worlds.Chunk(), Worlds.Floor(0, -50, 60, -50, 60, surface), Vector3.Zero);
        var motion = new SteadyMotion(0.06f);
        var player = new PlayerMotion(new Vector3(0, 0.5f, 0), East);
        for (var i = 0; i < 60; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        var jumped = false;
        for (var i = 0; i < 4 && !jumped; i++)
        {
            player.Step(world, new MoveInput(Vector2.Zero, Jump: i == 0), East, motion);
            jumped = player.Jumping;
        }
        Assert.True(jumped);
    }

    [Fact]
    public void ARunningJumpCarriesItsSpeedAndTheFallingRuleDoesntStopIt()
    {
        var world = Worlds.Flat();
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var run = new MoveInput(new Vector2(0, 1));
        for (var i = 0; i < 60; i++)
            player.Step(world, run, East, motion);
        var from = player.Sphere.Position.X;

        player.Step(world, run with { Jump = true }, East, motion);
        var ticks = 1;
        for (; player.Jumping && ticks < 200; ticks++)
            player.Step(world, run, East, motion);
        // 0.06 a tick through the whole flight, the descent included, where falling would stop a walk.
        Assert.InRange(player.Sphere.Position.X - from, 0.06f * (ticks - 2), 0.06f * (ticks + 1));
    }

    [Fact]
    public void ItJumpsOnlyFromTheGroundAndNotWhereItsForbidden()
    {
        var world = Worlds.Flat();
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        for (var i = 0; i < 10; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        var rising = player.Sphere.Velocity.Y;
        // No second jump in the air.
        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        Assert.True(player.Sphere.Velocity.Y < rising);

        var held = Standing(world, motion);
        held.CanJump = false;
        held.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        Assert.False(held.Jumping);
    }

    [Theory]
    [InlineData(Worlds.Grass)]
    [InlineData(1)]
    public void AJumpIntoAWallComesDownAndLands(int surface)
    {
        // A wall across the way at X 3, 5 tall; he runs at it and jumps, holding forward the whole time.
        var world = new CollisionWorld();
        world.Add(Worlds.Chunk(), [.. Worlds.Floor(0, -50, 60, -50, 60, surface), .. Worlds.Wall(3, 5)], Vector3.Zero);
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var run = new MoveInput(new Vector2(0, 1));
        while (player.Sphere.Position.X < 1.8f)
            player.Step(world, run, East, motion);
        player.Step(world, run with { Jump = true }, East, motion);
        Assert.True(player.Jumping);
        Assert.Equal(PlayerMotion.JumpSpeed - Body.Gravity, player.Sphere.Velocity.Y, 0.001f);
        var ticks = 0;
        for (; player.Jumping && ticks < 300; ticks++)
            player.Step(world, run, East, motion);
        // The wall stops his rise as he meets it (most surfaces keep no velocity), so he's down sooner than a whole flight.
        Assert.InRange(ticks, 20, 70);
        for (var i = 0; i < 10; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        Assert.InRange(player.Sphere.Position.Y, 0.45f, 0.55f);
        Assert.True(player.Sphere.Position.X < 3);
    }

    [Fact]
    public void AJumpOntoASlopeTooSteepToStandOnEndsThere()
    {
        // A slope rising 2 for every 1 across from X 2, too steep to stand on (its normal 0.45 up), plain as the hillsides are.
        var world = new CollisionWorld();
        world.Add(Worlds.Chunk(), [
            .. Worlds.Floor(0, -50, 2, -50, 60, 1),
            new CollisionTriangle(new(2, 0, -50), new(2, 0, 60), new(6, 8, -50), 1, CollisionRole.Normal, CollisionFlags.None),
            new CollisionTriangle(new(6, 8, -50), new(2, 0, 60), new(6, 8, 60), 1, CollisionRole.Normal, CollisionFlags.None),
        ], Vector3.Zero);
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var run = new MoveInput(new Vector2(0, 1));
        while (player.Sphere.Position.X < 1.6f)
            player.Step(world, run, East, motion);
        player.Step(world, run with { Jump = true }, East, motion);
        var ticks = 0;
        for (; player.Jumping && ticks < 300; ticks++)
            player.Step(world, run, East, motion);
        Assert.InRange(ticks, 10, 70);
    }

    [Fact]
    public void AJumpPressedJustBeforeLandingJumpsAsItLands()
    {
        var world = Worlds.Flat();
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        while (player.Sphere.Position.Y > 0.5f + 0.1f || player.Sphere.Velocity.Y > 0)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        // A few ticks short of the ground, the press is kept until he's on it.
        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        var again = false;
        for (var i = 0; i < PlayerMotion.JumpBufferTicks && !again; i++)
        {
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
            again = player.Sphere.Velocity.Y > 0;
        }
        Assert.True(again);
    }

    [Fact]
    public void AHopOverATriggerFloorTreadsIt()
    {
        // A trigger strip across the way from X 5 to 6, as a story's patch of ground that starts a scene.
        var world = Worlds.Flat();
        var part = Worlds.Chunk(1);
        world.Add(part, [
            new CollisionTriangle(new(5, 0, -5), new(5, 0, 5), new(6, 0, -5), Worlds.Grass, CollisionRole.Trigger, CollisionFlags.None, 7),
            new CollisionTriangle(new(6, 0, -5), new(5, 0, 5), new(6, 0, 5), Worlds.Grass, CollisionRole.Trigger, CollisionFlags.None, 7),
        ], Vector3.Zero);
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var run = new MoveInput(new Vector2(0, 1));
        while (player.Sphere.Position.X < 3.5f)
            player.Step(world, run, East, motion);

        player.Step(world, run with { Jump = true }, East, motion);
        var trod = false;
        var airborneOver = false;
        while (player.Jumping)
        {
            player.Step(world, run, East, motion);
            trod |= player.Sphere.Trod.Contains((part, 7));
            airborneOver |= player.Sphere.Position.X is > 5.2f and < 5.8f && !player.Sphere.Touching;
        }
        Assert.True(airborneOver);
        Assert.True(trod);
    }

    [Fact]
    public void InAJumpMapObjectsAreTestedFromWhereItLeftTheGround()
    {
        var world = Worlds.Flat();
        var motion = new SteadyMotion(0.06f);
        var player = Standing(world, motion);
        var ground = player.Sphere.Position.Y;
        player.Step(world, new MoveInput(Vector2.Zero, Jump: true), East, motion);
        for (var i = 0; i < 20; i++)
            player.Step(world, new MoveInput(Vector2.Zero), East, motion);
        Assert.True(player.Sphere.Position.Y > ground + 0.5f);
        Assert.Equal(ground, player.TouchPoint.Y);
    }
}
