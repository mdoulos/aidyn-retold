using AidynEnhanced.Core.Physics;
using AidynEnhanced.Core.Trek;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;
using AidynEnhanced.Views;
using Godot;

namespace AidynEnhanced.Game;

/// <summary>
/// A debug ring of entities' actors around a point, facing it and idling, each with a label naming it, its voice and its pitch
/// (<c>--voices members</c>, docs/systems/runtime.md). A near one is talked to with A: it says the same two lines as every other,
/// one plain and one pleading, through trek's conversation box, so the voices (<see cref="TextReveal"/>) can be compared by ear.
/// As the pose ring (<see cref="PoseCircleView"/>), the actors are bare scenes, lit by the sky alone, not bodies.
/// </summary>
public sealed partial class VoiceCircleView : Node3D
{
    /// <summary>How near the player's feet must be to a ring member's for A to talk to it, in scene units.</summary>
    public const float Reach = 2f;

    /// <summary>A couple of feet over the head, in scene units.</summary>
    private const float LabelAbove = 0.6f;

    /// <summary>What everyone in the ring says, with the name in place of <c>{name}</c>: one plain line, then a pleading one.</summary>
    private static readonly string[] Lines =
    [
        "I am {name}. The road north is long, and the nights grow cold.",
        "Will you walk with us? We need every sword we can find!",
    ];

    /// <summary>How far from the ring's centre a wanderer gets a label, in scene units: the guard by Gwernia's bridge.</summary>
    public const float WandererLabels = 40f;

    private readonly List<Member> _members = [];
    private readonly Dictionary<Wanderer, Label3D> _wandererLabels = [];
    private readonly CollisionWorld _world;
    private PluginSet _plugins = null!;
    private Vector3 _centre;
    private double _fraction;

    private VoiceCircleView(CollisionWorld world)
    {
        _world = world;
        Name = "VoiceCircle";
    }

    public VoiceCircleView()
    {
        _world = null!;
    }

    /// <summary>
    /// The ring for <paramref name="spec"/>: <c>members</c> for every recruitable party member and Trahern, or entity IDs joined
    /// with <c>,</c>. Entities with no animated actor are skipped.
    /// </summary>
    public static VoiceCircleView Build(PluginSet plugins, Models models, CollisionWorld world, string spec, Vector3 centre)
    {
        var entities = spec == "members"
            ? plugins.All<EntityRecord>(RecordTypes.Entity).Where(e => e.Member is not null || e.Name == "Trahern").OrderBy(e => e.Name).ToList()
            : spec.Split(',').Select(id => plugins.Get<EntityRecord>(RecordId.Parse(id))).ToList();
        var view = new VoiceCircleView(world) { _plugins = plugins, _centre = centre };
        var radius = Math.Max(3f, 0.35f * entities.Count);
        for (var i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (entity.Body is not { } body || plugins.TryGet<ActorRecord>(body.Actor) is not { } actor
                || models.InstantiateActor(actor) is not var (node, animator) || animator is null)
            {
                GD.PushWarning($"--voices: {entity.Id} ({entity.Name}) has no animated actor.");
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
                Text = Caption(plugins.TryGet<DbRecord>(entity.Id), entity.Name),
            };
            view.AddChild(node);
            view.AddChild(label);
            var member = new Member(entity, i, node, animator, label, above);
            view._members.Add(member);
            animator.Enter(0);
        }
        return view;
    }

    /// <summary>A speaker's label: its name, then its voice, pitch and tone.</summary>
    private static string Caption(DbRecord? speaker, string name)
    {
        var voice = speaker?.Voice is { } v ? v.ToString().ToLowerInvariant() : "no voice";
        return $"{name}\n{voice}, pitch {TextReveal.PitchOf(speaker):0.00}, {TextReveal.ToneOf(speaker).Name}";
    }

    /// <summary>
    /// Labels the talking wanderers within <see cref="WandererLabels"/> of the ring, as the members are labelled, following their bodies,
    /// and drops the labels of those gone: called each tick with trek's wanderers.
    /// </summary>
    public void LabelWanderers(IReadOnlyList<Wanderer> wanderers)
    {
        foreach (var w in wanderers)
        {
            if (!w.Talks)
                continue;
            var at = w.Body.Sphere.Position.ToGodot();
            if (new Vector2(at.X - _centre.X, at.Z - _centre.Z).Length() > WandererLabels)
                continue;
            if (!_wandererLabels.TryGetValue(w, out var label))
            {
                var speaker = _plugins.TryGet<DbRecord>(w.Party.Entity);
                label = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    PixelSize = 0.0022f,
                    FontSize = 36,
                    OutlineSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = Caption(speaker, speaker?.Name ?? w.Party.Entity.ToString()),
                };
                _wandererLabels[w] = label;
                AddChild(label);
            }
            label.Position = at + new Vector3(0, w.Look.Height * w.Look.Scale - w.Body.Sphere.Radius + LabelAbove, 0);
        }
        foreach (var (w, label) in _wandererLabels.Where(p => !wanderers.Contains(p.Key)).ToList())
        {
            label.QueueFree();
            _wandererLabels.Remove(w);
        }
    }

    /// <summary>The ring member whose feet are within <see cref="Reach"/> of <paramref name="feet"/>, the nearest of them, or none.</summary>
    public EntityRecord? Nearest(Vector3 feet)
    {
        Member? nearest = null;
        var best = Reach;
        foreach (var m in _members)
        {
            var distance = new Vector2(m.Node.Position.X - feet.X, m.Node.Position.Z - feet.Z).Length();
            if (distance < best)
                (nearest, best) = (m, distance);
        }
        return nearest?.Entity;
    }

    /// <summary>The two lines a ring member says, as a sign's script with the member as the speaker.</summary>
    public DialogueRecord ScriptFor(EntityRecord entity)
    {
        var index = _members.FindIndex(m => m.Entity.Id == entity.Id);
        var speak = new DialogueOp(DialogueCommand.SetSpeaker, Target: entity.Id);
        var end = new DialogueOp(DialogueCommand.EndDialogue);
        return new DialogueRecord(RecordId.Parse($"debug:dialogue/0x{index + 1:X4}"), index + 1, DialogueMode.Sign,
        [
            new DialogueNode(0, DialogueNodeKind.Line, Lines[0].Replace("{name}", entity.Name), [speak], [1]),
            new DialogueNode(1, DialogueNodeKind.Line, Lines[1].Replace("{name}", entity.Name), [end]),
        ]);
    }

    public override void _Process(double delta)
    {
        foreach (var m in _members)
            m.Ground(_world);
        _fraction += delta * 60;
        for (; _fraction >= 1; _fraction--)
            foreach (var m in _members)
                m.Tick();
    }

    /// <summary>One actor in the ring, idling.</summary>
    private sealed class Member(EntityRecord entity, int index, Node3D node, ActorAnimator animator, Label3D label, float labelAbove)
    {
        private bool _grounded;

        public EntityRecord Entity => entity;

        public int Index => index;

        public Node3D Node => node;

        /// <summary>A tick of its idle, started again at its end.</summary>
        public void Tick()
        {
            if (animator.Machine.Clock >= animator.Machine.Length(0))
                animator.Enter(0);
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
    }
}
