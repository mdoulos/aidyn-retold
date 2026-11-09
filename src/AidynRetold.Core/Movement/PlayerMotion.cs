using System.Numerics;
using AidynRetold.Core.Physics;

namespace AidynRetold.Core.Movement;

/// <summary>The controls that move the player for one tick.</summary>
/// <param name="Stick">The movement stick, each axis −1 to 1, up and right positive.</param>
/// <param name="Sneak">Z held: sneak instead of walking or running.</param>
/// <param name="Held">
/// Something else has the stick, as the trek camera's orbit does with R held: he stands, and neither turns nor reads it.
/// </param>
/// <param name="Jump">The jump pressed this tick (<see cref="PlayerMotion.Jumping"/>), a port addition.</param>
public readonly record struct MoveInput(Vector2 Stick, bool Sneak = false, bool Held = false, bool Run = false, bool Jump = false);

/// <summary>Plays an actor's animations and reports how far their root moves.</summary>
public interface IRootMotion
{
    /// <summary>
    /// Advances the animation one tick toward <paramref name="request"/> and returns how far its
    /// root moved forward (the Borg7 header's <c>+0x2c</c>, negated as the game does). That's nothing on
    /// a tick that enters a state or the two after it.
    /// </summary>
    float Tick(int request);
}

/// <summary>
/// The player walking about (<c>Actor::Move</c>, which combat also uses, and the player loop in
/// <c>PlayerData.cpp</c>): the stick drives his <see cref="Body"/>, a sphere of radius 0.5 that his
/// animations move. Walking and running speeds are his animations' root motion, about 1.26 and
/// 3.6 units a second. What only the player does is here: the stick read along where the camera
/// looks, sneaking, the turn a conversation asks of him, keeping to the ground on plain collision,
/// and finding he has fallen out of the world, which trek rescues him from (<see cref="Trek.TrekLoop"/>).
/// </summary>
public sealed class PlayerMotion
{
    /// <summary>Alaron's sphere's radius.</summary>
    public const float Radius = 0.5f;

    /// <summary>Falling faster than this a tick, he has fallen out of the world (<c>ConfirmPlayerWithinZone</c>, <c>0x8000c078</c>).</summary>
    public const float FallLimit = 0.3f;

    /// <summary>Below this height, he has fallen out of the world (<c>ConfirmPlayerWithinZone</c>).</summary>
    public const float Floor = -50;

    /// <summary>
    /// Falling faster than this a tick, he can't steer and his animation doesn't move him (<c>Bodies.Process</c>, <c>0x800132f4</c>:
    /// <c>0x800cdb60</c>, −0.048, for a body of Alaron's actor, <c>+0x70B</c>).
    /// </summary>
    public const float Falling = -0.048f;

    /// <summary>
    /// The ticks he may be falling by his velocity without dropping before he steers again (<see cref="Pinned"/>), the port's:
    /// a quarter of a second.
    /// </summary>
    public const int PinnedTicks = 15;

    /// <summary>Less than this lower than where <see cref="Pinned"/>'s count began, he hasn't dropped.</summary>
    private const float PinnedDrop = 0.05f;

    /// <summary>
    /// The ticks in a row he has been falling by his velocity (<see cref="Falling"/>), on that tick or the one before, yet stands,
    /// after each tick's collision, no more than <see cref="PinnedDrop"/> lower than as the count began. From
    /// <see cref="PinnedTicks"/> he steers and his animation moves him again, the port's, as the user chose to rescue rather than
    /// leave him stuck: against a leaning plain wall the ground rule, or a wall facing it, can hold him up while plain collision's
    /// push keeps his velocity falling, and 1.0 leaves him there with no way out. A real fall drops him at least 0.048 a tick, so
    /// it never counts past 2.
    /// </summary>
    public int Pinned { get; private set; }

    /// <summary>His height as <see cref="Pinned"/>'s count began.</summary>
    private float _pinnedFrom;

    /// <summary>Whether he was falling (<see cref="Falling"/>) on the last tick.</summary>
    private bool _fellLastTick;

    /// <summary>
    /// His upward speed a tick as he jumps: the original's own, its <c>!flea</c> cheat's jump's (<c>Body.ApplyInput</c>, the 0.084 at
    /// <c>0x800CDC58</c>, added at <c>0x80015080</c>), which gravity brings to <see cref="JumpHeight"/> after 28 ticks, then
    /// <see cref="JumpFallPull"/> brings him back in 16, about 0.73 s in the air on the flat.
    /// </summary>
    public const float JumpSpeed = 0.084f;

