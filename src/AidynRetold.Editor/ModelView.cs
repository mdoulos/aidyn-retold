using System.Globalization;
using AidynRetold.Core.Rng;
using AidynRetold.Plugins.Assets;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A 3D preview: loads a glTF at run time and orbits it. Drag to turn, right- or middle-drag to
/// pan, and scroll to zoom. Its animations loop, and can be scrubbed and stepped by the tick
/// (space plays and pauses, comma and full stop step). An actor's preview adds its states, and can
/// run its state machine with the runtime's <see cref="ActorAnimator"/>.
/// </summary>
public partial class ModelView : VBoxContainer
{
    /// <summary>What the preview shows, when that isn't just the asset itself: laid over the view's top corner.</summary>
    private readonly Label _note = new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore,
        Position = new Vector2(12, 8), CustomMinimumSize = new Vector2(420, 0),
    };
    private readonly OptionButton _animations = new() { TooltipText = "Animation" };
    private readonly Button _play = new() { Text = "Pause", CustomMinimumSize = new Vector2(64, 0), TooltipText = "Play or pause (space)" };
    /// <summary>The clip's ticks under the view, with what happens when, which scrubs (comma and full stop step).</summary>
    private readonly Timeline _time = new() { TooltipText = "Scrub by the tick (comma and full stop step)" };
    private readonly Label _tick = new() { CustomMinimumSize = new Vector2(150, 0) };
    private readonly HBoxContainer _legend = EditorStyle.Row(10);
    private readonly HSplitContainer _body = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly ScrollContainer _inspectorScroll = new()
    {
        CustomMinimumSize = new Vector2(360, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false,
    };
    private readonly CheckBox _wireframe = new() { Text = "Wireframe" };
    private readonly CheckBox _sceneCamera = new() { Text = "Scene camera", TooltipText = "Look through the scene's own camera, at 4:3" };
    private readonly CheckBox _emitters = new() { Text = "Emitters", ButtonPressed = true, TooltipText = "Mark particle emitters on the scene's locators, in their particles' brightest colour" };
    private readonly Control _frame = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
    private readonly SubViewportContainer _container = new() { Stretch = true, MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.All };
    private readonly SubViewport _viewport = new() { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X };
    private readonly Camera3D _camera = new() { Fov = 45 };
    private readonly DirectionalLight3D _headlight = new() { LightEnergy = 0.8f };
    private readonly ActorPanel _actorPanel = new() { Visible = false, CustomMinimumSize = new Vector2(0, 220) };
    private readonly List<(MeshInstance3D Marker, Node3D Node)> _markers = [];
    private Node3D? _content;
    private Camera3D? _ownCamera;
    // The scene camera's own planes, from its Borg3, which a cinematic's shot replaces.
    private float _ownNear, _ownFar;
    private AnimationPlayer? _player;
    private MirroredMeshes? _mirroring;
    private bool _playing = true;
    private readonly Orbit _orbit = new();

    // An actor's: its record, the root transform and its pivot, and the animator while driving.
    private ActorRecord? _actor;
    private GlbScene? _glb;
    private Node3D? _root;
    private Vector3 _pivot, _motion;
    private ActorAnimator? _animator;
    private double _fraction;
    private AidynRetold.Plugins.PluginSet? _plugins;
    // Driving an actor runs its emitters, as the runtime does.
    private ParticlesView? _particles;
    private MeshInstance3D? _ground;

    public ModelView()
    {
        // Wireframes are drawn only for meshes made after this.
        RenderingServer.SetDebugGenerateWireframes(true);

        // The left column: over the view, driving an actor and what the view shows; the view, framed; under it, playing the clip,
        // then one row of the states' Table and Graph and the timeline, then the actor's states. The inspector beside it all, the
        // page's height.
        AddThemeConstantOverride("separation", 0);
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 8);
        var bar = EditorStyle.Row(6);
        _actorPanel.Bar.Visible = false;
        bar.AddChild(_actorPanel.Bar);
        bar.AddChild(EditorStyle.Spacer());
        bar.AddChild(_emitters);
        bar.AddChild(_sceneCamera);
        bar.AddChild(_wireframe);
        left.AddChild(bar);

        var split = new VSplitContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _container.GuiInput += OnViewInput;
        _container.AddChild(_viewport);
        _frame.AddChild(_container);
        _note.ThemeTypeVariation = "DimLabel";

        _frame.AddChild(_note);
        _frame.Resized += Layout;
        var framed = new PanelContainer { ThemeTypeVariation = "Frame", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        framed.AddChild(_frame);
        split.AddChild(framed);
        // Under the view: playing the clip, then one row of the states' Table and Graph, the timeline and its legend, then the actor's
        // states, which take the rest of the height the split gives.
        var under = new VBoxContainer();
        under.AddThemeConstantOverride("separation", 8);
        var playing = EditorStyle.Row(6);
        playing.AddChild(_play);
        playing.AddChild(_animations);
        playing.AddChild(_tick);
        under.AddChild(playing);
        var row = EditorStyle.Row(8);
        _actorPanel.ViewButtons.Visible = false;
        row.AddChild(_actorPanel.ViewButtons);
        _time.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_time);
        foreach (var (kind, what) in new[] { (TimelineMarkKind.Footstep, "footstep"), (TimelineMarkKind.Sound, "sound"), (TimelineMarkKind.Emitter, "emitter"),
                     (TimelineMarkKind.Strike, "blow lands") })
        {
            var key = EditorStyle.DimLabel($"▼ {what}", 12);
            key.AddThemeColorOverride("font_color", Timeline.ColorOf(kind));
            _legend.AddChild(key);
        }
        _legend.Visible = false;
        row.AddChild(_legend);
        under.AddChild(row);
        under.AddChild(_actorPanel);
        split.AddChild(under);
        left.AddChild(split);
        _body.AddChild(left);
        Inspector.AddThemeConstantOverride("separation", 16);
        Inspector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // The inspector takes the width its side of the split is given, however wide that's dragged.
        var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        inset.AddThemeConstantOverride("margin_right", 6);
        inset.AddChild(Inspector);
        _inspectorScroll.AddChild(inset);
        _body.AddChild(_inspectorScroll);
        AddChild(_body);

        _viewport.AddChild(new WorldEnvironment
        {
            Environment = EditorTheme.Viewport(new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.45f, 0.45f, 0.45f),
            }),
        });
        _headlight.RotationDegrees = new Vector3(-50, 35, 0);
        _viewport.AddChild(_headlight);
        _viewport.AddChild(_camera);

        _animations.ItemSelected += _ => PlaySelected();
        _play.Pressed += TogglePlay;
        _time.Scrubbed += OnScrub;
        _wireframe.Toggled += on => _viewport.DebugDraw = on ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled;
        _sceneCamera.Toggled += _ => UseCamera();
        _emitters.Toggled += on => _markers.ForEach(m => m.Marker.Visible = on);

        _actorPanel.StateClicked += OnStateClicked;
        _actorPanel.DriveChanged += SetDrive;
        _actorPanel.RequestChanged += request =>
        {
            if (_animator is not null)
                _animator.Request = request;
        };
        _actorPanel.InPlaceChanged += _ =>
        {
            // Posed afresh, so turning it off puts the root back.
            if (_player is not null && _animator is null)
                _player.Seek(_player.CurrentAnimationPosition, update: true);
            ShowFrame();
        };
        _actorPanel.HomePressed += Home;
    }

    /// <summary>Runs an actor's state machine for a request, as the Drive switch does.</summary>
    public void Drive(int request)
    {
        if (_actor is not null)
            _actorPanel.Drive(request);
    }

    /// <summary>The installed plugins, for what a scene's asset doesn't hold: its emitters, and the cinematics' planes.</summary>
    public AidynRetold.Plugins.PluginSet? Plugins
    {
        get => _plugins;
        set
        {
            _plugins = value;
            _particles?.QueueFree();
            _particles = null;
        }
    }

    /// <summary>The model shown, which the inspector's sections draw on and pick parts of; null when nothing loaded.</summary>
    public Node3D? Content => _content;

    /// <summary>The model's glTF as parsed, with its extras, animations and clips.</summary>
    public GlbScene? File => _glb;

    /// <summary>The actor shown, if the model is an actor's.</summary>
    public ActorRecord? Actor => _actor;

    /// <summary>The model's ID, when the view was told it, whose plugin textures it draws.</summary>
    public AidynRetold.Plugins.RecordId? Scene { get; private set; }

    /// <summary>The model's size, drawn at <paramref name="scale"/>, as a body's scale draws it in the game.</summary>
    public void ScaleModel(float scale)
    {
        if (_content is not null)
            _content.Scale = Vector3.One * scale;
    }

    /// <summary>The inspector beside the view, which the record browser fills with sections for what's shown.</summary>
    public VBoxContainer Inspector { get; } = new();

    /// <summary>Shows or hides the inspector beside the view.</summary>
    public bool InspectorVisible
    {
        get => _inspectorScroll.Visible;
        set => _inspectorScroll.Visible = value;
    }

    public void SetWireframe(bool on) => _wireframe.ButtonPressed = on;

    /// <summary>Shows an actor's states as a graph rather than a table (<c>--states graph</c>).</summary>
    public void ShowStatesAsGraph(bool graph) => _actorPanel.ShowAsGraph(graph);

    public void SetSceneCamera(bool on) => _sceneCamera.ButtonPressed = on;

    public void Clear()
    {
        _content?.QueueFree();
        _content = null;
        _ownCamera = null;
        _player = null;
        _actor = null;
        _animator = null;
        _particles?.Head.Clear();
        _root = null;
        _glb?.Dispose();
        _glb = null;
        _markers.Clear();
        _ground?.QueueFree();
        _ground = null;
        _animations.Clear();
        _animations.Visible = _play.Visible = _time.Visible = _tick.Visible = false;
        // Driving an actor locks the scrub (SetDrive), and the next actor's panel turns Drive off without saying so.
        _time.Editable = true;
        _time.Marks = [];
        _legend.Visible = false;
        _markedState = -1;
        Marks = null;
        _emitters.Visible = _sceneCamera.Visible = false;
        _actorPanel.Visible = _actorPanel.Bar.Visible = _actorPanel.ViewButtons.Visible = false;
        _note.Text = "";
    }

    /// <summary>Shows a glTF binary, playing <paramref name="animation"/> if it has one by that name.</summary>
    /// <param name="note">What the preview shows, when that isn't just the asset itself.</param>
    /// <param name="actor">The actor whose scene this is, to show its states and drive it.</param>
    /// <param name="model">The model's ID, whose textures the plugins may give in place of its own copies, as the game draws them (ADR 0044).</param>
    public void Show(byte[] glb, string? animation, string? note, ActorRecord? actor = null, AidynRetold.Plugins.RecordId? model = null)
    {
        Clear();
        _note.Text = note ?? "";

        GlbScene file;
        try
        {
            file = GlbScene.Parse(glb);
        }
        catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException)
        {
            _note.Text = $"Not a glTF binary ({e.Message}).";
            return;
        }
        Scene = model;
        var textures = _plugins is { } plugins && model is { } id ? TextureReplacements.For(plugins, id) : null;
        if (GlbFile.Load(file, out var error, textures) is not { } scene)
        {
            file.Dispose();
            _note.Text = $"Godot couldn't load the glTF ({error}).";
            return;
        }
        _content = scene;
        _glb = file;
        _viewport.AddChild(scene);

        // The scene's own lights replace the headlight; unlit materials ignore both.
        _headlight.Visible = Nodes.FindAll<Light3D>(scene).Count == 0;
        var radius = Frame(scene);
        _ownCamera = Nodes.FindAll<Camera3D>(scene).FirstOrDefault();
        if (_ownCamera is not null)
            (_ownNear, _ownFar) = (_ownCamera.Near, _ownCamera.Far);
        _sceneCamera.Visible = _ownCamera is not null;
        UseCamera();
        // Sized for the view, so they show on effects with little or no geometry.
        MarkEmitters(file.Extras, scene, _orbit.Distance / 90);

        _mirroring = MirroredMeshes.For(scene, file.Extras);
        _player = Nodes.FindAll<AnimationPlayer>(scene).FirstOrDefault();
        if (_player is not null)
        {
            // Advanced here, so ticks can be stepped and the actor's root held.
            _player.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
            var names = _player.GetAnimationList().Where(n => n != "RESET").Order(StringComparer.Ordinal).ToList();
            foreach (var name in names)
            {
                _player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
                _animations.AddItem($"{name} ({Ticks(name)} ticks)");
                _animations.SetItemMetadata(_animations.ItemCount - 1, name);
            }
            if (names.Count > 0)
            {
                var start = animation is { } a && names.IndexOf(a) is >= 0 and var i ? i : 0;
                _animations.Select(start);
                _animations.Visible = _play.Visible = _time.Visible = _tick.Visible = true;
                PlaySelected();
            }
        }

        // An actor whose animations all hold it still has no player, but its states still run and start its emitters.
        if (actor is not null && (_player is not null || file.Extras.StillAnimations.Count > 0))
        {
            _actor = actor;
            // One outfit at a time, the one no character wears, not all of them over each other.
            OutfitSwitch.Add(actor, scene);
            var rootName = SceneNames.Transform(actor.RootTransform);
            _root = Nodes.FindAll<Node3D>(scene).FirstOrDefault(n => n.Name == rootName);
            _pivot = file.Clips(actor.RootTransform).Pivot.ToGodot();
            _motion = Vector3.Zero;
            var clips = file.Clips(actor.RootTransform);
            _actorPanel.Show(actor, n => clips.Clip(actor.States[n].Animation)?.Length ?? 0);
            _actorPanel.Visible = _actorPanel.Bar.Visible = _actorPanel.ViewButtons.Visible = true;
            _ground = Ground(radius);
            _viewport.AddChild(_ground);
            ShowFrame();
        }
    }

    /// <summary>Plays the selected animation from its first tick.</summary>
    public void PlaySelected()
    {
        if (_player is null || Selected() is not { } name)
            return;
        _player.Play(name);
        _player.Seek(0, update: true);
        _time.Length = Ticks(name);
        _markedState = -1;
        RefreshMarks();
        UseShotPlanes(name);
        SetPlaying(true);
        ShowFrame();
    }

    private string? Selected() => _animations.Selected < 0 ? null : _animations.GetItemMetadata(_animations.Selected).AsString();

    /// <summary>An animation's length in ticks: one loop, as the runtime counts it.</summary>
    private int Ticks(string name) =>
        _player is not null && _player.HasAnimation(name) ? (int)Math.Round(_player.GetAnimation(name).Length * 60) : 0;

    public override void _Process(double delta)
    {
        if (_player is null && _animator is null || !IsVisibleInTree())
            return;
        foreach (var (marker, node) in _markers)
        {
            // Markers keep one size, whatever the scale their node inherits.
            var scale = node.GlobalBasis.Scale;
            marker.Scale = new Vector3(1 / Math.Max(scale.X, 1e-4f), 1 / Math.Max(scale.Y, 1e-4f), 1 / Math.Max(scale.Z, 1e-4f));
        }
        if (!_playing)
            return;
        if (_animator is not null)
        {
            // The state machine ticks at 60 Hz, and the root's motion moves the whole actor.
            _fraction += delta * 60;
            var before = _motion;
            for (; _fraction >= 1; _fraction--)
            {
                var moved = _animator.TickMotion(_actorPanel.Request);
                _motion += new Vector3(moved.X, 0, moved.Z);
                _content!.Position = _motion;
                _particles?.Step();
            }
            _content!.Position = _motion;
            // The camera follows the actor over the ground's grid.
            _orbit.Target += _motion - before;
            _orbit.Apply(_camera);
        }
        else
        {
            _player?.Advance(delta);
        }
        ShowFrame();
    }

    /// <summary>Holds the actor's root if asked, and shows where the animation is.</summary>
    private void ShowFrame()
    {
        var tick = 0;
        if (_player is not null)
        {
            TransformTracks.Apply(_player, _glb!.Driven);
            if (_root is not null && _animator is null && _actorPanel.InPlace)
                ActorAnimator.HoldRoot(_root, _pivot);
            _mirroring?.Update();
            tick = (int)Math.Round(_player.CurrentAnimationPosition * 60);
            if (_animator is null)
                _time.Tick = Math.Min(tick + 1, _time.Length);
        }
        if (_animator is { Machine: var machine })
        {
            _tick.Text = $"state {machine.State} · {machine.Clock} / {machine.Length(machine.State)}";
            _actorPanel.Mark(machine.State);
            _time.Length = machine.Length(machine.State);
            _time.Tick = machine.Clock;
            if (machine.State != _markedState)
                RefreshMarks();
        }
        else if (_player is not null)
        {
            _tick.Text = $"tick {tick + 1} / {_time.Length}";
            if (_actor is not null)
                _actorPanel.Mark(_actor.States.ToList().FindIndex(s => ActorAnimator.AnimationName(s) == Selected()));
        }
    }

    /// <summary>
    /// What happens when in a clip, for the timeline, given its animation's name and its length in ticks: the inspector's, which
    /// knows who the model is shown as (<see cref="TimelineMarks"/>). Null for none.
    /// </summary>
    public Func<string, int, IReadOnlyList<TimelineMark>>? Marks { get; set; }

    private int _markedState = -1;

    /// <summary>The timeline's marks afresh, for the driven state's clip or the one picked.</summary>
    public void RefreshMarks()
    {
        string? clip;
        if (_animator is { Machine: var machine } && _actor is not null && machine.State < _actor.States.Count)
        {
            clip = ActorAnimator.AnimationName(_actor.States[machine.State]);
            _markedState = machine.State;
        }
        else
        {
            clip = Selected();
        }
        _time.Marks = Marks is { } marks && clip is not null ? marks(clip, _time.Length) : [];
        _legend.Visible = _time.Marks.Count > 0;
    }

    private void TogglePlay() => SetPlaying(!_playing);

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        _play.Text = playing ? "Pause" : "Play";
    }

    /// <summary>The timeline's tick counts from 1, as the label and the state machine's clock do.</summary>
    private void OnScrub(int tick)
    {
        if (_player is null || _animator is not null)
            return;
        SetPlaying(false);
        _player.Seek(Math.Max(tick - 1, 0) / 60.0, update: true);
        ShowFrame();
    }

    private void Step(int by)
    {
        if (_player is null || _animator is not null)
            return;
        SetPlaying(false);
        var tick = Math.Clamp(Math.Round(_player.CurrentAnimationPosition * 60) + by, 0, _time.Length);
        _player.Seek(tick / 60, update: true);
        ShowFrame();
    }

    // ---- Actors ---------------------------------------------------------------------------------

    private void SetDrive(bool on)
    {
        if (_actor is null || _content is null || _glb is null)
            return;
        Home();
        _animator = on ? ActorAnimator.Create(_content, _actor, _glb, new Lcg(0)) : null;
        _fraction = 0;
        _particles?.Head.Clear();
        if (_animator is not null && _plugins is not null)
        {
            if (_particles is null)
            {
                _particles = new ParticlesView(_plugins) { TickFraction = () => _fraction };
                _viewport.AddChild(_particles);
            }
            _animator.Particles = _particles.For(_actor, _content);
        }
        _time.Editable = !on;
        if (_animator is not null)
        {
            _animator.Driven = true;
            SetPlaying(true);
        }
        else
        {
            PlaySelected();
        }
    }

    private void OnStateClicked(int state)
    {
        if (_actor is null)
            return;
        if (_animator is not null)
        {
            _animator.Enter(state);
            return;
        }
        var name = ActorAnimator.AnimationName(_actor.States[state]);
        for (var i = 0; i < _animations.ItemCount; i++)
        {
            if (_animations.GetItemMetadata(i).AsString() == name)
            {
                _animations.Select(i);
                PlaySelected();
                return;
            }
        }
    }

    private void Home()
    {
        _orbit.Target -= _motion;
        _orbit.Apply(_camera);
        _motion = Vector3.Zero;
        if (_content is not null)
            _content.Position = Vector3.Zero;
    }

    // ---- Cameras and markers --------------------------------------------------------------------

    /// <summary>The scene's camera, at 4:3, or the orbiting one filling the view.</summary>
    private void UseCamera()
    {
        if (_sceneCamera.ButtonPressed && _ownCamera is not null)
            _ownCamera.MakeCurrent();
        else
            _camera.MakeCurrent();
        Layout();
    }

    /// <summary>
    /// Gives the scene camera a cinematic shot's near and far planes while its animation plays, as the cinematic table
    /// sets them in place of the scene's Borg3's, and the Borg3's otherwise.
    /// </summary>
    private void UseShotPlanes(string animation)
    {
        if (_ownCamera is null)
            return;
        var shot = Plugins is null || !SceneNames.TryAnimation(animation, out var borg6) ? null
            : Plugins.All<CinematicRecord>(AidynRetold.Plugins.RecordTypes.Cinematic).SelectMany(c => c.Shots)
                .FirstOrDefault(s => Convert.ToInt32(s.Animation.Key, 16) == borg6);
        (_ownCamera.Near, _ownCamera.Far) = shot is null ? (_ownNear, _ownFar) : (shot.Near, shot.Far);
    }

    private void Layout()
    {
        var size = _frame.Size;
        var shown = size;
        if (_sceneCamera.ButtonPressed && _ownCamera is not null && size.Y > 0)
            shown = size.X / size.Y > 4f / 3 ? new Vector2(size.Y * 4 / 3, size.Y) : new Vector2(size.X, size.X * 3 / 4);
        _container.Position = ((size - shown) / 2).Floor();
        _container.Size = shown.Floor();
    }

    /// <summary>
    /// Puts a marker on each particle emitter on the scene's own locators (nodes the importer
    /// names <see cref="SceneNames.Emitter"/>), drawn through the model in its particles' brightest colour.
    /// </summary>
    private void MarkEmitters(SceneExtras extras, Node3D scene, float radius)
    {
        var emitters = extras.EmitterColors;
        if (emitters.Count == 0)
            return;
        foreach (var node in Nodes.FindAll<Node3D>(scene).ToList())
        {
            if (!SceneNames.TryEmitter(node.Name.ToString(), out var i) || i >= emitters.Count)
                continue;
            var c = emitters[i];
            var marker = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 2, RadialSegments = 12, Rings = 6 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(c.X, c.Y, c.Z, 0.8f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    NoDepthTest = true,
                    RenderPriority = 1,
                },
                Visible = _emitters.ButtonPressed,
            };
            node.AddChild(marker);
            _markers.Add((marker, node));
        }
        _emitters.Visible = true;
        _emitters.Text = $"Emitters ({emitters.Count}, {_markers.Count} here)";
    }

    /// <summary>A grid of lines a scene unit apart on the ground under an actor, to show it moving.</summary>
    private static MeshInstance3D Ground(float radius)
    {
        var mesh = new ImmediateMesh();
        var half = Math.Max(40, (int)(radius * 40));
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (var i = -half; i <= half; i++)
        {
            mesh.SurfaceAddVertex(new Vector3(i, 0, -half));
            mesh.SurfaceAddVertex(new Vector3(i, 0, half));
            mesh.SurfaceAddVertex(new Vector3(-half, 0, i));
            mesh.SurfaceAddVertex(new Vector3(half, 0, i));
        }
        mesh.SurfaceEnd();
        return new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.5f, 0.55f, 0.65f, 0.35f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
    }

    /// <summary>Points the camera at the meshes' bounds, from far enough to see them whole. Returns their radius.</summary>
    private float Frame(Node3D scene)
    {
        Aabb? bounds = null;
        foreach (var mesh in Nodes.FindAll<MeshInstance3D>(scene))
        {
            if (mesh.Mesh is null)
                continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } b ? b.Merge(box) : box;
        }
        var aabb = bounds ?? new Aabb(-Vector3.One, 2 * Vector3.One);
        var radius = Mathf.Max(aabb.Size.Length() / 2, 0.01f);
        _orbit.Frame(_camera, aabb.GetCenter(), radius);
        return radius;
    }

    private void OnViewInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle }:
                _container.GrabFocus();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Space }:
                TogglePlay();
                _container.AcceptEvent();
                return;
            case InputEventKey { Pressed: true, Keycode: Key.Comma }:
                Step(-1);
                _container.AcceptEvent();
                return;
            case InputEventKey { Pressed: true, Keycode: Key.Period }:
                Step(1);
                _container.AcceptEvent();
                return;
        }
        _orbit.Handle(e, _camera);
    }
}
