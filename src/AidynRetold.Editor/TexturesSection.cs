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

        // Where it comes from, when that's other than the copy in the model's file, which a texture is unless said.
        if (entry.Override is { } r)
            _details.AddChild(Wrapped(r.Reach == TextureReach.Model ? $"{r.Plugin} gives this model its own PNG, in place of the copy in its file."
                : $"{r.Plugin} replaces this texture on every model that uses it."));
        else if (entry.Borg1 is null)
            _details.AddChild(Wrapped("The model's own image. It has no texture number, so a texture PNG can't replace it: edit the model to change it."));

        if (entry.Borg1 is { } borg1)
        {
            _details.AddChild(Wrapped(Others(borg1)));
            if (_usage is { } usage && usage.ModelsUsing(borg1).Where(m => m != _ctx.Scene).ToList() is { Count: > 0 } others)
                _details.AddChild(Shared(others));
        }

        // Two buttons, each asking which: export this texture or all the model's, replace it on this model or on every one.
        var buttons = new List<Button>();
        var export = new Button { Text = "Export Texture", TooltipText = "Save the texture, or all the model's, as PNGs" };
        export.Pressed += () => AskExport(entry);
        buttons.Add(export);
        if (entry.Borg1 is { } texture && !_ctx.Original)
        {
            var replace = new Button { Text = "Replace Texture", TooltipText = "A PNG of yours in its place, on this model or on every model that uses it" };
            replace.Pressed += () => AskReplace(entry, texture);
            buttons.Add(replace);
            if (entry.Override is { } put && put.Plugin != RecordId.RomNamespace)
            {
                var back = new Button { Text = "Put Back Texture", TooltipText = $"Take {put.Plugin}'s PNG out, so the {(put.Reach == TextureReach.Model ? "texture everywhere, or the model's copy," : "model's copy")} shows again" };
                var id = put.Reach == TextureReach.Model ? RecordId.ModelTexture(_ctx.Scene, texture) : RecordId.Hex(RecordTypes.Texture, texture);
                back.Pressed += () => PluginFiles.Remove(this, _ctx.Plugins, put.Plugin, id, "png", $"texture {entry.Label}'s PNG", _ctx.Reload);
                buttons.Add(back);
            }
        }
        _details.AddChild(new ButtonRows([.. buttons]));
    }

    /// <summary>
    /// The other models that use a texture, a list of their own in a box under the count: a row each, as a link, who wears it with
    /// the model's key dim at the right, or the model by its key where no one does, double-clicked to open; five rows show, the
    /// rest scroll.
    /// </summary>
    private Control Shared(IReadOnlyList<RecordId> models)
    {
        var list = new Tree { ThemeTypeVariation = "FlushTree", Columns = 2, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row };
        list.SetColumnExpand(0, true);
        list.SetColumnClipContent(0, true);
        list.SetColumnExpand(1, false);
        list.SetColumnCustomMinimumWidth(1, 80);
        var root = list.CreateItem();
        foreach (var m in models)
        {
            var summary = _ctx.Catalog.Find(m)?.Summary ?? "";
            var cut = summary.IndexOf(", used by", StringComparison.Ordinal);
            var who = cut > 0 ? summary[..cut] : "";
            var row = list.CreateItem(root);
            row.SetText(0, who.Length > 0 ? who : $"Model {m.Key}");
            row.SetCustomColor(0, RecordFieldsView.LinkColor);
            row.SetTooltipText(0, $"{(who.Length > 0 ? $"{who}: " : "")}{m}. Double-click to open.");
            row.SetText(1, who.Length > 0 ? m.Key : "");
            row.SetCustomColor(1, EditorStyle.Dim);
            row.SetTextAlignment(1, HorizontalAlignment.Right);
            row.SetTooltipText(1, $"{m}. Double-click to open.");
            row.SetMetadata(0, m.ToString());
        }
        list.ItemActivated += () =>
        {
            if (list.GetSelected()?.GetMetadata(0).AsString() is { } text && RecordId.TryParse(text, out var id))
                _ctx.Open(id);
        };
        // In a box of its own, as high as its rows, so the last ends on the box's border with no rule or space under it.
        var box = new PanelContainer { ThemeTypeVariation = "ListBox" };
        box.AddChild(new FittedTable(list, 5, inBox: true));
        return box;
    }

    /// <summary>Export: this texture, or every texture the model draws.</summary>
    private void AskExport(Entry entry)
    {
        if (_entries.Count == 1)
        {
            Export(entry);
            return;
        }
        new EditorDialog("Export textures",
            EditorDialog.Paragraphs($"Export texture {entry.Label} alone, or all {_entries.Count} of the model's textures, as PNGs named for their numbers?"),
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("All Textures", DialogButtonKind.Secondary, ExportAll),
                new DialogButton("This Texture", DialogButtonKind.Primary, () => Export(entry)),
            ]).Open(this);
    }

    /// <summary>Replace: on this model alone, or on every model that uses the texture.</summary>
    private void AskReplace(Entry entry, int texture)
    {
        var others = _usage?.ModelsUsing(texture).Count(m => m != _ctx.Scene);
        var reach = others switch
        {
            null => "every model that uses it",
            0 => "every model that uses it (no other model does now)",
            1 => "every model that uses it (one other does)",
            _ => $"every model that uses it ({others} others do)",
        };
        new EditorDialog("Replace the texture",
            EditorDialog.Paragraphs($"Put a PNG of yours in the place of texture {entry.Label} on this model alone, or on {reach}? It's written into your mod."),
            [
                new DialogButton("Cancel", DialogButtonKind.Cancel),
                new DialogButton("Every Model", DialogButtonKind.Secondary, () => Replace(RecordId.Hex(RecordTypes.Texture, texture), $"texture {entry.Label} for every model")),
                new DialogButton("This Model", DialogButtonKind.Primary, () => Replace(RecordId.ModelTexture(_ctx.Scene, texture), $"texture {entry.Label} on this model")),
            ]).Open(this);
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
