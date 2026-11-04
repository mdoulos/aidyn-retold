using System.Globalization;
using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A scrolling page of headings, lines, grids and links, which a record's preview rebuilds as what it
/// shows changes: a character sheet, an item, a shop, a loot table or a recipe.
/// </summary>
public partial class PagePanel : ScrollContainer
{
    /// <summary>The theme's dim text, for notes under what a page shows.</summary>
    protected static Color Dim => EditorStyle.Dim;

    private readonly Dictionary<RecordId, Texture2D?> _icons = [];

    protected PagePanel(PluginCatalog catalog, Action<RecordId> open)
    {
        Catalog = catalog;
        Open = open;
        HorizontalScrollMode = ScrollMode.Disabled;
        Body.AddThemeConstantOverride("separation", 6);
        AddChild(Body);
    }

    protected PluginCatalog Catalog { get; }

    protected PluginSet Plugins => Catalog.Plugins;

    /// <summary>Opens a record, as a link does.</summary>
    protected Action<RecordId> Open { get; }

    protected VBoxContainer Body { get; } = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };

    protected void ClearBody()
    {
        foreach (var child in Body.GetChildren())
        {
            Body.RemoveChild(child);
            child.QueueFree();
        }
    }

    protected void Heading(string text)
    {
        Body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        Body.AddChild(new Label { Text = text, ThemeTypeVariation = "HeadingLabel" });
    }

    protected Label Line(string text, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        if (color is { } c)
            label.AddThemeColorOverride("font_color", c);
        Body.AddChild(label);
        return label;
    }

    /// <summary>A grid with a column per heading, and a row of the headings unless they're all blank.</summary>
    protected static GridContainer Grid(string[] headings)
    {
        var grid = new GridContainer { Columns = headings.Length };
        grid.AddThemeConstantOverride("h_separation", 16);
        if (headings.All(h => h.Length == 0))
            return grid;
        foreach (var heading in headings)
            grid.AddChild(new Label { Text = heading, ThemeTypeVariation = "DimLabel" });
        return grid;
    }

    protected static void Row(GridContainer grid, params string[] cells)
    {
        foreach (var cell in cells)
            grid.AddChild(new Label { Text = cell });
    }

    protected Button LinkButton(RecordId id, string text)
    {
        var button = new Button { Text = text, Flat = true, Alignment = HorizontalAlignment.Left, TooltipText = $"Open {id}." };
        button.Pressed += () => Open(id);
        return button;
    }

    /// <summary>A link to an item, with its icon.</summary>
    protected Control ItemLink(RecordId id, RecordId? icon, string text, int scale = 1)
    {
        var row = new HBoxContainer();
        if (icon is { } i && Picture(i, scale) is { } picture)
            row.AddChild(picture);
        row.AddChild(LinkButton(id, text));
        return row;
    }

    /// <summary>
    /// An image or texture record's picture as the page shows it, or null if no plugin has it: at the size the game draws it
    /// (<see cref="PluginSet.ImageSize"/>), <paramref name="scale"/> times, pixel for pixel; a larger replacement, as the redrawn art's,
    /// drawn into that size, smoothed, so it's sharper rather than larger, as the game draws it (<c>UiImages</c>).
    /// </summary>
    protected TextureRect? Picture(RecordId id, int scale = 1)
    {
        if (Icon(id) is not { } texture)
            return null;
        var (width, height) = Plugins.ImageSize(id);
        var size = width > 0 ? new Vector2(width, height) : texture.GetSize();
        return new TextureRect
        {
            Texture = texture,
            TextureFilter = texture.GetSize().X > size.X ? TextureFilterEnum.LinearWithMipmaps : TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            CustomMinimumSize = size * scale,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }

    /// <summary>An image or texture record's PNG, read once per page; mipmapped if it's larger than the original's (<see cref="Picture"/>).</summary>
    protected Texture2D? Icon(RecordId id)
    {
        if (_icons.TryGetValue(id, out var texture))
            return texture;
        if (Catalog.Find(id)?.Asset("png") is { } png && Png.Load(png.Path) is { } image)
            texture = ImageTexture.CreateFromImage(image.GetWidth() > Plugins.ImageSize(id).Width ? Png.Mipmapped(image) : image);
        return _icons[id] = texture;
    }

    /// <summary>A record's <c>name</c> field, or its key if it has none.</summary>
    protected string NameOf(RecordId id) => (string?)Plugins.GetJson(id)?["name"] ?? id.Key;

    protected static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    protected static string Num(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
