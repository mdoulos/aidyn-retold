using System.Globalization;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Body section of a model's inspector: how the character it's shown as stands in the game (<see cref="EntityBody"/>), its
/// collision sphere's radius, its height, the scale its model is drawn at and where in an attack the blow lands, drawn on the model:
/// the sphere, a ring at its height, and Alaron beside it for size, as a figure of his own sphere and height.
/// </summary>
public partial class BodySection : VBoxContainer
{
    private static readonly Color Sphere = new(0.35f, 0.75f, 1f, 0.1f), Footprint = new(0.35f, 0.75f, 1f, 0.8f), Ring = new(1f, 0.85f, 0.35f, 0.95f),
        Figure = new(0.8f, 0.8f, 0.85f, 0.25f);

    private readonly ModelView _view;
    private readonly EntityBody _body;
    private readonly EntityBody? _alaron;
    private readonly CheckBox _sphere = new() { Text = "Collision sphere", ButtonPressed = true };
    private readonly CheckBox _height = new() { Text = "Height", ButtonPressed = true };
    private readonly CheckBox _beside = new() { Text = "Alaron beside it", TooltipText = "A figure of Alaron's sphere and height, for size" };
    private readonly CheckBox _scale = new() { Text = "At the game's scale" };
    private Node3D? _marks;

    /// <param name="alaron">Alaron's body, to stand beside this one; null when this is his.</param>
    public BodySection(ModelView view, EntityBody body, EntityBody? alaron)
    {
        (_view, _body, _alaron) = (view, body, alaron);
        AddThemeConstantOverride("separation", 4);
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 12);
        void Value(string name, string value, string tooltip)
        {
            var label = EditorStyle.DimLabel(name);
            label.TooltipText = tooltip;
            grid.AddChild(label);
            grid.AddChild(new Label { Text = value, TooltipText = tooltip });
        }
        Value("Radius", Number(body.Radius), "Its collision sphere's radius, before the scale");
        Value("Height", Number(body.Height), "About how tall its model is at a scale of 1, in scene units");
        Value("Scale", "×" + Number(body.Scale), "How much bigger than its model it's drawn, and its sphere when bodies push each other");
        Value("Strike", $"{body.Strike * 100:0}%", "The share of its attack's animation at which the blow lands");
        AddChild(grid);

        var toggles = new HFlowContainer();
        toggles.AddChild(_sphere);
        toggles.AddChild(_height);
        if (alaron is not null)
            toggles.AddChild(_beside);
        _scale.Disabled = Math.Abs(body.Scale - 1) < 1e-4f;
        _scale.TooltipText = _scale.Disabled ? "Drawn at its model's own size" : $"Draw the model ×{Number(body.Scale)}, as the game does";
        _scale.ButtonPressed = !_scale.Disabled;
        toggles.AddChild(_scale);
        AddChild(toggles);

        foreach (var box in new[] { _sphere, _height, _beside, _scale })
            box.Toggled += _ => Mark();
        TreeExiting += () => _marks?.QueueFree();
    }

    public override void _Ready() => Mark();

    /// <summary>Draws the marks on the model as the switches say, replacing what was drawn.</summary>
    private void Mark()
    {
        _marks?.QueueFree();
        _marks = null;
        if (_view.Content is not { } content)
            return;
        _view.ScaleModel(_scale.ButtonPressed ? _body.Scale : 1);
        _marks = new Node3D { Name = "body marks" };
        if (_sphere.ButtonPressed)
        {
            // Faint, so the model shows through it, with its widest ring drawn on the ground, where it meets walls.
            _marks.AddChild(Mesh(new SphereMesh { Radius = _body.Radius, Height = _body.Radius * 2 }, Sphere, new Vector3(0, _body.Radius, 0)));
            _marks.AddChild(Mesh(new TorusMesh { InnerRadius = _body.Radius * 0.97f, OuterRadius = _body.Radius, Rings = 48 }, Footprint, new Vector3(0, 0.01f, 0)));
        }
        if (_height.ButtonPressed)
        {
            _marks.AddChild(Mesh(new TorusMesh { InnerRadius = 0.2f, OuterRadius = 0.23f, Rings = 32 }, Ring, new Vector3(0, _body.Height, 0)));
            _marks.AddChild(new Label3D
            {
                Text = Number(_body.Height), Position = new Vector3(0.32f, _body.Height, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 40, PixelSize = 0.004f, Modulate = Ring, NoDepthTest = true, HorizontalAlignment = HorizontalAlignment.Left,
            });
        }
        if (_beside.ButtonPressed && _alaron is { } a)
        {
            // Beside it, clear of both spheres, at Alaron's size whatever this model is drawn at.
            var scale = _scale.ButtonPressed ? _body.Scale : 1;
            var x = (_body.Radius * scale + a.Radius + 0.4f) / scale;
            var figure = new Node3D { Position = new Vector3(x, 0, 0), Scale = Vector3.One / scale };
            figure.AddChild(Mesh(new CapsuleMesh { Radius = a.Radius, Height = a.Height }, Figure, new Vector3(0, a.Height / 2, 0)));
            figure.AddChild(new Label3D
            {
                Text = $"Alaron, {Number(a.Height)}", Position = new Vector3(0, a.Height + 0.25f, 0), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 48, PixelSize = 0.004f, NoDepthTest = true,
            });
            _marks.AddChild(figure);
        }
        content.AddChild(_marks);
    }

    private static MeshInstance3D Mesh(Mesh mesh, Color color, Vector3 at) => new()
    {
        Mesh = mesh,
        Position = at,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        },
    };

    private static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
