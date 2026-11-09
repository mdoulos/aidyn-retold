using Godot;

namespace AidynRetold.Views;

/// <summary>
/// An actor's separate head, hung from its locator: turns it across the ground as the core's <c>HeadTurn</c> says, keeping what the animation does to
/// the neck, and lets the pieces of hair in it (<c>hair_1</c> and so on, each node's origin the piece's root on the scalp) move with the head: they
/// trail behind him as he jogs and runs, flutter with his stride, and lift and press down as he jumps. Standing or walking, they settle. The hair
/// is the remade game's addition, and only a head that has such nodes moves any.
/// </summary>
/// <remarks>Works out each frame from where the head has actually gone, so it answers to whatever moves him, with no word from the core.</remarks>
public partial class HeadRig : Node
{
    /// <summary>Speed that fills the hair's trail and flutter, about the remade game's full run, units a second.</summary>
    private const float FullSpeed = 6.4f;

    /// <summary>Most the hair leans from where it hangs, in radians, by his speed or his fall.</summary>
    private const float MostTrail = 7 * Mathf.Pi / 180;

    /// <summary>Most the hair flutters either way as he runs, in radians.</summary>
    private const float MostFlutter = 3.5f * Mathf.Pi / 180;

    /// <summary>The spring the hair hangs on: how stiff and how damped. Just under critically damped, so it swings a little past as he stops.</summary>
    private const float Stiffness = 90, Damping = 11;

    private readonly Node3D _locator = null!;
    private readonly Node3D _head = null!;
    private readonly Quaternion _rest;

    /// <summary>How the head's own model is turned inside what hangs from the locator: its space is Y up, facing +Z.</summary>
    private readonly Quaternion _fit = Quaternion.Identity;
    private Piece[] _hair = [];
    private Vector3 _last;
    private Vector3 _velocity;
    private bool _placed;
    private float _clock;

    private sealed class Piece(Node3D node, Vector3 reach, float phase)
    {
        public Node3D Node { get; } = node;

        public Quaternion Rest { get; } = node.Quaternion;

        /// <summary>The way the piece lies out from its root, in the head's space, unit length.</summary>
        public Vector3 Reach { get; } = reach;

        public float Phase { get; } = phase;

        /// <summary>The piece's lean, as an axis scaled by its angle, and how fast that's changing.</summary>
        public Vector3 Lean, Swing;
    }

    public HeadRig()
    {
    }

    private HeadRig(Node3D locator, Node3D head)
    {
        _locator = locator;
        _head = head;
        _rest = head.Quaternion;
        // Godot wraps a glTF's root in a node of its own, which the root's turn is left on.
        if (head.GetChildCount() == 1 && head.GetChild(0) is Node3D { } inner)
            _fit = inner.Quaternion;
        Name = nameof(HeadRig);
    }

    /// <summary>The rig for a head hung from a locator of an actor's scene, added under the scene.</summary>
    public static HeadRig Add(Node3D scene, Node3D locator, Node3D head)
    {
        var rig = new HeadRig(locator, head);
        scene.AddChild(rig);
        return rig;
    }

    /// <summary>The rig under an actor's scene, or null if the actor has no separate head.</summary>
    public static HeadRig? Of(Node3D scene) => scene.GetNodeOrNull<HeadRig>(nameof(HeadRig));

    /// <summary>Where the head is, in the world.</summary>
    public System.Numerics.Vector3 Position
    {
        get
        {
            var at = LocatorTransform().Origin;
            return new System.Numerics.Vector3(at.X, at.Y, at.Z);
        }
    }

    /// <summary>The way the neck faces on the ground, in X and Z, or null when it's pointing straight up or down, as he lies dead.</summary>
    public System.Numerics.Vector2? NeckFacing
    {
        get
        {
            if (!_head.IsInsideTree())
                return null;
            var forward = LocatorRotation() * _rest * _fit * Vector3.Back;
            var ground = new Vector2(forward.X, forward.Z);
            return ground.LengthSquared() < 0.04f ? null : new System.Numerics.Vector2(ground.X, ground.Y);
        }
    }

