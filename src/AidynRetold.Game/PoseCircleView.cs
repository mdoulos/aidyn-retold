using AidynRetold.Core.Physics;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Game;

/// <summary>
/// A debug ring of entities' actors around a point, facing it, each playing its actor's states in turn from the first,
/// with a label a couple of feet above its head naming the state (<c>--poses members</c>, docs/systems/runtime.md).
/// The actors are bare scenes, lit by the sky alone, not bodies: nothing pushes or shadows them.
/// </summary>
public sealed partial class PoseCircleView : Node3D
{
    /// <summary>A couple of feet over the head, in scene units.</summary>
    private const float LabelAbove = 0.6f;

    private readonly List<Posed> _posed = [];
    private readonly CollisionWorld _world;
    private double _fraction;

    private PoseCircleView(CollisionWorld world)
    {
        _world = world;
        Name = "PoseCircle";
    }

    public PoseCircleView()
    {
        _world = null!;
    }

    /// <summary>
    /// The ring for <paramref name="spec"/>: <c>members</c> for every recruitable party member (their records' member
    /// flags) and Trahern, or entity IDs joined with <c>,</c>. Entities with no animated actor are skipped.
    /// </summary>
    public static PoseCircleView Build(PluginSet plugins, Models models, CollisionWorld world, string spec, Vector3 centre)
    {
        var entities = spec == "members"
            ? plugins.All<EntityRecord>(RecordTypes.Entity).Where(e => e.Member is not null || e.Name == "Trahern").OrderBy(e => e.Name).ToList()
            : spec.Split(',').Select(id => plugins.Get<EntityRecord>(RecordId.Parse(id))).ToList();
        var view = new PoseCircleView(world);
        var radius = Math.Max(3f, 0.35f * entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (entity.Body is not { } body || plugins.TryGet<ActorRecord>(body.Actor) is not { } actor
                || models.InstantiateActor(actor) is not var (node, animator) || animator is null)
            {
                GD.PushWarning($"--poses: {entity.Id} ({entity.Name}) has no animated actor.");
                continue;
            }
            var angle = Mathf.Tau * i / entities.Count;
            var place = centre + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            var toCentre = centre - place;
            node.Position = place;
            node.Rotation = new Vector3(0, Mathf.Atan2(toCentre.X, toCentre.Z), 0);
            node.Scale = Vector3.One * body.Scale;
            animator.Driven = true;
            var above = body.Height * body.Scale + LabelAbove;
            var label = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.0022f,
                FontSize = 36,
                OutlineSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Position = place + new Vector3(0, above, 0),
            };
            view.AddChild(node);
            view.AddChild(label);
            var posed = new Posed(entity.Name, actor, node, animator, label, above);
            view._posed.Add(posed);
            posed.Enter(0);
        }
        return view;
    }

    public override void _Process(double delta)
    {
        foreach (var p in _posed)
            p.Ground(_world);
        _fraction += delta * 60;
        for (; _fraction >= 1; _fraction--)
            foreach (var p in _posed)
                p.Tick();
    }

    /// <summary>One actor in the ring, and where it is in its states.</summary>
    private sealed class Posed(string name, ActorRecord actor, Node3D node, ActorAnimator animator, Label3D label, float labelAbove)
    {
        private int _state;
        private bool _grounded;

        /// <summary>Plays <paramref name="state"/> from its first tick and names it on the label.</summary>
        public void Enter(int state)
        {
            _state = state;
            animator.Enter(state);
            var length = animator.Machine.Length(state);
            label.Text = $"{name}\nstate {state + 1} of {actor.States.Count}: {RequestName(RequestFor(state))}\n{actor.States[state].Animation}, {length} ticks";
        }

        /// <summary>A tick on: the next state once this one's animation has shown its last frame.</summary>
        public void Tick()
        {
            if (animator.Machine.Clock >= animator.Machine.Length(_state))
                Enter((_state + 1) % actor.States.Count);
            else
                animator.TickMotion(animator.Machine.Request);
        }

        /// <summary>Sets it on the ground once the chunk's collision is loaded.</summary>
        public void Ground(CollisionWorld world)
        {
            if (_grounded)
                return;
            var at = node.Position.ToNumerics();
            var up = new System.Numerics.Vector3(0, 3, 0);
            if (!SpherePhysics.Sweep(world, at + up, at - up, 0.05f, true, out var point, out _))
                return;
            _grounded = true;
            node.Position = new Vector3(at.X, point.Y, at.Z);
            label.Position = node.Position + new Vector3(0, labelAbove, 0);
        }

        /// <summary>The request whose branches most often lead to the state, or -1 for one no branch leads to.</summary>
        private int RequestFor(int state) =>
            actor.States.SelectMany(s => s.Branches).Where(b => b.State == state && b.Request != 255)
                .GroupBy(b => b.Request).OrderByDescending(g => g.Count()).Select(g => (int?)g.Key).FirstOrDefault() ?? -1;

        /// <summary>The requests' names (docs/formats/actors.md, docs/systems/combat.md), or the number.</summary>
        private static string RequestName(int request) => request switch
        {
            -1 => "no request leads here",
            0 => "idle (0)",
            2 => "walk (2)",
            3 => "run (3)",
            4 => "talk (4)",
            5 => "cast (5)",
            7 => "hit (7)",
            8 => "dodge (8)",
            9 => "dying (9)",
            10 => "helped (10)",
            14 => "shot (14)",
            15 => "throw (15)",
            >= 12 and <= 18 => $"attack ({request})",
            19 => "gesture (19)",
            25 => "sneak (25)",
            _ => $"request {request}",
        };
    }
}
