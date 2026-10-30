using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;
using AidynEnhanced.Views;
using Godot;

namespace AidynEnhanced.Editor;

/// <summary>
/// A small view of one model, for a panel: the model framed whole, lit as the model viewer lights, turned by dragging
/// and zoomed by scrolling. The map view shows the picked scene object's model in it.
/// </summary>
public partial class ModelThumb : SubViewportContainer
{
    private readonly SubViewport _viewport = new() { OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X };
    private readonly Camera3D _camera = new() { Fov = 45 };
    private readonly Orbit _orbit = new();
    private Node3D? _content;
    private RecordId? _shown;

    public ModelThumb()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(0, 240);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        AddChild(_viewport);
        _viewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.16f, 0.17f, 0.2f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.45f, 0.45f, 0.45f),
            },
        });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.8f, RotationDegrees = new Vector3(-50, 35, 0) });
        _viewport.AddChild(_camera);
        GuiInput += e =>
        {
            if (_orbit.Handle(e, _camera) && _content is not null)
                Billboards.Face(Billboards.Facings(_content), _camera.GlobalPosition);
        };
    }

    /// <summary>Shows a scene's or an actor's model, or nothing for an ID with no model.</summary>
    public void Show(PluginSet plugins, Models models, RecordId? model)
    {
        if (model == _shown)
            return;
        Clear();
        _shown = model;
        if (model is not { } id)
            return;
        var scene = id.Type == RecordTypes.Actor ? plugins.TryGet<ActorRecord>(id)?.Scene : id;
        if (scene is not { } sceneId || models.Instantiate(sceneId) is not { } node)
            return;
        _content = node;
        _viewport.AddChild(node);
        Visible = true;

        Aabb? bounds = null;
        foreach (var mesh in Nodes.FindAll<GeometryInstance3D>(node))
        {
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } b ? b.Merge(box) : box;
        }
        var framed = bounds ?? new Aabb(Vector3.Zero, Vector3.One);
        _orbit.Yaw = 0.6f;
        _orbit.Pitch = -0.3f;
        _orbit.Frame(_camera, framed.GetCenter(), Mathf.Max(framed.Size.Length() / 2, 0.5f) * 0.8f);
        Billboards.Face(Billboards.Facings(node), _camera.GlobalPosition);
    }

    public void Clear()
    {
        _content?.QueueFree();
        _content = null;
        _shown = null;
        Visible = false;
    }
}
