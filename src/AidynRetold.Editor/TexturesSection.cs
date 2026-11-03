using System.Globalization;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Assets;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The Textures section of a model's inspector (ADR 0044): a tile for each texture the model draws, as the game draws it (a plugin's
/// PNG in place of the model's copy where there's one), with how many other models use it; and for the one picked, where it comes
/// from, the models that share it, and the buttons that export it, replace it on this model or everywhere, and put it back.
/// </summary>
public partial class TexturesSection : VBoxContainer
{
    private const int Tile = 64;
    // The mode's, made as the section is, which a change of mode makes again.
    private readonly StyleBoxFlat Plain = Frame(EditorTheme.Current.Border), Picked = Frame(EditorTheme.Current.Accent);

    private readonly ModelContext _ctx;
    private readonly List<Entry> _entries = [];
    private readonly HFlowContainer _tiles = new();
    private readonly VBoxContainer _details = new();
    private readonly List<PanelContainer> _frames = [];
    private TextureUsage? _usage;
    private int _picked = -1;

    /// <summary>One image of the model: a texture by its Borg1, or a remodel's own image, with no number.</summary>
    private sealed record Entry(int? Borg1, string Name, byte[] Png, TextureOverride? Override)
    {
        public string Label => Borg1 is { } b ? $"0x{b:X4}" : Name;
    }

    public TexturesSection(ModelContext ctx)
    {
        _ctx = ctx;
        AddThemeConstantOverride("separation", 8);
        _tiles.AddThemeConstantOverride("h_separation", 6);
        _tiles.AddThemeConstantOverride("v_separation", 6);
        AddChild(_tiles);
        _details.AddThemeConstantOverride("separation", 6);
        AddChild(_details);
        Read();
        if (_entries.Count == 0)
        {
            AddChild(EditorStyle.DimLabel("No textures in this model."));
            return;
        }
        for (var i = 0; i < _entries.Count; i++)
            _tiles.AddChild(TileFor(i));
        var all = new Button { Text = "Export all…", TooltipText = "Save every texture the model draws, as PNGs named for their numbers" };
        all.Pressed += ExportAll;
        var row = EditorStyle.Row();
        row.AddChild(all);
        AddChild(row);
        Pick(0);
        _ = BackgroundTask.Report(CountUsage());
    }

    /// <summary>What to say after the section's title: how many textures, and how many a plugin replaces.</summary>
    public string Summary => _entries.Count == 0 ? "none"
        : $"{_entries.Count}" + (_entries.Count(e => e.Override is not null) is > 0 and var n ? $", {n} replaced" : "");

    /// <summary>The model's images from its file, each with the plugin PNG drawn in its place.</summary>
    private void Read()
    {
        if (_ctx.View.File is not { } file || !file.Reader.Root.TryGetProperty("images", out var images))
            return;
        var i = 0;
        foreach (var image in images.EnumerateArray())
        {
            var name = image.TryGetProperty("name", out var n) ? n.GetString() ?? $"image {i}" : $"image {i}";
            var png = image.TryGetProperty("bufferView", out var view) ? file.Reader.BufferViewBytes(view.GetInt32()).ToArray() : [];
            if (SceneNames.TryImage(name, out var borg1))
                _entries.Add(new Entry(borg1, name, png, _ctx.Original ? null : TextureOverrides.Find(_ctx.Plugins, _ctx.Scene, borg1)));
            else
                _entries.Add(new Entry(null, name, png, null));
            i++;
        }
    }

