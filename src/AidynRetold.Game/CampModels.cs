using AidynRetold.Core.Camp;
using AidynRetold.Core.Rng;
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
    public const float Aim = 0.95f, Pitch = 13 * Mathf.Pi / 180;

    /// <summary>
    /// Where a figure is in its world as it stands now, each frame, for the shadows the camp lays under it: its hips, its top and its eyes' height, how far it reaches to either
    /// side, and what of it touches the ground, each a place on the ground, how wide, and how firmly, 0 to 1, as it comes down to it.
    /// </summary>
    public sealed record ModelShape(Vector3 Hips, float Top, float Eyes, float Left, float Right, IReadOnlyList<(Vector3 At, float Width, float Strength)> Touches);

    /// <summary>Someone at the camp, for those who look at them: where on the ground, in figure heights across from the fire and toward the camera; whether standing; and how tall against Alaron.</summary>
    public readonly record struct Spot(Vector2 Ground, bool Standing, float Stature);

    /// <summary>The shape of the figure at a slot as it stands now, or null if none is drawn there.</summary>
    public ModelShape? Shape(string slot) => _figures.GetValueOrDefault(slot)?.Shape;

    /// <summary>How far down a picture the ground under the model's origin is, as a share of its height.</summary>
    public static float FootFrom => 0.5f + Aim * Mathf.Cos(Pitch) / ViewHeight;

    private sealed class Figure(SubViewport view, Node3D turn, Node3D model, AnimationPlayer player, Texture2D picture, double clock, Skeleton3D body, HeadRig? rig, CampGaze gaze)
    {
        public SubViewport View { get; } = view;

        public Node3D Turn { get; } = turn;

        public Node3D Model { get; } = model;

        public AnimationPlayer Player { get; } = player;

        public Texture2D Picture { get; } = picture;

        public double Clock { get; set; } = clock;

        public string? Clip { get; set; }

        /// <summary>The versions of its pose it blends between, or null for a pose that has none.</summary>
        public Variation? Varied { get; set; }

        /// <summary>Whether it's moving to, or staying in, its pose's shifted version.</summary>
        public bool ShiftedTo { get; set; }

        /// <summary>How far it has moved to the shifted version, 0 to 1.</summary>
        public double Shift { get; set; }

        /// <summary>The seconds until it moves again.</summary>
        public double ShiftIn { get; set; }

        /// <summary>The skeleton of its body.</summary>
        public Skeleton3D Body { get; } = body;

        /// <summary>Its head's rig, which turns the head, if it has a separate head.</summary>
        public HeadRig? Rig { get; } = rig;

        /// <summary>Where it's looking, while it sits.</summary>
        public CampGaze Gaze { get; } = gaze;

        /// <summary>How far its head has turned from its body, across and down, easing toward what it's looking at.</summary>
        public float HeadTurn { get; set; }

        public float HeadNod { get; set; }

        /// <summary>The pose and clip its placement last asked for, or null before it has taken any.</summary>
        public (string Pose, int Clip)? Placed { get; set; }

        /// <summary>The pose it's in or moving to, its place in its list, and whether it's seated.</summary>
        public CampModelPose? Current { get; set; }

        public int Index { get; set; }

        public bool Seated { get; set; }

        /// <summary>The seconds until it changes how it sits.</summary>
        public double ChangeIn { get; set; }

        /// <summary>Its bones as they were when it began moving to the pose it's moving to now, or null when it's not moving.</summary>
        public BonePose[]? From { get; set; }

        /// <summary>The poses still to come on its way, after the one it's moving to.</summary>
        public Queue<CampModelPose> Path { get; } = new();

        /// <summary>How many poses its way passes through in all, which it's moving to now, how far along the whole of it it has got, in poses, and how long each takes.</summary>
        public int Stops { get; set; }

        public int Stop { get; set; }

        public double Along { get; set; }

        public double StopSeconds { get; set; }

        /// <summary>Where it is as it stands now (<see cref="ModelShape"/>).</summary>
        public ModelShape? Shape { get; set; }
    }

    /// <summary>A bone as it stands at a moment.</summary>
    private readonly record struct BonePose(Vector3 Position, Quaternion Rotation, Vector3 Scale);

    /// <summary>How long a move to another pose takes, in seconds, and each step of one that passes through others.</summary>
    private const double MoveSeconds = 1.5, StepSeconds = 0.75;

    /// <summary>The height of a standing figure's eyes, for those who look up at it; and how much of the way up to them a seated one tilts its head, the eyes doing the rest, and at most.</summary>
    private const float StandingEyes = 1.68f, LookUpShare = 0.8f, MostLookUp = 30 * Mathf.Pi / 180;

    /// <summary>How far a neck turns the head from where the body faces, in radians: 65°, short of what a neck can do.</summary>
    private const float MostFromBody = 65 * Mathf.Pi / 180;

    /// <summary>How quickly the head eases to where it's looking: the seconds to cover about two thirds of the way.</summary>
    private const float HeadEase = 0.22f;

    /// <summary>A pose's clips to blend, and each bone track of the main one with where it is in the others, -1 for nowhere.</summary>
    private sealed record Variation(Skeleton3D Skeleton, Animation Main, Animation? Upright, Animation? Shifted, Animation? ShiftedUpright, BoneTrack[] Tracks, float ShiftMin, float ShiftMax);

    private readonly record struct BoneTrack(int Bone, Animation.TrackType Kind, int Main, int Upright, int Shifted, int ShiftedUpright);

    /// <summary>How long a figure takes to move its hands to their other place, in seconds.</summary>
    private const double ShiftSeconds = 1.4;

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
    /// <param name="spots">Everyone at the camp, by their slot's name (<see cref="Spot"/>).</param>
    public void Step(double delta, IEnumerable<CampPlacement> placements, int height, IReadOnlyDictionary<string, Spot> spots)
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
            var model = placement.Figure!.Model!;
            var seated = CampPoses.IsSeated(placement.Pose);
            var poses = model.Poses(seated);
            if (poses.Count > 0)
            {
                var index = placement.Clip % poses.Count;
                if (figure.Placed != (placement.Pose, placement.Clip))
                {
                    // Taken at once as it first appears; after that the arrangement's asking for another, which it moves to.
                    if (figure.Placed is null)
                        Take(figure, poses[index]);
                    else
                        Move(figure, poses[index], seated, model);
                    figure.Placed = (placement.Pose, placement.Clip);
                    figure.Index = index;
                    figure.Seated = seated;
                    figure.ChangeIn = Between(model.SitEveryMin, model.SitEveryMax);
                }
                else if (seated && poses.Count > 1 && figure.From is null)
                {
                    figure.ChangeIn -= delta;
                    if (figure.ChangeIn <= 0)
                    {
                        figure.Index = (figure.Index + 1 + Random.Shared.Next(poses.Count - 1)) % poses.Count;
                        Move(figure, poses[figure.Index], seated, model);
                        figure.ChangeIn = Between(model.SitEveryMin, model.SitEveryMax);
                    }
                }
            }
            if (figure.Clip is { } playing)
            {
                figure.Clock += delta;
                var time = figure.Clock % figure.Player.GetAnimation(playing).Length;
                figure.Player.Seek(time, update: true);
                if (figure.Varied is { } varied)
                    Blend(figure, varied, placement.Lean, delta, time);
                if (figure.From is not null)
                    Fade(figure, delta);
            }
            if (seated)
                Look(figure, placement, spots, (float)delta);
            else
                LookAhead(figure, placement, (float)delta);
            figure.Shape = Measure(figure);
        }
    }

    /// <summary>Puts a figure in a pose at once.</summary>
    private static void Take(Figure figure, CampModelPose pose)
    {
        var clip = ClipName(pose.Clip);
        if (!figure.Player.HasAnimation(clip))
            return;
        figure.Clip = clip;
        figure.Current = pose;
        // A clip leaves any bone it keys and the next doesn't as it was: the sit makes his legs longer and thicker, which standing has no keys to undo.
        foreach (var skeleton in Nodes.FindAll<Skeleton3D>(figure.Model))
            skeleton.ResetBonePoses();
        figure.Player.Play(clip);
        figure.Player.Pause();
        // Posed at once: a pose taken after this frame's has been laid would otherwise show the skeleton at its rest for a frame.
        figure.Player.Seek(figure.Clock % figure.Player.GetAnimation(clip).Length, update: true);
        figure.Varied = Vary(figure, pose);
        figure.ShiftedTo = false;
        figure.Shift = 0;
        figure.ShiftIn = figure.Varied is { } varied ? Wait(varied) : 0;
    }

    /// <summary>Sets a figure moving to a pose, from whatever it's doing, through the poses between (<see cref="CampMoves"/>).</summary>
    private static void Move(Figure figure, CampModelPose to, bool seated, CampFigureModel model)
    {
        var path = CampMoves.Path(model, figure.Current, figure.Seated, to, seated);
        figure.Path.Clear();
        foreach (var pose in path.Skip(1))
            figure.Path.Enqueue(pose);
        figure.Stops = path.Count;
        figure.Stop = 0;
        figure.Along = 0;
        figure.StopSeconds = path.Count == 1 ? MoveSeconds : StepSeconds;
        Glide(figure, path[0]);
    }

    /// <summary>Begins a figure's move to a pose, from its bones as they stand.</summary>
    private static void Glide(Figure figure, CampModelPose pose)
    {
        var body = figure.Body;
        figure.From = [.. Enumerable.Range(0, body.GetBoneCount()).Select(b => new BonePose(body.GetBonePosePosition(b), body.GetBonePoseRotation(b), body.GetBonePoseScale(b)))];
        Take(figure, pose);
    }

    /// <summary>
    /// Lays where a moving figure has got to over the pose it's moving to, going on to the next pose on its way as it reaches each. The whole way eases in at its start and out
    /// at its end and goes on steadily between, so it doesn't stop at each pose it passes through.
    /// </summary>
    private static void Fade(Figure figure, double delta)
    {
        figure.Along = Math.Min(figure.Along + delta / figure.StopSeconds, figure.Stops);
        var eased = Mathf.SmoothStep(0.0, 1.0, figure.Along / figure.Stops) * figure.Stops;
        // Each pose reached, the next begins from it: the bones stand in it now, its clip having posed them this frame.
        while (eased >= figure.Stop + 1 && figure.Path.TryDequeue(out var next))
        {
            figure.Stop++;
            Glide(figure, next);
        }
        var share = (float)Math.Clamp(eased - figure.Stop, 0, 1);
        var (body, from) = (figure.Body, figure.From!);
        for (var b = 0; b < from.Length; b++)
        {
            body.SetBonePosePosition(b, from[b].Position.Lerp(body.GetBonePosePosition(b), share));
            body.SetBonePoseRotation(b, from[b].Rotation.Normalized().Slerp(body.GetBonePoseRotation(b).Normalized(), share));
            body.SetBonePoseScale(b, from[b].Scale.Lerp(body.GetBonePoseScale(b), share));
        }
        if (figure.Along >= figure.Stops)
            figure.From = null;
    }

    /// <summary>
    /// Turns a seated figure's head to what it's looking at (<see cref="CampGaze"/>): the fire or another figure's place, a little off it or now and then a good way off, as far as a
    /// neck turns from where the body faces, easing there.
    /// </summary>
    private void Look(Figure figure, CampPlacement placement, IReadOnlyDictionary<string, Spot> spots, float delta)
    {
        if (figure.Rig is not { } rig || !spots.TryGetValue(placement.Slot.Id, out var here))
            return;
        var others = spots.Where(s => s.Key != placement.Slot.Id).OrderBy(s => s.Key, StringComparer.Ordinal).ToList();
        figure.Gaze.Step(delta, others.Count);
        var focus = figure.Gaze.Focus == 0 ? (KeyValuePair<string, Spot>?)null : others[figure.Gaze.Focus - 1];
        var way = (focus?.Value.Ground ?? Vector2.Zero) - here.Ground;
        var body = Yaw(placement);
        var goal = way.LengthSquared() < 1e-4f ? body : Mathf.Atan2(way.X, way.Y);
        var turn = Mathf.Clamp(Mathf.Wrap(goal + figure.Gaze.Yaw - body, -Mathf.Pi, Mathf.Pi), -MostFromBody, MostFromBody);
        // Up at someone standing, the user's (Nov 9, 2026): most of the way along the line from his eyes to theirs, so less the further off they are.
        var up = 0f;
        if (focus is { Value.Standing: true } standing && !figure.Gaze.Glancing)
        {
            var theirs = _figures.GetValueOrDefault(standing.Key)?.Shape?.Eyes ?? StandingEyes * standing.Value.Stature;
            var distance = Math.Max(way.Length() * CampFigureRecord.CanonicalHeight, 0.3f);
            up = Mathf.Clamp(Mathf.Atan2(theirs - rig.Position.Y, distance) * LookUpShare, 0, MostLookUp);
        }
        var ease = 1 - Mathf.Exp(-delta / HeadEase);
        figure.HeadTurn += (turn - figure.HeadTurn) * ease;
        figure.HeadNod += (figure.Gaze.Nod - up - figure.HeadNod) * ease;
        var angle = body + figure.HeadTurn;
        rig.Turn(new System.Numerics.Vector2(MathF.Sin(angle), MathF.Cos(angle)), figure.HeadNod);
    }

    /// <summary>A standing figure's head easing back to look where its body faces, as the animation has it.</summary>
    private static void LookAhead(Figure figure, CampPlacement placement, float delta)
    {
        if (figure.Rig is not { } rig)
            return;
        var keep = Mathf.Exp(-delta / HeadEase);
        figure.HeadTurn *= keep;
        figure.HeadNod *= keep;
        if (Math.Abs(figure.HeadTurn) + Math.Abs(figure.HeadNod) < 0.002f)
        {
            (figure.HeadTurn, figure.HeadNod) = (0, 0);
            rig.Rest();
            return;
        }
        var angle = Yaw(placement) + figure.HeadTurn;
        rig.Turn(new System.Numerics.Vector2(MathF.Sin(angle), MathF.Cos(angle)), figure.HeadNod);
    }

    /// <summary>
    /// Where a figure is as it stands now (<see cref="ModelShape"/>): its hips, top, eyes and reach from its bones; and what touches the ground, its soles at the heel, the
    /// ball and the toe, its seat, a shin or a knee laid on the ground and a hand on it, each as firmly as it's near the ground.
    /// </summary>
    private static ModelShape Measure(Figure figure)
    {
        var body = figure.Body;
        var world = body.GlobalTransform;
        Transform3D? Bone(string name) => body.FindBone(name) is >= 0 and var b ? world * body.GetBoneGlobalPose(b) : null;
        Vector3 At(string name) => Bone(name)?.Origin ?? Vector3.Zero;
        List<(Vector3 At, float Width, float Strength)> touches = [];
        void Touch(Vector3 at, float width, float low, float high)
        {
            var strength = Mathf.Clamp(1 - (at.Y - low) / (high - low), 0, 1);
            if (strength > 0.05f)
                touches.Add((at with { Y = 0 }, width, strength));
        }
        var hips = At("pelvis");
        var head = At("head");
        var points = new List<Vector3> { hips };
        foreach (var side in new[] { "_L", "_R" })
        {
            var knee = At("shin" + side);
            var ankle = At("foot" + side);
            points.AddRange([knee, ankle]);
            // The sole, from where it lies on the foot as the skeleton rests: under the heel and forward to the toe.
            if (Bone("foot" + side) is { } foot && body.FindBone("foot" + side) is var footBone)
            {
                var rest = body.GetBoneGlobalRest(footBone).Basis.Inverse();
                var across = side == "_L" ? 0.02f : -0.02f;
                foreach (var under in new[] { new Vector3(0, -0.1f, -0.06f), new Vector3(across / 2, -0.1f, 0.08f), new Vector3(across, -0.09f, 0.2f) })
                    Touch(world.Basis.Orthonormalized() * (body.GetBoneGlobalPose(footBone).Basis.Orthonormalized() * (rest.Orthonormalized() * under)) + ankle, 0.09f, 0.0f, 0.09f);
            }
            foreach (var t in new[] { 0f, 0.35f, 0.7f })
                Touch(knee.Lerp(ankle, t), 0.11f, 0.08f, 0.17f);
            var hand = At("hand" + side).Lerp(At("fingers" + side), 0.5f);
            points.Add(hand);
            Touch(hand, 0.08f, 0.04f, 0.13f);
        }
        Touch(hips + new Vector3(0, 0, -0.04f), 0.3f, 0.1f, 0.28f);
        return new ModelShape(hips, head.Y + 0.17f, head.Y + 0.06f, points.Min(p => p.X), points.Max(p => p.X), touches);
    }


    private static string ClipName(RecordId clip) => SceneNames.Animation(Convert.ToInt32(clip.Key, 16));

    private static double Between(double least, double most) => least + Random.Shared.NextDouble() * (most - least);

    private static double Wait(Variation varied) => Between(varied.ShiftMin, varied.ShiftMax);

    /// <summary>The clips a pose varies between, with its bones' tracks in each, or null if it has none to vary between.</summary>
    private static Variation? Vary(Figure figure, CampModelPose pose)
    {
        Animation? Of(RecordId? id) => id is { } clip && figure.Player.HasAnimation(ClipName(clip)) ? figure.Player.GetAnimation(ClipName(clip)) : null;
        var (main, upright, shifted, both) = (Of(pose.Clip), Of(pose.Upright), Of(pose.Shifted), Of(pose.ShiftedUpright));
        if (main is null || (upright is null && shifted is null))
            return null;
        var body = figure.Body;
        var tracks = new List<BoneTrack>();
        for (var i = 0; i < main.GetTrackCount(); i++)
        {
            var kind = main.TrackGetType(i);
            var path = main.TrackGetPath(i);
            if (kind is not (Animation.TrackType.Rotation3D or Animation.TrackType.Position3D or Animation.TrackType.Scale3D) || path.GetSubNameCount() != 1)
                continue;
            var name = path.GetSubName(0).ToString();
            if (body.FindBone(name) is >= 0 and var bone)
                tracks.Add(new BoneTrack(bone, kind, i, upright?.FindTrack(path, kind) ?? -1, shifted?.FindTrack(path, kind) ?? -1, both?.FindTrack(path, kind) ?? -1));
        }
        return new Variation(body, main, upright, shifted, both, [.. tracks], pose.ShiftMin, pose.ShiftMax);
    }

    /// <summary>
    /// Sets its bones to the blend of its pose's versions for now: how upright this placement sits, and how far it has moved toward the shifted version, which it does every so
    /// often and back. Over the main clip, which has already posed whatever isn't a bone.
    /// </summary>
    private static void Blend(Figure figure, Variation varied, float lean, double delta, double time)
    {
        if (varied.Shifted is not null)
        {
            figure.ShiftIn -= delta;
            if (figure.ShiftIn <= 0)
            {
                figure.ShiftedTo = !figure.ShiftedTo;
                figure.ShiftIn = Wait(varied);
            }
            figure.Shift = Mathf.MoveToward(figure.Shift, figure.ShiftedTo ? 1 : 0, delta / ShiftSeconds);
        }
        var shift = (float)Mathf.SmoothStep(0.0, 1.0, figure.Shift);
        Quaternion Turn(Animation? clip, int track, Quaternion instead) => clip is null || track < 0 ? instead : clip.RotationTrackInterpolate(track, time).Normalized();
        Vector3 Place(Animation? clip, int track, Vector3 instead) => clip is null || track < 0 ? instead : clip.PositionTrackInterpolate(track, time);
        Vector3 Size(Animation? clip, int track, Vector3 instead) => clip is null || track < 0 ? instead : clip.ScaleTrackInterpolate(track, time);
        foreach (var t in varied.Tracks)
        {
            if (t.Kind == Animation.TrackType.Rotation3D)
            {
                var main = Turn(varied.Main, t.Main, Quaternion.Identity);
                var shifted = Turn(varied.Shifted, t.Shifted, main);
                varied.Skeleton.SetBonePoseRotation(t.Bone, main.Slerp(Turn(varied.Upright, t.Upright, main), lean).Slerp(shifted.Slerp(Turn(varied.ShiftedUpright, t.ShiftedUpright, shifted), lean), shift));
            }
            else if (t.Kind == Animation.TrackType.Position3D)
            {
                var main = Place(varied.Main, t.Main, Vector3.Zero);
                var shifted = Place(varied.Shifted, t.Shifted, main);
                varied.Skeleton.SetBonePosePosition(t.Bone, main.Lerp(Place(varied.Upright, t.Upright, main), lean).Lerp(shifted.Lerp(Place(varied.ShiftedUpright, t.ShiftedUpright, shifted), lean), shift));
            }
            else
            {
                var main = Size(varied.Main, t.Main, Vector3.One);
                var shifted = Size(varied.Shifted, t.Shifted, main);
                varied.Skeleton.SetBonePoseScale(t.Bone, main.Lerp(Size(varied.Upright, t.Upright, main), lean).Lerp(shifted.Lerp(Size(varied.ShiftedUpright, t.ShiftedUpright, shifted), lean), shift));
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
            || Nodes.FindAll<AnimationPlayer>(node).FirstOrDefault() is not { } player
            || Nodes.FindAll<Skeleton3D>(node).MaxBy(s => s.GetBoneCount()) is not { } body)
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
        return new Figure(view, turn, node, player, picture, slot.GetHashCode() % 7 * 0.61, body, HeadRig.Of(node), new CampGaze(new Lcg((uint)Random.Shared.Next())));
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
