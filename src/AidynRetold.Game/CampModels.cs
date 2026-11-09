using AidynRetold.Core.Camp;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Game;

/// <summary>
/// The camp's figures that are drawn from a 3D model rather than a painted pose (<see cref="CampFigureRecord.Model"/>, the user's choice, Nov 9, 2026): for each slot such a
/// figure stands at, a world of its own holding its actor, playing its standing or sitting clip, turned for the pose its slot takes and seen by an orthographic camera
/// tilted down as the picture's ground is. <see cref="CampView"/> draws each one's picture into the same layers and at the same slot as a painted figure.
/// </summary>
public partial class CampModels : Node
{
    /// <summary>How much of the model's world each picture shows, wide and high, in the model's units.</summary>
    public const float ViewWidth = 2.3f, ViewHeight = 2.3f;

    /// <summary>The height the camera aims at, and how far it looks down: the picture's ground, seen at about 13°.</summary>
    private const float Aim = 0.95f, Pitch = 13 * Mathf.Pi / 180;

    /// <summary>How far down a picture the ground under the model's origin is, as a share of its height.</summary>
    public static float FootFrom => 0.5f + Aim * Mathf.Cos(Pitch) / ViewHeight;

    private sealed class Figure(SubViewport view, Node3D turn, Node3D model, AnimationPlayer player, Texture2D picture, double clock)
    {
        public SubViewport View { get; } = view;

        public Node3D Turn { get; } = turn;

        public Node3D Model { get; } = model;

        public AnimationPlayer Player { get; } = player;

        public Texture2D Picture { get; } = picture;

        public double Clock { get; set; } = clock;

        public string? Clip { get; set; }
    }

    private readonly PluginSet _plugins = null!;
    private readonly Models _models = null!;
    private readonly Dictionary<string, Figure> _figures = [];

    public CampModels()
    {
    }

    public CampModels(PluginSet plugins, Models models)
    {
        _plugins = plugins;
        _models = models;
        Name = "Camp models";
    }

    /// <summary>The picture of the figure at a slot, or null if none is drawn there yet.</summary>
    public Texture2D? Picture(string slot) => _figures.GetValueOrDefault(slot)?.Picture;

    /// <summary>
    /// Keeps a figure for each placement that has a model, gone with its placement, and moves each on to now.
    /// </summary>
    /// <param name="height">How tall the pictures should be, in pixels.</param>
    public void Step(double delta, IEnumerable<CampPlacement> placements, int height)
    {
        var wanted = placements.Where(p => p.Figure?.Model is not null).ToDictionary(p => p.Slot.Id);
        foreach (var gone in _figures.Keys.Except(wanted.Keys).ToList())
            Remove(gone);
        var size = new Vector2I(Mathf.RoundToInt(height * ViewWidth / ViewHeight), height);
        foreach (var (slot, placement) in wanted)
        {
            if (!_figures.TryGetValue(slot, out var figure))
            {
                if (Make(slot, placement) is not { } made)
                    continue;
                figure = _figures[slot] = made;
            }
            if (Math.Abs(figure.View.Size.Y - size.Y) > 16)
                figure.View.Size = size;
            figure.Turn.Rotation = new Vector3(0, Yaw(placement), 0);
            var clips = placement.Figure!.Model!.Clips(CampPoses.IsSeated(placement.Pose));
            var clip = clips.Count == 0 ? null : SceneNames.Animation(Convert.ToInt32(clips[placement.Clip % clips.Count].Key, 16));
            if (clip is not null && figure.Clip != clip && figure.Player.HasAnimation(clip))
            {
                figure.Clip = clip;
                // A clip leaves any bone it keys and the next doesn't as it was: the sit makes his legs longer and thicker, which standing has no keys to undo.
                foreach (var skeleton in Nodes.FindAll<Skeleton3D>(figure.Model))
                    skeleton.ResetBonePoses();
                figure.Player.Play(clip);
                figure.Player.Pause();
            }
            if (figure.Clip is { } playing)
            {
                figure.Clock += delta;
                figure.Player.Seek(figure.Clock % figure.Player.GetAnimation(playing).Length, update: true);
            }
        }
    }

    /// <summary>Lights the figure at a slot: its ambient and lights, each a colour and the way to it, x to the picture's right, y up, z toward the camera.</summary>
    public void Light(string slot, Color ambient, IReadOnlyList<(Color Color, Vector3 Toward)> lights)
    {
        if (_figures.TryGetValue(slot, out var figure))
            N64Material.SetOwnLights(figure.Model, ambient, lights);
    }

    /// <summary>Lets every figure go, as the camp closes.</summary>
    public void Clear()
    {
        foreach (var slot in _figures.Keys.ToList())
            Remove(slot);
    }

    private void Remove(string slot)
    {
        _figures[slot].View.QueueFree();
        _figures.Remove(slot);
    }

    private Figure? Make(string slot, CampPlacement placement)
    {
        if (_plugins.TryGet<ActorRecord>(placement.Figure!.Model!.Actor) is not { } actor
            || _models.InstantiateActor(actor, N64Material.Lighting.Own, particles: false) is not ({ } node, { } animator)
            || Nodes.FindAll<AnimationPlayer>(node).FirstOrDefault() is not { } player)
            return null;
        // The camp plays its clips by itself, so the actor's states don't.
        animator.Driven = true;
        var view = new SubViewport
        {
            OwnWorld3D = true,
            TransparentBg = true,
            Msaa3D = Viewport.Msaa.Msaa4X,
            HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            // It's posed as it's drawn, so it isn't to be drawn a tick behind.
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
        };
        var turn = new Node3D();
        turn.AddChild(node);
        view.AddChild(turn);
        // As the pause menu's scroll: the material writes the values that go on screen, over nothing.
        var environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.ClearColor, BackgroundColor = Colors.Transparent };
        OnScreenOutput.Apply(environment);
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = ViewHeight,
            KeepAspect = Camera3D.KeepAspectEnum.Height,
            Near = 0.1f,
            Far = 20,
            Environment = environment,
        };
        view.AddChild(camera);
        camera.Position = new Vector3(0, Aim + 5 * Mathf.Sin(Pitch), 5 * Mathf.Cos(Pitch));
        camera.Rotation = new Vector3(-Pitch, 0, 0);
        AddChild(view);
        var picture = new CanvasTexture { DiffuseTexture = view.GetTexture(), TextureFilter = CanvasItem.TextureFilterEnum.Linear };
        // Figures at different slots don't breathe together.
        return new Figure(view, turn, node, player, picture, slot.GetHashCode() % 7 * 0.61);
    }

    /// <summary>
    /// How the model is turned for its pose and the way its slot faces, 0 facing the camera and a quarter turn facing the picture's right: a profile straight along the fire,
    /// a rear three-quarter away from the camera toward it, a front three-quarter toward the camera and it, and a rear straight away.
    /// </summary>
    private static float Yaw(CampPlacement placement)
    {
        var degrees = placement.Pose switch
        {
            CampPoses.StandProfile or CampPoses.SitProfile => 90f,
            CampPoses.StandRear3Q or CampPoses.SitRear3Q => 135f,
            CampPoses.StandFront3Q or CampPoses.SitFront3Q => 45f,
            _ => 180f,
        };
        return Mathf.DegToRad(placement.Facing == CampFacing.Left ? -degrees : degrees);
    }
}