    /// <summary>Turns the head to face <paramref name="way"/> on the ground, in X and Z, about the world's up, leaving the neck's tilt as it is.</summary>
    public void Turn(System.Numerics.Vector2 way)
    {
        if (NeckFacing is not { } neck)
            return;
        var turn = Mathf.Wrap(MathF.Atan2(way.X, way.Y) - MathF.Atan2(neck.X, neck.Y), -Mathf.Pi, Mathf.Pi);
        var locator = LocatorRotation();
        _head.Quaternion = locator.Inverse() * new Quaternion(Vector3.Up, turn) * locator * _rest;
    }

    /// <summary>
    /// The locator as the animation has it now. A bone attachment only follows its bone as the frame is drawn, and stands where it was imported
    /// through a tick, so the bone's own pose is read, which is current.
    /// </summary>
    private Transform3D LocatorTransform() =>
        _locator.GetParent() is BoneAttachment3D { BoneIdx: >= 0 } bone && bone.GetSkeleton() is { } skeleton
            ? skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(bone.BoneIdx) * _locator.Transform
            : _locator.GlobalTransform;

    private Quaternion LocatorRotation() => LocatorTransform().Basis.Orthonormalized().GetRotationQuaternion();

    public override void _Ready()
    {
        var hair = new List<Piece>();
        foreach (var node in Nodes.FindAll<MeshInstance3D>(_head))
        {
            if (!node.Name.ToString().StartsWith("hair_", StringComparison.Ordinal))
                continue;
            var reach = node.GetAabb().GetCenter();
            hair.Add(new Piece(node, reach.LengthSquared() > 1e-8f ? reach.Normalized() : Vector3.Down, hair.Count * 1.7f));
        }
        _hair = [.. hair];
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_hair.Length == 0 || delta <= 0 || !_head.IsInsideTree())
            return;
        var dt = (float)delta;
        var locator = LocatorTransform();
        var at = locator.Origin;
        if (!_placed)
        {
            _last = at;
            _placed = true;
        }
        var moved = (at - _last) / dt;
        _last = at;
        // A teleport or a cut isn't a speed.
        if (moved.LengthSquared() > 30 * 30)
            moved = Vector3.Zero;
        _velocity = _velocity.Lerp(moved, 1 - Mathf.Exp(-dt / 0.06f));
        var speed = new Vector2(_velocity.X, _velocity.Z).Length();
        // Past a walk, up to a full run.
        var running = Mathf.SmoothStep(2.0f, FullSpeed, speed);
        // In the head's own space, which the pieces lie in.
        var trail = (locator.Basis.Orthonormalized().GetRotationQuaternion() * _head.Quaternion * _fit).Inverse() * -_velocity / FullSpeed;
        trail = new Vector3(trail.X * Mathf.SmoothStep(1.5f, 3.2f, speed), trail.Y * 1.2f, trail.Z * Mathf.SmoothStep(1.5f, 3.2f, speed));
        trail = trail.LimitLength(1.2f);
        _clock += dt * Mathf.Tau * (1.8f + 0.25f * speed);
        foreach (var piece in _hair)
        {
            var goal = piece.Reach.Cross(trail) * MostTrail;
            goal += piece.Reach.Cross(Vector3.Up) * (Mathf.Sin(_clock + piece.Phase) * MostFlutter * running);
            piece.Swing += ((goal - piece.Lean) * Stiffness - piece.Swing * Damping) * dt;
            piece.Lean += piece.Swing * dt;
            var angle = piece.Lean.Length();
            piece.Node.Quaternion = angle > 1e-5f ? piece.Rest * new Quaternion(piece.Lean / angle, angle) : piece.Rest;
        }
    }
}
