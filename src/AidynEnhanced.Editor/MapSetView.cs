using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AidynEnhanced.Core;
using AidynEnhanced.Core.Flags;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Core.MapObjects;
using AidynEnhanced.Core.Physics;
using AidynEnhanced.Core.Rng;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;
using AidynEnhanced.Plugins.World;
using AidynEnhanced.Views;
using Godot;

namespace AidynEnhanced.Editor;

/// <summary>
/// A map set, or the chunks of one, drawn whole with the runtime's code (ADR 0010): each chunk's
/// scenes and scene objects as <see cref="ChunkScenery"/> builds them, with their billboards
/// turned and textures scrolled, the collision coloured by surface or role, and a marker for
/// every other map object. Click a marker or a placed model to pick its object; hovering names the chunk, surface
/// and role under the cursor. Double-click the collision to orbit about that point. A picked object can be edited
/// (docs/editing-the-world.md): dragged along the ground, or up and down with Shift, set by its numbers, dropped to the
/// ground, duplicated, deleted or given new fields, and the edits (<see cref="WorldEdits"/>) saved into a mod.
/// </summary>
public partial class MapSetView : VBoxContainer
{
    /// <summary>What to show: chunks of a set, framing a chunk, an object, or the chunks.</summary>
    public sealed record Focus(MapSetRecord Set, IReadOnlyList<ChunkEntry> Chunks, ChunkEntry? Chunk = null, RecordId? Object = null);

    /// <summary>A map object with a marker, where it is in the map set.</summary>
    private sealed record Marked(RecordId Id, MapObject Object, ChunkEntry Chunk, Vector3 Position);

    private const float MarkerPixels = 10;