    private Control TileFor(int index)
    {
        var entry = _entries[index];
        var frame = new PanelContainer { TooltipText = Tooltip(entry), MouseDefaultCursorShape = CursorShape.PointingHand };
        frame.AddThemeStyleboxOverride("panel", Plain);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(new TextureRect
        {
            Texture = Texture(entry), CustomMinimumSize = new Vector2(Tile, Tile), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, TextureFilter = TextureFilterEnum.Nearest, MouseFilter = MouseFilterEnum.Ignore,
        });
        var label = new Label { Text = entry.Label, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 12);
        if (entry.Override is not null)
            label.ThemeTypeVariation = "ChangedLabel";
        box.AddChild(label);
        frame.AddChild(box);
        frame.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                Pick(index);
        };
        _frames.Add(frame);
        return frame;
    }

    private static string Tooltip(Entry entry) => entry.Override is { } o
        ? $"{entry.Label}: {(o.Reach == TextureReach.Model ? "this model's own" : "everywhere")}, from {o.Plugin}"
        : entry.Borg1 is null ? $"{entry.Name}: the model's own image, with no texture number" : $"{entry.Label}: the model's copy";

    /// <summary>The texture as the model draws it: the plugin's PNG, or the model's own copy.</summary>
    private static Texture2D? Texture(Entry entry)
    {
        if (entry.Override is { } o)
            return TextureReplacements.Load(o.Path);
        var image = new Image();
        return entry.Png.Length > 0 && image.LoadPngFromBuffer(entry.Png) == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
    }

    private async Task CountUsage()
    {
        _usage = await _ctx.Usage();
        if (!IsInstanceValid(this))
            return;
        for (var i = 0; i < _entries.Count && i < _frames.Count; i++)
        {
            if (_entries[i].Borg1 is { } b)
                _frames[i].TooltipText = $"{Tooltip(_entries[i])}. {Others(b)}";
        }
        Pick(_picked);
    }

    /// <summary>How many other models use a texture, in words.</summary>
    private string Others(int borg1)
    {
        if (_usage is null)
            return "Counting the models that use it…";
        var others = _usage.ModelsUsing(borg1).Count(m => m != _ctx.Scene);
        return others == 0 ? "No other model uses it." : $"{others} other {(others == 1 ? "model uses" : "models use")} it.";
    }

    private void Pick(int index)
    {
        if (index < 0 || index >= _entries.Count)
            return;
        _picked = index;
        for (var i = 0; i < _frames.Count; i++)
            _frames[i].AddThemeStyleboxOverride("panel", i == index ? Picked : Plain);
        foreach (var child in _details.GetChildren())
            child.QueueFree();

        var entry = _entries[index];
        var size = entry.Override is { } o && PngHeader.Size(o.Path) is var (ow, oh) ? $"{ow}×{oh}"
            : PngHeader.Size(entry.Png) is var (w, h) ? $"{w}×{h}" : "";
        var head = EditorStyle.Row(8);
        var title = new Label { Text = entry.Borg1 is null ? entry.Name : $"Texture {entry.Label}" };
        title.AddThemeFontSizeOverride("font_size", 15);
        head.AddChild(title);
        head.AddChild(EditorStyle.DimLabel(size));
        if (entry.Override is { } over)
            head.AddChild(EditorStyle.Chip($"{(over.Reach == TextureReach.Model ? "this model" : "everywhere")} · {over.Plugin}", over.Path, changed: true));
        _details.AddChild(head);

        _details.AddChild(Wrapped(entry.Override is { } r
            ? r.Reach == TextureReach.Model ? $"{r.Plugin} gives this model its own PNG, in place of the copy in its file."
                : $"{r.Plugin} replaces this texture on every model that uses it."
            : entry.Borg1 is null ? "The model's own image. It has no texture number, so a texture PNG can't replace it: edit the model to change it."
            : "The model draws the copy in its file."));

        if (entry.Borg1 is { } borg1)
        {
            _details.AddChild(Wrapped(Others(borg1)));
            if (_usage is { } usage && usage.ModelsUsing(borg1).Where(m => m != _ctx.Scene).ToList() is { Count: > 0 } others)
                _details.AddChild(Shared(others));
        }

        var buttons = new HFlowContainer();
        buttons.AddThemeConstantOverride("h_separation", 6);
        buttons.AddThemeConstantOverride("v_separation", 6);
        var export = new Button { Text = "Export PNG…", TooltipText = "Save the texture as the model draws it" };
        export.Pressed += () => Export(entry);
        buttons.AddChild(export);
        if (entry.Borg1 is { } texture && !_ctx.Original)
        {
            var here = new Button { Text = "Replace on this model…", TooltipText = "A PNG of yours on this model alone, written into a plugin" };
            here.Pressed += () => Replace(RecordId.ModelTexture(_ctx.Scene, texture), $"texture {entry.Label} on this model");
            buttons.AddChild(here);
            var everywhere = new Button { Text = "Replace everywhere…", TooltipText = "A PNG of yours on every model that uses this texture" };
            everywhere.Pressed += () => Replace(RecordId.Hex(RecordTypes.Texture, texture), $"texture {entry.Label}, everywhere");
            buttons.AddChild(everywhere);
            if (entry.Override is { } put && put.Plugin != RecordId.RomNamespace)
            {
                var back = new Button { Text = "Put back", TooltipText = $"Take {put.Plugin}'s PNG out, so the {(put.Reach == TextureReach.Model ? "texture everywhere, or the model's copy," : "model's copy")} shows again" };
                var id = put.Reach == TextureReach.Model ? RecordId.ModelTexture(_ctx.Scene, texture) : RecordId.Hex(RecordTypes.Texture, texture);
                back.Pressed += () => PluginFiles.Remove(this, _ctx.Plugins, put.Plugin, id, "png", $"texture {entry.Label}'s PNG", _ctx.Reload);
                buttons.AddChild(back);
            }
        }
        _details.AddChild(buttons);
    }

    /// <summary>The other models that use a texture, by who they are, each a link, the first few and a menu of all.</summary>
    private Control Shared(IReadOnlyList<RecordId> models)
    {
        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 4);
        string Name(RecordId m)
        {
            var summary = _ctx.Catalog.Find(m)?.Summary ?? "";
            var cut = summary.IndexOf(", used by", StringComparison.Ordinal);
            var who = cut > 0 ? summary[..cut] : "";
            return who.Length > 0 ? $"{who} ({m.Key})" : m.Key;
        }
        foreach (var m in models.Take(5))
        {
            var link = new LinkButton { Text = Name(m), TooltipText = $"Open {m}", Underline = LinkButton.UnderlineMode.OnHover };
            link.Pressed += () => _ctx.Open(m);
            flow.AddChild(link);
        }
        if (models.Count > 5)
        {
            var more = new MenuButton { Text = $"and {models.Count - 5} more ▾", Flat = true };
            var popup = more.GetPopup();
            for (var i = 0; i < models.Count; i++)
                popup.AddItem(Name(models[i]), i);
            popup.IdPressed += i => _ctx.Open(models[(int)i]);
            flow.AddChild(more);
        }
        return flow;
    }

    private void Export(Entry entry) =>
        PluginFiles.Save(this, "Save the texture as a PNG", $"{entry.Label}.png", "*.png ; PNG images", path =>
        {
            try
            {
                System.IO.File.WriteAllBytes(path, Bytes(entry));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                PluginFiles.Tell(this, "Couldn't save it", e.Message);
            }
        });

    private void ExportAll() =>
        PluginFiles.Folder(this, "The folder to save the model's textures in", folder =>
        {
            try
            {
                foreach (var entry in _entries)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, string.Join("_", entry.Label.Split(System.IO.Path.GetInvalidFileNameChars())) + ".png"), Bytes(entry));
                PluginFiles.Tell(this, "Saved", $"{_entries.Count} textures saved in {folder}.");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                PluginFiles.Tell(this, "Couldn't save them", e.Message);
            }
        });

    /// <summary>The texture's PNG as the model draws it.</summary>
    private static byte[] Bytes(Entry entry) => entry.Override is { } o ? System.IO.File.ReadAllBytes(o.Path) : entry.Png;

    /// <summary>Picks a PNG and writes it into a plugin as <paramref name="id"/>'s asset, then loads the plugins again.</summary>
    private void Replace(RecordId id, string what) =>
        PluginFiles.Open(this, $"A PNG for {what}", "*.png ; PNG images", path =>
        {
            byte[] bytes;
            try
            {
                bytes = System.IO.File.ReadAllBytes(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                PluginFiles.Tell(this, "Couldn't read it", e.Message);
                return;
            }
            if (PngHeader.Size(bytes) is null)
            {
                PluginFiles.Tell(this, "Not a PNG", $"{System.IO.Path.GetFileName(path)} isn't a PNG image.");
                return;
            }
            PluginFiles.Write(this, _ctx.Plugins, what, folder => folder.WriteAsset(id, "png", bytes), _ctx.Reload);
        });

    private static Label Wrapped(string text) => new() { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(300, 0) };

    private static StyleBoxFlat Frame(Color edge) => new()
    {
        BgColor = EditorTheme.Current.Field, BorderColor = edge, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
        CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 2,
    };
}