    /// <summary>How many times gravity pulls him as a jump comes down, the user's (Nov 9, 2026): at gravity's own he hung in the air, 28 ticks down as well as up.</summary>
    public const float JumpFallPull = 3;

    /// <summary>The falling speed a tick that pull stops at, just past where a jump on the flat lands: off a cliff he goes on by gravity alone, as a fall too long is the world's edge (<see cref="FallLimit"/>).</summary>
    public const float JumpFallMost = 0.15f;

    /// <summary>
    /// How high a jump lifts his sphere's centre above where it left the ground, as each tick adds gravity and then moves: 1.134. His
    /// feet clear about 1.1, and the edge of a ledge up to about 1.6 is under his middle at the top, so he climbs it, but every gate the
    /// story shuts is 2 or more tall.
    /// </summary>
    public static readonly float JumpHeight = Rise(JumpSpeed);

    private static float Rise(float speed)
    {
        var (height, v) = (0f, speed);
        while ((v -= Body.Gravity) > 0)
            height += v;
        return height;
    }

    /// <summary>The ticks a jump pressed just before he lands is kept for, so it jumps as he does.</summary>
    public const int JumpBufferTicks = 6;

    /// <summary>The longest a jump lasts, 3 seconds: off a cliff, the falling rule takes over from it.</summary>
    public const int JumpMostTicks = 180;

    private int _jumpTicks;

    /// <summary>
    /// Whether he's in a jump, from leaving the ground until he's back on it (the port's, a PC game's jump, the user's choice): he
    /// carries the speed he left the ground at along his heading, which the stick still turns; his pose holds as it was, or plays
    /// <see cref="Body.Jump"/> if his actor has it; the falling rule's stop doesn't hold him; and plain collision doesn't press him
    /// down. Trek treads the trigger floors he passes over (<see cref="SpherePhysics.TreadBelow"/>), and tests map objects from where
    /// he left the ground (<see cref="TouchPoint"/>), so a hop skips none.
    /// </summary>
    public bool Jumping { get; private set; }

    /// <summary>Whether he may jump: trek's, by the Jump setting and the map he's in (<see cref="Plugins.Records.MapRecord.NoJump"/>).</summary>
    public bool CanJump { get; set; } = true;

    private int _jumpBuffer;

    /// <summary>His speed forward a tick on the ground, smoothed over the steps' unevenness, which a jump carries.</summary>
    private float _groundForward;

    /// <summary>The speed forward a tick the jump carries.</summary>
    private float _carry;

    /// <summary>His sphere's height as the jump left the ground.</summary>
    private float _takeoff;

    /// <summary>Where he counts as being for map objects: his sphere's centre, but in a jump at the height it left the ground.</summary>
    public Vector3 TouchPoint => Jumping ? Sphere.Position with { Y = _takeoff } : Sphere.Position;

    public PlayerMotion(Vector3 position, Vector2 heading)
    {
        Body = new Body(position, Radius) { Heading = Vector2.Normalize(heading) };
    }

    /// <summary>His body, the first that <c>Bodies.Process</c> steps.</summary>
    public Body Body { get; }

    public CollisionSphere Sphere => Body.Sphere;

    /// <summary>Whether he stood on the ground on the last step: touching something that faces up.</summary>
    public bool Grounded => Sphere.Touching && Sphere.ContactNormal.Y > 0.5f && Sphere.Velocity.Y > -0.048f;

    /// <summary>
    /// Whether he fell out of the world on the last step (<c>ConfirmPlayerWithinZone</c>): faster than <see cref="FallLimit"/> a
    /// tick, or below <see cref="Floor"/>. He's stopped, and it's trek's to put him somewhere (<see cref="Trek.TrekLoop"/>).
    /// </summary>
    public bool FellOut { get; private set; }

    /// <summary>
    /// Where the nearest thing he could use is, in the world, or null for none (body <c>+0x110</c>, and <c>+0x10c</c> its distance): what his head
    /// turns toward (<see cref="HeadTurn"/>). Trek keeps it each tick.
    /// </summary>
    public Vector3? LookingAt { get; set; }

    /// <summary>How his head has turned, which the view steps as it poses him, since it turns from where the animation has put his neck.</summary>
    public HeadTurn Head { get; } = new();

