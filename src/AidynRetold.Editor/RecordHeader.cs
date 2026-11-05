using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The record page's header, one row: the record's name, large; its ID and Borg, dim; one chip, the last plugin that touches it: grey
/// for the ROM plugin's record as it is, green for one a plugin added, and the changed colour for one a plugin patches or whose file it
/// replaces, whichever plugin defined it (the whole chain is the chip's tooltip); and the page's tabs, at the row's end. Clicking the
/// ID copies it.
/// </summary>
public partial class RecordHeader : HBoxContainer
{
    private readonly Label _title = new()
    {
        SizeFlagsHorizontal = SizeFlags.ShrinkBegin, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, ClipText = true,
        VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Pass,
    };
    private readonly Label _id = EditorStyle.DimLabel(size: 13);
    private readonly HBoxContainer _sources = EditorStyle.Row(3);
    private string _copied = "";

    public RecordHeader()
    {
        AddThemeConstantOverride("separation", 10);
        _title.ThemeTypeVariation = "TitleLabel";
        AddChild(_title);
        _id.MouseFilter = MouseFilterEnum.Stop;
        _id.MouseDefaultCursorShape = CursorShape.PointingHand;
        _id.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _copied.Length == 0)
                return;
            DisplayServer.ClipboardSet(_copied);
            Toast.Show(this, $"Copied {_copied}.");
        };
        AddChild(_id);
        AddChild(_sources);
        AddChild(EditorStyle.Spacer());
        Tabs.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        AddChild(Tabs);
    }

    /// <summary>The page's tabs, which the browser keeps with its pages.</summary>
    public TabBar Tabs { get; } = new() { ClipTabs = false };

    /// <summary>A plugin the record or its files come from, and what it gives.</summary>
    public readonly record struct Source(string Plugin, string Gives);

    /// <summary>Shows a record: <paramref name="title"/> its name or summary, which may be long (the whole is its tooltip).</summary>
    public void Show(string title, string id, IReadOnlyList<Source> sources)
    {
        _title.Text = title.ReplaceLineEndings(" ");
        _title.TooltipText = title;
        // A long title gives up its room before the tabs do.
        _title.CustomMinimumSize = new Vector2(Math.Min(
            _title.GetThemeFont("font").GetStringSize(_title.Text, fontSize: _title.GetThemeFontSize("font_size")).X + 2, 560), 0);
        _id.Text = id;
        // The ID alone, without the Borg or the object's place that follow it.
        _copied = id.Split(" · ")[0];
        _id.TooltipText = _copied.Length == 0 ? "" : $"{_copied}: click to copy it.";
        foreach (var child in _sources.GetChildren())
        {
            _sources.RemoveChild(child);
            child.QueueFree();
        }
        if (sources.Count == 0)
            return;
        // The last plugin's chip alone: the chain, last first, is its tooltip.
        var (plugin, _) = sources[^1];
        var chain = string.Join("\n", sources.Reverse().Select(s => $"{EditorStyle.PluginName(s.Plugin)} ({s.Plugin}): {s.Gives}"));
        var changed = sources.Count > 1;
        var added = !changed && plugin != RecordId.RomNamespace;
        _sources.AddChild(EditorStyle.Chip(EditorStyle.PluginName(plugin), chain, changed, added));
    }
}
