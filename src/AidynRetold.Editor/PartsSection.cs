using AidynRetold.Plugins.Assets;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Parts section of a model's inspector: the model's tree of nodes as the game finds them by name (<see cref="SceneNames"/>),
/// its parts (transforms, the actor's root among them), its locators and what the game hangs from each, its lights, camera and
/// emitters, and a remodel's own nodes. The one picked is marked in the view, moving with it: a box round a part's own meshes, or a
/// locator's axes, its attachment's turned as what's held there is.
/// </summary>
public partial class PartsSection : VBoxContainer
{
    private static readonly Color Box = new(1f, 0.85f, 0.3f), AxisX = new(1f, 0.35f, 0.35f), AxisY = new(0.4f, 1f, 0.4f), AxisZ = new(0.4f, 0.6f, 1f);

    private readonly ModelContext _ctx;
    private readonly Tree _tree = new()
    {
        Columns = 2, HideRoot = true, CustomMinimumSize = new Vector2(0, 260), SelectMode = Tree.SelectModeEnum.Row, SizeFlagsVertical = SizeFlags.ExpandFill,
    };
    private readonly Label _about = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };
    // What's picked, said under the tree; hidden until something is, so the tree runs to the card's foot.
    private readonly MarginContainer _aboutInset = new() { Visible = false };
    private readonly Dictionary<TreeItem, Node3D> _nodes = [];
    private MeshInstance3D? _mark;
    private int _parts, _locators;

    public PartsSection(ModelContext ctx)
    {
        _ctx = ctx;
        AddThemeConstantOverride("separation", 0);
        _tree.SetColumnExpand(0, true);
        _tree.SetColumnExpand(1, false);
        _tree.SetColumnCustomMinimumWidth(1, 96);
        _tree.ItemSelected += Picked;
        // The tree spans the card (its section is flush), its folds' arrows inset by the table's indent; what's picked is said under
        // it, inset as a card's body is.
        _tree.ThemeTypeVariation = "FlushTree";
        AddChild(_tree);
        _about.ThemeTypeVariation = "DimLabel";
        foreach (var (side, by) in new[] { ("left", 24), ("right", 24), ("top", 12), ("bottom", 18) })
            _aboutInset.AddThemeConstantOverride($"margin_{side}", by);
        _aboutInset.AddChild(_about);
        AddChild(_aboutInset);
        if (ctx.ViewOrThrow.Content is { } content)
        {
            var root = _tree.CreateItem();
            foreach (var child in content.GetChildren().OfType<Node3D>())
                Add(root, child);
        }
        TreeExiting += Unmark;
    }

    /// <summary>What to say after the section's title: how many parts and locators.</summary>
    public string Summary => $"{_parts} {(_parts == 1 ? "part" : "parts")}, {_locators} {(_locators == 1 ? "locator" : "locators")}";

    private int? Root => _ctx.ViewOrThrow.Actor?.RootTransform;

    /// <summary>Picks the node named <paramref name="name"/>, as clicking it does (<c>--part</c>).</summary>
    public void Select(string name)
    {
        if (_nodes.Keys.FirstOrDefault(i => i.GetText(0) == name) is not { } item)
            return;
        for (var parent = item.GetParent(); parent is not null; parent = parent.GetParent())
            parent.Collapsed = false;
        item.Select(0);
        _tree.ScrollToItem(item);
    }

    private void Add(TreeItem parent, Node3D node)
    {
        // The view's own marks aren't the model's.
        if (node.Name.ToString() is "body marks" or "OutfitSwitch")
            return;
        var name = node.Name.ToString();
        var item = _tree.CreateItem(parent);
        item.SetText(0, name);
        item.SetText(1, Kind(name, node));
        item.SetCustomColor(1, EditorStyle.Dim);
        item.SetTooltipText(0, Explain(name));
        item.SetTooltipText(1, Explain(name));
        _nodes[item] = node;
        foreach (var child in node.GetChildren().OfType<Node3D>())
            Add(item, child);
        // Deep trees start folded below their parts, as a remodel's skeleton and meshes do.
        item.Collapsed = item.GetChildCount() > 12;
    }

    /// <summary>What the game reads a node as, from its name.</summary>
    private string Kind(string name, Node3D node)
    {
        if (SceneNames.TryTransform(name, out var t))
        {
            _parts++;
            return t == Root ? "root part" : "part";
        }
        if (SceneNames.TryAttachment(name, out _))
            return "attachment";
        if (SceneNames.TryLocator(name, out var l))
        {
            _locators++;
            return LocatorUse(l);
        }
        if (SceneNames.TryLight(name, out _))
            return "light";
        if (SceneNames.TryEmitter(name, out _))
            return "particle emitter";
        if (name == SceneNames.Camera)
            return "camera";
        if (name.EndsWith(" billboard", StringComparison.Ordinal) || name.EndsWith(SceneNames.FacingSuffix, StringComparison.Ordinal) || name.EndsWith(" card", StringComparison.Ordinal))
            return "billboard";
        return node switch
        {
            Skeleton3D => "skeleton",
            MeshInstance3D => "mesh",
            _ => "",
        };
    }

    /// <summary>What the game does with a node of this name, at length, for its tooltip.</summary>
    private static string Explain(string name) =>
        SceneNames.TryAttachment(name, out var a) ? $"{name}: locator {a} turned by its model's rotation, which what's held there hangs from"
        : SceneNames.TryLocator(name, out var l) ? $"{name}: where the game hangs {l switch { 0 => "a held weapon, or a bow's arrow", 1 => "a bow", 2 => "a shield", 3 => "Alaron's head, or a Troubadour's notes", _ => "what a script or spell puts there" }}, and spells' effects"
        : SceneNames.TryTransform(name, out _) ? $"{name}: a part, which the animations move by its name"
        : name;

    /// <summary>What a body hangs from a locator, as the combat views and Alaron's head do.</summary>
    private static string LocatorUse(int locator) => locator switch
    {
        0 => "weapon hand",
        1 => "bow hand",
        2 => "shield arm",
        3 => "head, notes",
        _ => "locator",
    };

    private void Picked()
    {
        Unmark();
        if (_tree.GetSelected() is not { } item || !_nodes.TryGetValue(item, out var node) || !IsInstanceValid(node))
            return;
        _aboutInset.Visible = true;
        var name = node.Name.ToString();
        var meshes = node.GetChildren().OfType<MeshInstance3D>().Where(m => m.Mesh is not null).ToList();
        if (node is MeshInstance3D { Mesh: not null } self)
            meshes.Add(self);
        if (meshes.Count > 0)
        {
            // Its own meshes, in its own space, so the box moves and turns with it.
            Aabb? bounds = null;
            foreach (var mesh in meshes)
            {
                var box = (node == mesh ? Transform3D.Identity : mesh.Transform) * mesh.GetAabb();
                bounds = bounds is { } b ? b.Merge(box) : box;
            }
            _mark = Lines(BoxEdges(bounds!.Value.Grow(bounds.Value.Size.Length() * 0.03f)), [Box]);
            _about.Text = $"{name}: {meshes.Count} {(meshes.Count == 1 ? "mesh" : "meshes")} of its own, boxed in the view.";
        }
        else
        {
            var size = (_ctx.ViewOrThrow.Content is { } c ? Frame(c) : 1) * 0.12f;
            _mark = Lines([Vector3.Zero, Vector3.Right * size, Vector3.Zero, Vector3.Up * size, Vector3.Zero, Vector3.Back * size], [AxisX, AxisX, AxisY, AxisY, AxisZ, AxisZ]);
            _about.Text = $"{name}: no mesh of its own; its axes are drawn, X red, Y green, Z blue.";
        }
        node.AddChild(_mark);
    }

    private void Unmark()
    {
        if (_mark is not null && IsInstanceValid(_mark))
            _mark.QueueFree();
        _mark = null;
    }

    /// <summary>About how big the model is, for the axes' length.</summary>
    private static float Frame(Node3D content)
    {
        Aabb? bounds = null;
        foreach (var mesh in Nodes.FindAll<MeshInstance3D>(content))
        {
            if (mesh.Mesh is null)
                continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } b ? b.Merge(box) : box;
        }
        return Math.Max(bounds?.Size.Length() ?? 1, 0.1f);
    }

    private static Vector3[] BoxEdges(Aabb a)
    {
        var p = new Vector3[8];
        for (var i = 0; i < 8; i++)
            p[i] = a.Position + new Vector3((i & 1) != 0 ? a.Size.X : 0, (i & 2) != 0 ? a.Size.Y : 0, (i & 4) != 0 ? a.Size.Z : 0);
        int[] edges = [0, 1, 1, 3, 3, 2, 2, 0, 4, 5, 5, 7, 7, 6, 6, 4, 0, 4, 1, 5, 2, 6, 3, 7];
        return [.. edges.Select(e => p[e])];
    }

    /// <summary>Lines drawn over the model, through it, each pair of points one line, in its colour or the first.</summary>
    private static MeshInstance3D Lines(Vector3[] points, Color[] colors)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (var i = 0; i < points.Length; i++)
        {
            mesh.SurfaceSetColor(colors.Length == points.Length ? colors[i] : colors[0]);
            mesh.SurfaceAddVertex(points[i]);
        }
        mesh.SurfaceEnd();
        return new MeshInstance3D
        {
            Name = "part mark", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, NoDepthTest = true, RenderPriority = 2,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
    }
}