    /// <summary>
    /// The way he faces, in X and Z. The original keeps the opposite, <c>facing</c>, which is why
    /// the camera's goal is along it.
    /// </summary>
    public Vector2 Heading
    {
        get => Body.Heading;
        set => Body.Heading = value;
    }

    /// <summary>The animation he's asked for.</summary>
    public int Request => Body.Request;

    /// <summary>
    /// The heading he's turning to where he stands (<c>Actor::SetFacing</c>, as a conversation
    /// turns him to the one he talks to), or null. He turns 10° a frame, as other bodies do, and
    /// the stick cancels it.
    /// </summary>
    public Vector2? TurnTo { get; private set; }

    /// <summary>
    /// An animation asked for in place of standing, for the next tick only, as a conversation asks
    /// for his talking (request 4). Turning, or the stick, overrides it, as <c>Actor::Move</c> does.
    /// </summary>
    public int? Asked { get; set; }

    /// <summary>
    /// Ends a jump and forgets a press waiting for one, as he's put somewhere: through a teleporter, by a load, back from a battle.
    /// A jump carried on there would touch from where it left the ground before.
    /// </summary>
    public void Settle()
    {
        Jumping = false;
        _jumpBuffer = 0;
        _carry = 0;
    }

    /// <summary>Turns him to face <paramref name="heading"/> where he stands (<c>Actor::SetFacing</c>).</summary>
    public void Face(Vector2 heading) =>
        TurnTo = heading.LengthSquared() > SpherePhysics.Epsilon * SpherePhysics.Epsilon ? Vector2.Normalize(heading) : null;

    /// <summary>How many animation ticks he plays per tick (<see cref="Body.Pace"/>).</summary>
    public float Pace
    {
        get => Body.Pace;
        set => Body.Pace = value;
    }

    /// <summary>Where his feet are: the sphere's bottom.</summary>
    public Vector3 Feet => Sphere.Position - new Vector3(0, Sphere.Radius, 0);

    /// <summary>One 60 Hz tick.</summary>
    /// <param name="look">The way the camera looks across the ground, which up on the stick moves him.</param>
    public void Step(CollisionWorld world, MoveInput input, Vector2 look, IRootMotion animation)
    {
        Control(input, look);
        StartJump(world, input);

        if (Jumping && Sphere.Velocity.Y <= 0 && Sphere.Velocity.Y > -JumpFallMost)
            Sphere.Velocity.Y -= Body.Gravity * (JumpFallPull - 1);

        var height = Sphere.Position.Y;
        SpherePhysics.Step(world, Sphere);
        if (Jumping)
        {
            // Down on anything that faces up, on the way down: the ground, a ledge, or a slope too steep to stand on, which he then
            // slides down as he would have walked onto it. A wall he's against faces across, and doesn't end it; the floor under it
            // does, whatever he last touched. Until then, the trigger floors under him down to where he left the ground.
            if (Sphere.Velocity.Y <= 0 && (Grounded || Below(world, 0.1f, 0.05f)) || ++_jumpTicks >= JumpMostTicks)
            {
                Jumping = false;
                Land(world);
                height = Sphere.Position.Y;
            }
            else
                SpherePhysics.TreadBelow(world, Sphere, _takeoff - Radius - 0.25f);
        }
        if (Jumping)
        {
            // He carries the speed he left the ground at, until he's carried into a wall: most surfaces keep no velocity on contact,
            // so each tick's push into it would hold him up there, sliding down it at a crawl.
            var contact = Sphere.ContactNormal;
            var across = new Vector2(contact.X, contact.Z);
            if (Sphere.Touching && contact.Y < 0.5f && Vector2.Dot(Heading, across) < 0)
                _carry = 0;
            // And on the way down he's eased a hair off a wall he's against, as a sphere that barely moves while it touches something
            // settles where it is (SpherePhysics.Step), which would hold him up there for good.
            if (Sphere.Touching && contact.Y < 0.5f && Sphere.Velocity.Y <= 0 && across != Vector2.Zero)
            {
                across = Vector2.Normalize(across) * 0.02f;
                Sphere.Position += new Vector3(across.X, 0, across.Y);
            }
            // His pose holds unless his actor has a jump.
            if (animation is Actors.IActorMotion actor && actor.Knows(Body.Jump))
            {
                Body.Request = Body.Jump;
                Body.Animate(animation);
            }
            Body.Advance(_carry);
            EndStep();
            return;
        }

        // On plain collision, which keeps velocity, don't slide down when there's ground just below.
        if (Sphere.Surface is 1 or 2 && SpherePhysics.Ground(world, Sphere.Position, Radius, 0.05f) && Sphere.Position.Y < height)
            Sphere.Position.Y = height;
        // And press down onto it.
        Body.Press();

        var forward = Body.Animate(animation);
        _groundForward += (forward - _groundForward) * 0.2f;
        // Falling: no steering, unless he has been held where he is for a while, falling by his velocity alone.
        var falling = Sphere.Velocity.Y <= Falling;
        // A frame's push lands on one of its ticks, so a body held so can be falling on every other tick only.
        var fellThisFrame = falling || _fellLastTick;
        _fellLastTick = falling;
        if (!fellThisFrame)
            Pinned = 0;
        else if (Pinned == 0)
            (Pinned, _pinnedFrom) = (1, height);
        else
            Pinned = Sphere.Position.Y > _pinnedFrom - PinnedDrop ? Pinned + 1 : 0;
        if (falling && Pinned < PinnedTicks)
            Sphere.Velocity.X = Sphere.Velocity.Z = 0;
        else
            Body.Advance(forward);
        EndStep();
    }