    private static readonly Shader MarkerShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, skip_vertex_transform;
            uniform vec4 colour : source_color;
            uniform float size = 10.0;
            uniform float pull = 0.995;
            void vertex() {
                // Pulled toward the camera, so a marker on the ground isn't half buried.
                VERTEX = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz * pull;
                POINT_SIZE = size;
            }
            void fragment() {
                float r = length(POINT_COORD * 2.0 - 1.0);
                if (r > 1.0) discard;
                ALBEDO = r > 0.72 ? vec3(0.0) : colour.rgb;
            }
            """,
    };

    private static readonly StandardMaterial3D LineMaterial = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = 2,
    };

    // The switches persist from one map to the next.
    private static readonly HashSet<Type> HiddenKinds = [typeof(SceneObject), typeof(ReferencePointObject)];

    private readonly Label _status = new() { ClipText = true, CustomMinimumSize = new Vector2(0, 20) };
    private readonly CheckBox _terrain = new() { Text = "Terrain", ButtonPressed = true, TooltipText = "Each chunk's scenes" };
    private readonly CheckBox _scenery = new() { Text = "Scenery", ButtonPressed = true, TooltipText = "The models scene objects place, each at its nearest level of detail" };
    private readonly OptionButton _collision = new() { TooltipText = "Collision" };
    private readonly CheckBox _markers = new() { Text = "Objects", ButtonPressed = true, TooltipText = "A marker for each map object, by type. Click one to pick it." };
    private readonly CheckBox _pak = new() { Text = "Expansion Pak", ButtonPressed = true, TooltipText = "Objects and triangles for playing with the Expansion Pak, as the runtime does, or without it" };
    private readonly CheckBox _gate = new()
    {
        Text = "Flags",
        TooltipText = "Leave out what isn't there with the editor's flags, as a chunk loads in the game: a new game's, or a checkpoint's or save's loaded on a flag page",
    };
    private readonly CheckBox _grid = new() { Text = "Chunk grid" };
    private readonly Button _all = new() { Text = "Whole map set", TooltipText = "Load every chunk of the map set, not just those around this one" };
    private readonly Button _save = new() { Visible = false, TooltipText = "Write the objects moved, changed, added or removed into a mod, as patches to their maps" };
    private readonly Button _discard = new() { Text = "Discard edits", Visible = false };
    private static string _modId = "my_mod";
    private readonly HFlowContainer _legend = new();
    private readonly HSplitContainer _split = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly SubViewportContainer _container = new()
    {
        Stretch = true, MouseFilter = MouseFilterEnum.Stop, FocusMode = FocusModeEnum.All,
        SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
    };
    private readonly SubViewport _viewport = new() { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X };
    private readonly Camera3D _camera = new() { Fov = 50 };
    private readonly Orbit _orbit = new();
    private readonly Node3D _world = new() { Name = "Chunks" };
    private readonly Node3D _markerNode = new() { Name = "Markers" };
    private readonly Node3D _gridNode = new() { Name = "Grid", Visible = false };
    private readonly Node3D _selection = new() { Name = "Selection" };
    private readonly CollisionLayer _collisionLayer = new();
    private readonly VBoxContainer _panel = new() { Visible = false, CustomMinimumSize = new Vector2(380, 0) };
    private readonly Label _title = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _open = new()
    {
        Text = "Open it in the loot window",
        TooltipText = "Roll what it holds, as opening it does, and show the loot window against the editor's game, without setting its flags.",
    };
    private readonly Label _opened = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _lookThrough = new()
    {
        Text = "Look through it",
        TooltipText = "Put the view where this camera object puts the camera, aimed at where the player stands to trigger it: " +
            "the middle of its reach, or of the trigger triangles that name it, at the player's height.",
    };
    private readonly Lcg _random = new((uint)System.Random.Shared.Next());
    private Window? _lootWindow;
    private readonly RecordFieldsView _fields = new() { SizeFlagsVertical = SizeFlags.ExpandFill };

    // Editing the picked object.
    private readonly VBoxContainer _edit = new();
    private readonly SpinBox[] _position = Spins(0.01), _rotation = Spins(1), _scale = Spins(0.01);
    private readonly HBoxContainer _rotationRow = new(), _scaleRow = new();
    private readonly Button _drop = new() { Text = "Drop to ground", TooltipText = "Put it on the collision under it" };
    private readonly Button _duplicate = new() { Text = "Duplicate", TooltipText = "A copy beside it, at the next free index (Ctrl+D)" };
    private readonly Button _delete = new() { Text = "Delete", TooltipText = "Take it out of the map (Delete)" };
    private readonly Button _editFields = new() { Text = "Edit fields…", ToggleMode = true, TooltipText = "Its fields as JSON, to change and apply" };
    private readonly TextEdit _json = new() { Visible = false, CustomMinimumSize = new Vector2(0, 220), WrapMode = TextEdit.LineWrappingMode.Boundary };
    private readonly Button _apply = new() { Text = "Apply fields", Visible = false };
    private readonly Label _editStatus = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private bool _filling;
    private bool _dragging;
    private Vector2 _dragFrom;
    private Vector3 _dragStart;
    private float _dragAbove;

    private readonly Dictionary<(int Row, int Column), ChunkScenery> _chunks = [];
    private readonly List<Marked> _objects = [];
    private readonly List<Marked> _shown = [];
    private readonly Dictionary<RecordId, ChunkEntry> _chunkOfMap = [];

    /// <summary>The objects as edited here and not yet saved. They outlive a reload of the plugins.</summary>
    public WorldEdits Edits { get; } = new();

    /// <summary>The edits were saved into a mod, so the plugins should be loaded again to show them.</summary>
    public event Action? Saved;

    private PluginCatalog? _catalog;
    private Models? _models;
    private EditorGame? _game;
    private MapSetRecord? _set;
    private Marked? _selected;
    private int _generation;
    private readonly FrameGate _textureFrame = new(Cadence.TextureFrameTicks);
    private double _ticks;
    private Vector2? _pointer;
    private Vector2 _pressedAt;
    private string _summary = "", _hover = "";
    private Vector3 _facedFrom;
    private int _facedChunks;

    public MapSetView()
    {
        var bar = new HFlowContainer();
        bar.AddChild(_terrain);
        bar.AddChild(_scenery);
        bar.AddChild(new Label { Text = "Collision" });
        _collision.AddItem("off", (int)CollisionLayer.Colouring.Off);
        _collision.AddItem("by surface", (int)CollisionLayer.Colouring.Surface);
        _collision.AddItem("by role", (int)CollisionLayer.Colouring.Role);
        bar.AddChild(_collision);
        bar.AddChild(_markers);
        bar.AddChild(_pak);
        bar.AddChild(_gate);
        bar.AddChild(_grid);
        bar.AddChild(_all);
        bar.AddChild(_save);
        bar.AddChild(_discard);
        AddChild(bar);

        _container.AddChild(_viewport);
        _container.GuiInput += OnViewInput;
        _container.MouseExited += () =>
        {
            _pointer = null;
            SetStatus(hover: "");
        };
        _split.AddChild(_container);
        _panel.AddChild(_title);
        _open.Pressed += Roll;
        _panel.AddChild(_open);
        _lookThrough.Pressed += LookThrough;
        _panel.AddChild(_lookThrough);
        _panel.AddChild(_opened);
        _panel.AddChild(_editStatus);
        _edit.AddChild(Row("Position", _position));
        _rotationRow = Row("Rotation °", _rotation);
        _edit.AddChild(_rotationRow);
        _scaleRow = Row("Scale", _scale);
        _edit.AddChild(_scaleRow);
        var buttons = new HFlowContainer();
        buttons.AddChild(_drop);
        buttons.AddChild(_duplicate);
        buttons.AddChild(_delete);
        buttons.AddChild(_editFields);
        _edit.AddChild(buttons);
        _edit.AddChild(_json);
        _edit.AddChild(_apply);
        _panel.AddChild(_edit);
        _panel.AddChild(_fields);
        _split.AddChild(_panel);
        foreach (var spin in _position.Concat(_rotation).Concat(_scale))
            spin.ValueChanged += _ => FromSpins();
        _drop.Pressed += DropToGround;
        _duplicate.Pressed += Duplicate;
        _delete.Pressed += Delete;
        _editFields.Toggled += on =>
        {
            _json.Visible = _apply.Visible = on;
            if (on && _selected is { } selected)
                _json.Text = MapPatch.ToJson(selected.Object).ToJsonString(PluginJson.Options);
        };
        _apply.Pressed += ApplyFields;
        _save.Pressed += AskToSave;
        _discard.Pressed += () =>
        {
            Edits.Clear();
            if (_set is { } set && _catalog is { } catalog && _models is { } models)
            {
                var chunks = _chunks.Keys.Select(k => set.Chunk(k.Row, k.Column)!).ToList();
                Clear();
                _ = BackgroundTask.Report(ShowAsync(catalog, models, _game, new Focus(set, chunks)));
            }
            UpdateSaveButton();
        };
        AddChild(_split);
        AddChild(_legend);
        AddChild(_status);

        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.125f, 0.133f, 0.165f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.45f, 0.45f, 0.45f),
            },
        });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.8f, RotationDegrees = new Vector3(-50, 35, 0) });
        _viewport.AddChild(_camera);
        _viewport.AddChild(_world);
        _viewport.AddChild(_collisionLayer.Node);
        _viewport.AddChild(_markerNode);
        _viewport.AddChild(_gridNode);
        _viewport.AddChild(_selection);
        _collisionLayer.Node.Visible = false;

        _terrain.Toggled += _ => ApplyFilters();
        _scenery.Toggled += _ => ApplyFilters();
        _markers.Toggled += _ => ApplyFilters();
        _gate.Toggled += _ => ApplyFilters();
        _pak.Toggled += _ =>
        {
            ApplyFilters();
            Recolour();
        };
        _collision.ItemSelected += _ => Recolour();
        _grid.Toggled += on => _gridNode.Visible = on;
        _all.Pressed += () =>
        {
            if (_set is { } set && _catalog is { } catalog && _models is { } models)
                _ = BackgroundTask.Report(ShowAsync(catalog, models, _game, new Focus(set, set.Chunks)));
        };
        _fields.Opened += id => Opened?.Invoke(id);
        _orbit.PointUnder = point => Cast(point)?.Point;
        _orbit.Near = 0.05f;
    }

    /// <summary>A link was followed, or an object picked: its voxel ID.</summary>
    public event Action<RecordId>? Opened;

    private static SpinBox[] Spins(double step) =>
        [.. Enumerable.Range(0, 3).Select(_ => new SpinBox { Step = step, MinValue = -1e6, MaxValue = 1e6, CustomArrowStep = step * 10, SizeFlagsHorizontal = SizeFlags.ExpandFill })];

    private static HBoxContainer Row(string label, SpinBox[] spins)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        foreach (var spin in spins)
            row.AddChild(spin);
        return row;
    }

    /// <summary>Colours the collision, for scripts (<c>--collision surface|role</c>).</summary>
    public void SetCollision(string mode)
    {
        _collision.Select(mode switch { "surface" => 1, "role" => 2, _ => 0 });
        Recolour();
    }

    /// <summary>Forgets what's loaded, as when the plugins are reloaded.</summary>
    public void Clear()
    {
        _generation++;
        foreach (var chunk in _chunks.Values)
            chunk.Root.QueueFree();
        _chunks.Clear();
        _collisionLayer.Clear();
        _objects.Clear();
        _chunkOfMap.Clear();
        SetStatus("", "");
        _set = null;
        _catalog = null;
        _models = null;
        Select(null);
        RebuildMarkers();
        foreach (var child in _gridNode.GetChildren())
            child.QueueFree();
    }

    /// <summary>
    /// Shows chunks of a map set, loading those not loaded yet, nearest the focus first, a few a
    /// frame. Chunks already loaded from the same set stay. Completes once they're all loaded.
    /// </summary>
    public async Task ShowAsync(PluginCatalog catalog, Models models, EditorGame? game, Focus focus)
    {
        if (_catalog != catalog || _models != models || _set?.Id != focus.Set.Id)
            Clear();
        var generation = ++_generation;
        (_catalog, _models, _game, _set) = (catalog, models, game, focus.Set);
        _gate.Disabled = game is null;
        foreach (var chunk in focus.Set.Chunks)
            _chunkOfMap.TryAdd(chunk.Map, chunk);

        var centre = focus.Object is { } voxel && _chunkOfMap.GetValueOrDefault(MapRecord.MapOf(voxel)) is { } objectChunk
            ? objectChunk
            : focus.Chunk;
        var missing = focus.Chunks.Where(c => !_chunks.ContainsKey((c.Row, c.Column)))
            .OrderBy(c => centre is null ? 0 : Math.Max(Math.Abs(c.Row - centre.Row), Math.Abs(c.Column - centre.Column)))
            .ToList();
        var plugins = catalog.Plugins;
        var reading = Task.Run(() => missing.ToDictionary(c => c, c => plugins.FindAsset(c.Map, "glb") is { } path
            ? CollisionMesh.Read(File.ReadAllBytes(path)) : (IReadOnlyList<CollisionTriangle>)[]));

        var collision = await reading;
        if (generation != _generation)
            return;
        // The collision first, which is quick, so the view can be framed on it before the scenery loads.
        foreach (var chunk in missing.Where(c => !_collisionLayer.Contains(c)))
        {
            var origin = focus.Set.Origin(chunk.Row, chunk.Column).ToGodot();
            _collisionLayer.Add(chunk, collision[chunk], origin, Colouring, _pak.ButtonPressed);
            AddGridCell(chunk, origin);
        }
        // A picked object is framed once it's selected, if it's out of view.
        if (focus.Object is null && focus.Chunk is not null)
            FrameCells([focus.Chunk]);
        else if (focus.Object is not null ? missing.Count > 0 && centre is not null : missing.Count == focus.Chunks.Count)
            FrameCells(focus.Object is not null ? [centre!] : focus.Chunks);
        _all.Visible = _chunks.Count + missing.Count < focus.Set.Chunks.Count;

        var clock = Stopwatch.StartNew();
        for (var i = 0; i < missing.Count; i++)
        {
            var chunk = missing[i];
            var origin = focus.Set.Origin(chunk.Row, chunk.Column).ToGodot();
            var scenery = _chunks[(chunk.Row, chunk.Column)] = ChunkScenery.Load(plugins, models, focus.Set, chunk, _ => true);
            _world.AddChild(scenery.Root);
            foreach (var placed in scenery.Objects)
                placed.Levels.FirstOrDefault(l => l is not null)?.Show(0xFF);
            var map = plugins.Get<MapRecord>(chunk.Map);
            ApplyEdits(scenery, map);
            foreach (var o in Edits.Objects(map))
                _objects.Add(new Marked(MapRecord.VoxelId(chunk.Map, o.Index), o, chunk, o.Position.ToGodot() + origin));
            if (clock.ElapsedMilliseconds > 30 && i < missing.Count - 1)
            {
                SetStatus(summary: $"Loading {focus.Set.Label}: {i + 1:N0} of {missing.Count:N0} chunks…");
                ApplySceneFilters();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (generation != _generation)
                    return;
                clock.Restart();
            }
        }

        // Each chunk's collision was coloured as it came in.
        _collisionLayer.Node.Visible = Colouring != CollisionLayer.Colouring.Off;
        ApplyFilters();
        _all.Visible = _chunks.Count < focus.Set.Chunks.Count;
        SetStatus(summary: Summary());
        UpdateSaveButton();
        // An object opened from elsewhere is framed; one picked here only if it's out of view.
        Select(focus.Object is { } id ? _objects.FirstOrDefault(m => m.Id == id) : null, missing.Count > 0 ? Framing.Always : Framing.OutOfView);
    }

    /// <summary>Puts a chunk's scenery as the edits have it: changed scene objects moved, added ones placed, removed ones gone.</summary>
    private void ApplyEdits(ChunkScenery scenery, MapRecord map)
    {
        if (!Edits.Dirty || _catalog is not { } catalog || _models is not { } models)
            return;
        var edited = Edits.Objects(map);
        foreach (var o in edited.OfType<SceneObject>())
        {
            if (!Edits.IsEdited(map.Id, o.Index))
                continue;
            if (scenery.Find(o.Index) is { } placed)
                placed.Node.Transform = ChunkScenery.TransformOf(o);
            else
                PlaceNew(scenery, o);
        }
        var present = edited.Select(o => o.Index).ToHashSet();
        foreach (var o in map.Objects.OfType<SceneObject>().Where(o => !present.Contains(o.Index)))
            scenery.Remove(o.Index);
    }

    private void PlaceNew(ChunkScenery scenery, SceneObject o)
    {
        var placed = scenery.Place(_catalog!.Plugins, _models!, o);
        scenery.Root.GetNodeOrNull<Node3D>("Scenery")?.AddChild(placed.Node);
        placed.Levels.FirstOrDefault(l => l is not null)?.Show(0xFF);
        placed.Node.Visible = Shown(o);
    }

    private CollisionLayer.Colouring Colouring => (CollisionLayer.Colouring)_collision.GetSelectedId();

    private string Summary()
    {
        var shown = _objects.Count(m => Shown(m.Object));
        return $"{_set!.Label}: {_chunks.Count:N0} of {_set.Chunks.Count:N0} chunks, cells {_set.CellSize:0.##} apart, " +
            $"{_collisionLayer.Triangles:N0} triangles, {shown:N0} objects";
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _chunks.Count == 0)
            return;
        // Billboards only turn when the camera moves, or chunks come in: there can be thousands.
        if (_camera.GlobalPosition != _facedFrom || _chunks.Count != _facedChunks)
        {
            (_facedFrom, _facedChunks) = (_camera.GlobalPosition, _chunks.Count);
            Billboards.Face(_chunks.Values.SelectMany(c => c.Billboards), _facedFrom);
        }
        // Every scene in view is drawn each frame at the console's average 20 fps, which scrolls its textures.
        _ticks += delta * 60;
        var whole = (int)_ticks;
        _ticks -= whole;
        if (_textureFrame.Tick(whole) > 0)
        {
            if (_terrain.ButtonPressed || _scenery.ButtonPressed)
            {
                foreach (var scene in _chunks.Values.SelectMany(c => c.Scrolling()))
                    scene.Draw();
            }
        }
        Fly(delta);
        if (_pointer is { } pointer)
        {
            _pointer = null;
            ShowHover(pointer);
        }
    }

    /// <summary>
    /// W, A, S and D fly the camera forward, left, back and right, and E and Q up and down, faster
    /// with Shift, at a pace that suits how far away what it's looking at is.
    /// </summary>
    private void Fly(double delta)
    {
        if (!_container.HasFocus())
            return;
        float Held(Key key) => Input.IsKeyPressed(key) ? 1 : 0;
        var move = FreeFly.Direction(_camera.GlobalBasis, Held(Key.D) - Held(Key.A), Held(Key.E) - Held(Key.Q), Held(Key.S) - Held(Key.W));
        if (move == Vector3.Zero)
            return;
        var speed = Mathf.Clamp(_orbit.Distance, 5, 2000) * (Input.IsKeyPressed(Key.Shift) ? 3 : 1);
        _orbit.Fly(_camera, move.Normalized() * speed * (float)delta);
    }

    // ---- Filters --------------------------------------------------------------------------------

    /// <summary>Whether an object is there with the switches as they are: the Expansion Pak's, and the flags'.</summary>
    private bool Shown(MapObject o)
    {
        if (o.Pak == (_pak.ButtonPressed ? PakVariant.Without : PakVariant.With))
            return false;
        return !_gate.ButtonPressed || _game is not { Network: var network }
            || MapObjectPresence.There(o, network.IsSet);
    }

    private void ApplyFilters()
    {
        ApplySceneFilters();
        RebuildMarkers();
        if (_set is not null && _chunks.Count > 0)
            SetStatus(summary: Summary());
        if (_selected is { } selected)
            Select(selected);
    }

    private void ApplySceneFilters()
    {
        foreach (var chunk in _chunks.Values)
        {
            foreach (var child in chunk.Root.GetChildren().OfType<Node3D>())
                child.Visible = child.Name == "Scenery" ? _scenery.ButtonPressed : _terrain.ButtonPressed;
            foreach (var placed in chunk.Objects)
                placed.Node.Visible = Shown(placed.Object);
        }
    }

    private void Recolour()
    {
        _collisionLayer.Recolour(Colouring, _pak.ButtonPressed);
        RebuildLegend();
    }

    // ---- Markers and the legend -----------------------------------------------------------------

    private void RebuildMarkers()
    {
        foreach (var child in _markerNode.GetChildren())
            child.QueueFree();
        _shown.Clear();
        _markerNode.Visible = _markers.ButtonPressed;
        foreach (var (type, kind) in MapObjects.Kinds)
        {
            if (HiddenKinds.Contains(type))
                continue;
            var marked = _objects.Where(m => m.Object.GetType() == type && Shown(m.Object)).ToList();
            if (marked.Count == 0)
                continue;
            _shown.AddRange(marked);
            _markerNode.AddChild(Points(marked.Select(m => m.Position).ToArray(), kind.Colour, MarkerPixels, 0.995f));
        }
        RebuildLegend();
    }

    private static MeshInstance3D Points(Vector3[] positions, Color colour, float size, float pull)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Points, arrays);
        var material = new ShaderMaterial { Shader = MarkerShader, RenderPriority = 1 };
        material.SetShaderParameter("colour", colour);
        material.SetShaderParameter("size", size);
        material.SetShaderParameter("pull", pull);
        return new MeshInstance3D { Mesh = mesh, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }

    private void RebuildLegend()
    {
        foreach (var child in _legend.GetChildren())
            child.QueueFree();
        var counts = _objects.Where(m => Shown(m.Object)).GroupBy(m => m.Object.GetType()).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (type, kind) in MapObjects.Kinds)
        {
            if (!counts.TryGetValue(type, out var count))
                continue;
            var box = new CheckBox { Text = $"{kind.Name} {count:N0}", ButtonPressed = !HiddenKinds.Contains(type), Icon = Swatch(kind.Colour) };
            box.Toggled += on =>
            {
                if (on)
                    HiddenKinds.Remove(type);
                else
                    HiddenKinds.Add(type);
                RebuildMarkers();
            };
            _legend.AddChild(box);
        }
        foreach (var (name, colour) in _collisionLayer.Legend(Colouring))
            _legend.AddChild(new Button { Text = name, Icon = Swatch(colour), Flat = true, FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Ignore });
    }

    private static ImageTexture Swatch(Color colour)
    {
        var image = Image.CreateEmpty(12, 12, false, Image.Format.Rgba8);
        image.Fill(colour);
        return ImageTexture.CreateFromImage(image);
    }

    // ---- The grid and framing -------------------------------------------------------------------

    private void AddGridCell(ChunkEntry chunk, Vector3 origin)
    {
        var size = _set!.CellSize;
        var y = _collisionLayer.Bounds(chunk)?.GetCenter().Y ?? origin.Y;
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, LineMaterial);
        mesh.SurfaceSetColor(new Color(0.5f, 0.69f, 1, 0.6f));
        foreach (var (x, z) in new[] { (0f, 0f), (size, 0f), (size, size), (0f, size), (0f, 0f) })
            mesh.SurfaceAddVertex(new Vector3(origin.X + x, y, origin.Z + z));
        mesh.SurfaceEnd();
        _gridNode.AddChild(new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _gridNode.AddChild(new Label3D
        {
            Text = chunk.Name,
            Position = new Vector3(origin.X + size / 2, y, origin.Z + size / 2),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            PixelSize = 0.0012f,
            FontSize = 28,
            OutlineSize = 8,
            NoDepthTest = true,
            Modulate = new Color(0.75f, 0.85f, 1),
        });
    }

    /// <summary>
    /// Frames chunks by their collision, cut to their cells, since one chunk has a stray box far
    /// away. Interiors are small rooms in large cells.
    /// </summary>
    private void FrameCells(IEnumerable<ChunkEntry> chunks)
    {
        var set = _set!;
        Aabb? box = null;
        foreach (var chunk in chunks)
        {
            var origin = set.Origin(chunk.Row, chunk.Column).ToGodot();
            var cell = new Aabb(new Vector3(origin.X, -1e6f, origin.Z), new Vector3(set.CellSize, 2e6f, set.CellSize));
            var part = _collisionLayer.Bounds(chunk) is { } bounds && bounds.Intersection(cell) is { Size: { X: > 0, Z: > 0 } } inside
                ? inside
                : new Aabb(new Vector3(origin.X, 0, origin.Z), new Vector3(set.CellSize, 0, set.CellSize));
            box = box is { } b ? b.Merge(part) : part;
        }
        if (box is not { } framed)
            return;
        // Far enough to see the whole set from well outside it.
        _orbit.Far = Mathf.Max(10000, (set.Chunks.Max(c => Math.Max(c.Row, c.Column)) + 1) * set.CellSize * 4);
        _orbit.Pitch = -0.85f;
        _orbit.Yaw = 0.35f;
        _orbit.Frame(_camera, framed.GetCenter(), Mathf.Max(framed.Size.Length() / 2, 5) * 0.6f);
    }

    // ---- Picking and hovering -------------------------------------------------------------------

    /// <summary>The object whose marker is nearest a point on the view, within a marker's reach.</summary>
    private Marked? MarkerAt(Vector2 point)
    {
        if (!_markers.ButtonPressed)
            return null;
        Marked? best = null;
        var bestScore = float.MaxValue;
        foreach (var marked in _shown)
        {
            if (_camera.IsPositionBehind(marked.Position))
                continue;
            var pixels = _camera.UnprojectPosition(marked.Position).DistanceTo(point);
            if (pixels > MarkerPixels * 0.8f)
                continue;
            // Nearer the cursor first, then nearer the camera.
            var score = MathF.Round(pixels) * 1e6f + _camera.GlobalPosition.DistanceTo(marked.Position);
            if (score < bestScore)
                (best, bestScore) = (marked, score);
        }
        return best;
    }

    /// <summary>The scene object whose placed model a point on the view is over, nearest the camera, by the models' bounds.</summary>
    private Marked? PlacedAt(Vector2 point)
    {
        if (!_scenery.ButtonPressed)
            return null;
        var from = _camera.ProjectRayOrigin(point);
        var direction = _camera.ProjectRayNormal(point);
        Marked? best = null;
        var bestDistance = float.MaxValue;
        foreach (var (key, chunk) in _chunks)
        {
            foreach (var placed in chunk.Objects)
            {
                if (!placed.Node.Visible || !placed.Node.IsInsideTree())
                    continue;
                foreach (var level in placed.Levels)
                {
                    if (level is not { Alpha: > 0 })
                        continue;
                    foreach (var mesh in level.Meshes)
                    {
                        var box = mesh.GlobalTransform * mesh.GetAabb();
                        if (RayBox(from, direction, box) is not { } distance)
                            continue;
                        if (distance < bestDistance && _objects.FirstOrDefault(m => m.Chunk.Row == key.Row && m.Chunk.Column == key.Column && m.Object.Index == placed.Object.Index) is { } marked)
                            (best, bestDistance) = (marked, distance);
                    }
                }
            }
        }
        return best;
    }

    private void OnViewInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey { Pressed: true, Echo: false } key when _selected is not null && _container.HasFocus():
                if (key.Keycode == Key.Delete)
                {
                    Delete();
                    return;
                }
                if (key.Keycode == Key.D && key.CtrlPressed)
                {
                    Duplicate();
                    return;
                }
                break;
            case InputEventMouseButton { Pressed: true, DoubleClick: true, ButtonIndex: MouseButton.Left } click:
                if (Cast(click.Position) is { } hit)
                {
                    _orbit.Distance = _camera.GlobalPosition.DistanceTo(hit.Point);
                    _orbit.Target = hit.Point;
                    _orbit.Apply(_camera);
                }
                return;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press when _selected is { } selected && At(press.Position)?.Id == selected.Id:
                // Dragging the picked object: along the ground, keeping its height over it, or up and down with Shift.
                _container.GrabFocus();
                _pressedAt = press.Position;
                _dragging = true;
                _dragFrom = press.Position;
                _dragStart = selected.Position;
                var ground = _collisionLayer.Cast(selected.Position + Vector3.Up * 0.5f, Vector3.Down);
                _dragAbove = ground is { } g && g.Point.Y <= selected.Position.Y + 0.5f ? selected.Position.Y - g.Point.Y : 0;
                return;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right or MouseButton.Middle } press:
                _container.GrabFocus();
                _pressedAt = press.Position;
                break;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release when _dragging:
                _dragging = false;
                if (release.Position.DistanceTo(_pressedAt) >= 4)
                {
                    RebuildMarkers();
                    Select(_selected);
                }
                return;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release when release.Position.DistanceTo(_pressedAt) < 4:
                if (At(release.Position) is { } picked)
                {
                    if (picked.Id != _selected?.Id)
                        Opened?.Invoke(picked.Id);
                }
                else
                {
                    Select(null);
                }
                return;
            case InputEventMouseMotion motion when _dragging && _selected is { } selected:
                if (motion.ShiftPressed)
                {
                    var rise = (_dragFrom.Y - motion.Position.Y) * Mathf.Clamp(_orbit.Distance, 1, 2000) * 0.0015f;
                    MoveSelected(new Vector3(_dragStart.X, _dragStart.Y + rise, _dragStart.Z), rebuild: false);
                }
                else if (Cast(motion.Position) is { } under)
                {
                    MoveSelected(under.Point + Vector3.Up * _dragAbove, rebuild: false);
                }
                return;
            case InputEventMouseMotion motion:
                _pointer = motion.Position;
                break;
        }
        _orbit.Handle(e, _camera);
    }

    /// <summary>How far along a ray a box is first met, or null if it isn't (the slab test).</summary>
    private static float? RayBox(Vector3 from, Vector3 direction, Aabb box)
    {
        float near = 0, far = float.MaxValue;
        for (var axis = 0; axis < 3; axis++)
        {
            var (start, size, d) = (box.Position[axis], box.Size[axis], direction[axis]);
            if (Mathf.Abs(d) < 1e-9f)
            {
                if (from[axis] < start || from[axis] > start + size)
                    return null;
                continue;
            }
            var t1 = (start - from[axis]) / d;
            var t2 = (start + size - from[axis]) / d;
            (near, far) = (Mathf.Max(near, Mathf.Min(t1, t2)), Mathf.Min(far, Mathf.Max(t1, t2)));
            if (near > far)
                return null;
        }
        return near;
    }

    /// <summary>The object under a point on the view: its marker first, else its placed model.</summary>
    private Marked? At(Vector2 point) => MarkerAt(point) ?? PlacedAt(point);

    /// <summary>The line under the view: what's under the cursor, or else what's loaded.</summary>
    private void SetStatus(string? summary = null, string? hover = null)
    {
        _summary = summary ?? _summary;
        _hover = hover ?? _hover;
        _status.Text = _hover.Length > 0 ? _hover : _summary;
    }

    private CollisionLayer.Hit? Cast(Vector2 point) =>
        _collisionLayer.Cast(_camera.ProjectRayOrigin(point), _camera.ProjectRayNormal(point));

    private void ShowHover(Vector2 point)
    {
        if (At(point) is { } marked)
        {
            SetStatus(hover: $"{_catalog!.Describe(marked.Object)} · object {marked.Object.Index} in {marked.Chunk.Name} · {marked.Id}");
            return;
        }
        if (Cast(point) is not { } hit)
        {
            SetStatus(hover: "");
            return;
        }
        var t = hit.Triangle;
        var local = hit.Point - _set!.Origin(hit.Chunk.Row, hit.Chunk.Column).ToGodot();
        var flags = t.Flags & (CollisionFlags.Passable | CollisionFlags.SeeThrough);
        SetStatus(hover:
            $"{hit.Chunk.Name} · {SurfaceNames.Of(t.Surface)} · {CollisionLayer.RoleName((int)t.Role)}" +
            (flags != 0 ? $" · {flags}" : "") +
            $" · ({hit.Point.X:F1}, {hit.Point.Y:F1}, {hit.Point.Z:F1}), in the chunk ({local.X:F1}, {local.Y:F1}, {local.Z:F1})");
    }

    // ---- Selection ------------------------------------------------------------------------------

    private enum Framing { None, OutOfView, Always }

    private void Select(Marked? marked, Framing framing = Framing.None)
    {
        _selected = marked;
        foreach (var child in _selection.GetChildren())
            child.QueueFree();
        _panel.Visible = marked is not null;
        if (marked is null || _catalog is not { } catalog)
            return;

        var o = marked.Object;
        var kind = MapObjects.KindOf(o);
        _selection.AddChild(Points([marked.Position], Colors.White, 18, 0.98f));
        _selection.AddChild(Points([marked.Position], kind.Colour, 12, 0.97f));

        // Its radius, as three circles, and a line to each object it names.
        var json = Edits.IsEdited(marked.Chunk.Map, o.Index) ? MapPatch.ToJson(o) : MapObjects.Json(catalog.Plugins, marked.Id);
        var lines = new ImmediateMesh();
        lines.SurfaceBegin(Mesh.PrimitiveType.Lines, LineMaterial);
        if (o.Radius > 0)
        {
            lines.SurfaceSetColor(new Color(kind.Colour, 0.6f));
            const int Segments = 48;
            for (var i = 0; i < Segments; i++)
            {
                var (a, b) = (Mathf.Tau * i / Segments, Mathf.Tau * (i + 1) / Segments);
                foreach (var axis in new Func<float, Vector3>[]
                {
                    t => new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t)),
                    t => new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0),
                    t => new Vector3(0, Mathf.Cos(t), Mathf.Sin(t)),
                })
                {
                    lines.SurfaceAddVertex(marked.Position + axis(a) * o.Radius);
                    lines.SurfaceAddVertex(marked.Position + axis(b) * o.Radius);
                }
            }
        }
        lines.SurfaceSetColor(new Color(1, 1, 1, 0.9f));
        var linked = Links(json).Where(id => id.Type == RecordTypes.Voxel).Distinct()
            .Select(id => _objects.FirstOrDefault(m => m.Id == id)).OfType<Marked>().ToList();
        foreach (var other in linked)
        {
            lines.SurfaceAddVertex(marked.Position);
            lines.SurfaceAddVertex(other.Position);
        }
        // An empty surface can't be drawn.
        lines.SurfaceAddVertex(marked.Position);
        lines.SurfaceAddVertex(marked.Position);
        lines.SurfaceEnd();
        _selection.AddChild(new MeshInstance3D { Mesh = lines, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (linked.Count > 0)
            _selection.AddChild(Points([.. linked.Select(l => l.Position)], Colors.White, 8, 0.97f));

        _open.Visible = o is ContainerObject && _game is not null;
        _lookThrough.Visible = o is CameraObject { RefPoint: not null } camera && (camera.Flags & CameraFlags.Position) != 0;
        _opened.Text = "";
        var there = Shown(o) ? "" : " It isn't there with these switches.";
        _title.Text = $"{catalog.Describe(o)}\nObject {o.Index} of {marked.Chunk.Map}, in {_set!.Label} {marked.Chunk.Name}.{there}";
        _fields.Show(json, field => catalog.Plugins.Setters(marked.Chunk.Map, $"objects[{o.Index}].{field}"), id => catalog.Find(id) is not null || id.Type == RecordTypes.Voxel);
        FillPanel(o);

        var outOfView = _camera.IsPositionBehind(marked.Position) ||
            !new Rect2(Vector2.Zero, _container.Size).HasPoint(_camera.UnprojectPosition(marked.Position)) ||
            _camera.GlobalPosition.DistanceTo(marked.Position) > 300;
        if (framing == Framing.Always || (framing == Framing.OutOfView && outOfView))
        {
            _orbit.Pitch = -0.6f;
            _orbit.Frame(_camera, marked.Position, Mathf.Clamp(o.Radius * 1.5f, 8, 60));
        }
    }

    // ---- Editing --------------------------------------------------------------------------------

    /// <summary>Shows the picked object's place in the spin boxes, and whether it's been edited.</summary>
    private void FillPanel(MapObject o)
    {
        _filling = true;
        var scene = o as SceneObject;
        _rotationRow.Visible = _scaleRow.Visible = scene is not null;
        for (var i = 0; i < 3; i++)
        {
            _position[i].Value = o.Position[i];
            if (scene is not null)
            {
                _rotation[i].Value = Mathf.RadToDeg(scene.Rotation[i]);
                _scale[i].Value = scene.Scale[i];
            }
        }
        _filling = false;
        if (_editFields.ButtonPressed)
            _json.Text = MapPatch.ToJson(o).ToJsonString(PluginJson.Options);
        _editStatus.Text = _selected is { } marked && Edits.IsEdited(marked.Chunk.Map, o.Index) ? "Edited here, not saved yet." : "";
    }

    private void FromSpins()
    {
        if (_filling || _selected is not { } marked)
            return;
        var o = marked.Object with { Position = new System.Numerics.Vector3((float)_position[0].Value, (float)_position[1].Value, (float)_position[2].Value) };
        if (o is SceneObject scene)
        {
            o = scene with
            {
                Rotation = new System.Numerics.Vector3(Mathf.DegToRad((float)_rotation[0].Value), Mathf.DegToRad((float)_rotation[1].Value), Mathf.DegToRad((float)_rotation[2].Value)),
                Scale = new System.Numerics.Vector3((float)_scale[0].Value, (float)_scale[1].Value, (float)_scale[2].Value),
            };
        }
        SetObject(o);
    }

    /// <summary>Moves the picked object to a place in the map set. Without <paramref name="rebuild"/>, the markers wait for the drag's end.</summary>
    private void MoveSelected(Vector3 position, bool rebuild)
    {
        if (_selected is not { } marked)
            return;
        var origin = _set!.Origin(marked.Chunk.Row, marked.Chunk.Column).ToGodot();
        SetObject(marked.Object with { Position = (position - origin).ToNumerics() }, rebuild);
    }

    /// <summary>Puts an object, changed, in its map and the view, and keeps it picked.</summary>
    private void SetObject(MapObject o, bool rebuild = true)
    {
        if (_selected is not { } marked || _catalog is not { } catalog)
            return;
        var map = catalog.Plugins.Get<MapRecord>(marked.Chunk.Map);
        Edits.Set(map, o);
        var origin = _set!.Origin(marked.Chunk.Row, marked.Chunk.Column).ToGodot();
        var updated = marked with { Object = o, Position = o.Position.ToGodot() + origin };
        var at = _objects.IndexOf(marked);
        if (at >= 0)
            _objects[at] = updated;
        if (o is SceneObject scene && _chunks.TryGetValue((marked.Chunk.Row, marked.Chunk.Column), out var scenery))
        {
            if (scenery.Find(o.Index) is { } placed)
                placed.Node.Transform = ChunkScenery.TransformOf(scene);
            else
                PlaceNew(scenery, scene);
        }
        _selected = updated;
        UpdateSaveButton();
        if (rebuild)
        {
            RebuildMarkers();
            Select(updated);
        }
        else
        {
            // Only the selection's overlay follows during a drag.
            foreach (var child in _selection.GetChildren())
                child.QueueFree();
            _selection.AddChild(Points([updated.Position], Colors.White, 18, 0.98f));
            _selection.AddChild(Points([updated.Position], MapObjects.KindOf(o).Colour, 12, 0.97f));
            _filling = true;
            for (var i = 0; i < 3; i++)
                _position[i].Value = o.Position[i];
            _filling = false;
        }
    }

    private void DropToGround()
    {
        if (_selected is not { } marked)
            return;
        var ground = _collisionLayer.Cast(marked.Position + Vector3.Up * 50, Vector3.Down);
        if (ground is { } g)
            MoveSelected(new Vector3(marked.Position.X, g.Point.Y, marked.Position.Z), rebuild: true);
        else
            _editStatus.Text = "No collision under it.";
    }

    private void Duplicate()
    {
        if (_selected is not { } marked || _catalog is not { } catalog)
            return;
        var map = catalog.Plugins.Get<MapRecord>(marked.Chunk.Map);
        var copy = Edits.Duplicate(map, marked.Object with { Position = marked.Object.Position + new System.Numerics.Vector3(1, 0, 0) });
        var origin = _set!.Origin(marked.Chunk.Row, marked.Chunk.Column).ToGodot();
        var added = new Marked(MapRecord.VoxelId(marked.Chunk.Map, copy.Index), copy, marked.Chunk, copy.Position.ToGodot() + origin);
        _objects.Add(added);
        if (copy is SceneObject scene && _chunks.TryGetValue((marked.Chunk.Row, marked.Chunk.Column), out var scenery))
            PlaceNew(scenery, scene);
        UpdateSaveButton();
        RebuildMarkers();
        Select(added);
    }

    private void Delete()
    {
        if (_selected is not { } marked || _catalog is not { } catalog)
            return;
        var map = catalog.Plugins.Get<MapRecord>(marked.Chunk.Map);
        Edits.Remove(map, marked.Object.Index);
        _objects.Remove(marked);
        if (marked.Object is SceneObject && _chunks.TryGetValue((marked.Chunk.Row, marked.Chunk.Column), out var scenery))
            scenery.Remove(marked.Object.Index);
        UpdateSaveButton();
        RebuildMarkers();
        Select(null);
    }

    /// <summary>The fields as typed, read strictly as the game reads them; what's wrong is said under the box.</summary>
    private void ApplyFields()
    {
        if (_selected is not { } marked)
            return;
        try
        {
            var json = JsonNode.Parse(_json.Text, documentOptions: PluginJson.DocumentOptions) as JsonObject
                ?? throw new InvalidDataException("The fields are a JSON object.");
            json["index"] = marked.Object.Index;
            JsonShape.Normalise(json, typeof(MapObject));
            var o = json.Deserialize<MapObject>(PluginJson.Strict) ?? throw new InvalidDataException("Nothing to read.");
            SetObject(o);
        }
        catch (Exception e) when (e is System.Text.Json.JsonException or InvalidDataException)
        {
            _editStatus.Text = e.Message.Split(" Path:")[0];
        }
    }

    private void UpdateSaveButton()
    {
        _save.Visible = _discard.Visible = Edits.Dirty;
        _save.Text = $"Save {Edits.Count} edit{(Edits.Count == 1 ? "" : "s")} to mod…";
    }

    /// <summary>Asks which mod to write the edits to, writes them, and asks for the plugins to be loaded again.</summary>
    private void AskToSave()
    {
        if (_catalog is not { } catalog)
            return;
        var dialog = new ConfirmationDialog { Title = "Save edits to a mod", OkButtonText = "Save" };
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = "The mod's ID, which names its folder in the plugins folder: lower-case letters, digits and underscores. A mod that isn't there is made." });
        var id = new LineEdit { Text = _modId, PlaceholderText = "my_village" };
        box.AddChild(id);
        var error = new Label { Modulate = new Color(1, 0.6f, 0.6f) };
        box.AddChild(error);
        dialog.AddChild(box);
        dialog.Confirmed += () =>
        {
            try
            {
                _modId = id.Text.Trim();
                var pluginsDirectory = Path.GetDirectoryName(catalog.Plugins.Plugins[0].Directory.TrimEnd('/', '\\'))!;
                var mod = ModFolder.OpenOrCreate(pluginsDirectory, _modId);
                var report = Edits.Save(catalog.Plugins, mod);
                SetStatus(summary: $"Saved to {mod.Directory}: {string.Join("; ", report)}");
                UpdateSaveButton();
                Saved?.Invoke();
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                SetStatus(summary: e.Message);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(520, 180));
        id.GrabFocus();
    }

    /// <summary>
    /// Puts the view at the picked camera object's reference point, aimed where the player stands to
    /// trigger it. A camera object that tracks aims at the player, 1.05 above his sphere's centre.
    /// </summary>
    public void LookThrough()
    {
        if (_selected is not { Object: CameraObject { RefPoint: { } id } camera } marked || _objects.FirstOrDefault(m => m.Id == id) is not { } point)
            return;
        // The middle of the triangles, raised to the player's sphere's centre and his camera's height.
        var target = _collisionLayer.TriggerCentre(marked.Chunk, camera.Index) is { } floor
            ? floor + new Vector3(0, 0.5f + 1.05f, 0)
            : marked.Position;
        var look = point.Position - target;
        if (look.LengthSquared() < 1e-6f)
            return;
        _orbit.Target = target;
        _orbit.Distance = look.Length();
        look /= _orbit.Distance;
        _orbit.Pitch = Mathf.Asin(Mathf.Clamp(-look.Y, -1, 1));
        _orbit.Yaw = Mathf.Atan2(look.X, look.Z);
        _orbit.Apply(_camera);
    }

    /// <summary>The loot window open on a container, if any.</summary>
    public BarterWindowPanel? LootWindow => _lootWindow is { } window && IsInstanceValid(window)
        ? window.GetChildren().OfType<ScrollContainer>().FirstOrDefault()?.GetChildren().OfType<BarterWindowPanel>().FirstOrDefault()
        : null;

    /// <summary>
    /// Opens the container picked in the loot window against the editor's game (<c>--roll</c>): a source's
    /// reagent at the party's harvest share, or its loot table rolled, or its gold, and the item it always
    /// holds. Its flags stay as they are, so it can be opened again.
    /// </summary>
    public void Roll()
    {
        if (_selected?.Object is not ContainerObject record || _game is not { } game || _catalog is not { } catalog)
            return;
        var container = new AidynEnhanced.Core.Items.Container(record);
        var party = game.Game.Party;
        var (pile, gold) = record.Kind is ContainerKind.Herb or ContainerKind.Spice or ContainerKind.Gem
            ? (container.Reagent(Containers.HarvestShare(party, _random), game.Game.Items), 0)
            : container.Contents(game.Game.Items, id => catalog.Plugins.Get<LootRecord>(id), _random);
        if (pile.Stacks.Count == 0)
        {
            // With no items, the gold alone, and a message rather than the window.
            party.Gold += (uint)gold;
            _opened.Text = gold > 0 ? $"It held {gold:N0} gold alone, which is the party's now." : "It held nothing.";
            return;
        }
        _opened.Text = "";
        if (_lootWindow is { } old && IsInstanceValid(old))
            old.QueueFree();
        var window = _lootWindow = new Window { Title = $"Loot: {catalog.Describe(record)}", Size = new Vector2I(720, 720), Transient = true };
        window.CloseRequested += window.QueueFree;
        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        var panel = BarterWindowPanel.ForLoot(catalog.Plugins, game, pile, gold, catalog.Describe(record));
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(panel);
        window.AddChild(scroll);
        AddChild(window);
        window.PopupCentered();
    }

    /// <summary>Every record ID the fields name.</summary>
    private static IEnumerable<RecordId> Links(JsonNode? node) => node switch
    {
        JsonObject o => o.SelectMany(p => Links(p.Value)),
        JsonArray a => a.SelectMany(Links),
        JsonValue v when v.TryGetValue(out string? text) && RecordId.TryParse(text, out var id) => [id],
        _ => [],
    };
}