    /// <summary>
    /// A jump pressed, or kept from the last few ticks, leaves the ground if he's on it and may jump: ground he could stand on,
    /// facing more than 60° up, within a hair below, so plain collision's push down, which can leave him falling by his velocity
    /// where he stands, doesn't hold it back, and a slope too steep to stand on can't be climbed by jumping.
    /// </summary>
    private void StartJump(CollisionWorld world, MoveInput input)
    {
        if (input.Jump)
            _jumpBuffer = JumpBufferTicks;
        else if (_jumpBuffer > 0)
            _jumpBuffer--;
        if (_jumpBuffer == 0 || Jumping || !CanJump || input.Held || !(Grounded || Below(world, 0.05f, 0.5f)))
            return;
        _jumpBuffer = 0;
        _jumpTicks = 0;
        Jumping = true;
        _takeoff = Sphere.Position.Y;
        _carry = _groundForward;
        Sphere.Velocity.Y = JumpSpeed;
        _fellLastTick = false;
        Pinned = 0;
    }

    /// <summary>
    /// Sets him on what's under him as a jump ends, stopped. Collision looks ahead along a sphere's speed, so at a jump's it stops him up to about 0.1 short of the ground, where
    /// a sphere that touches something and barely moves stays (<see cref="SpherePhysics.Step"/>).
    /// </summary>
    private void Land(CollisionWorld world)
    {
        if (!SpherePhysics.SweepNearest(world, Sphere.Position, Sphere.Position - new Vector3(0, 0.2f, 0), Radius, skipSeeThrough: false, 0.05f, out var point, out var normal))
            return;
        Sphere.Position = point + normal * Radius;
        Sphere.Velocity.Y = 0;
    }

    /// <summary>Whether something facing at least <paramref name="up"/> upward is within <paramref name="depth"/> below his sphere.</summary>
    private bool Below(CollisionWorld world, float depth, float up) =>
        SpherePhysics.SweepNearest(world, Sphere.Position, Sphere.Position - new Vector3(0, depth, 0), Radius, skipSeeThrough: false, up, out _, out _);

    /// <summary>Whether he has fallen out of the world (<see cref="FellOut"/>), which stops him and ends a jump.</summary>
    private void EndStep()
    {
        FellOut = Sphere.Velocity.Y < -FallLimit || Sphere.Position.Y < Floor;
        if (!FellOut)
            return;
        Sphere.Velocity = Vector3.Zero;
        Jumping = false;
    }

    /// <summary>Reads the controls (<c>Actor::Move</c>, the trek parts).</summary>
    private void Control(MoveInput input, Vector2 look)
    {
        Body.Request = Asked ?? Body.Idle;
        Asked = null;
        if (input.Held)
            return;

        var tilt = input.Stick.Length();
        if (tilt <= Body.Deadzone)
        {
            if (TurnTo is { } to)
            {
                Body.Request = Body.Idle;
                if (Body.TurnToward(to))
                    TurnTo = null;
            }
            return;
        }
        TurnTo = null;
        // Up on the stick is the way the camera looks, and right is to its right.
        var wanted = Vector2.Normalize(Facing.Rotate(new Vector2(-input.Stick.X, input.Stick.Y), look.X, look.Y));
        Body.Go(tilt, wanted, input.Sneak, input.Run);
    }
}
