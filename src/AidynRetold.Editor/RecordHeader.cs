using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The record page's header, one row: the record's name, large; its ID and Borg, dim; a chip for each plugin it comes from, in load
/// order, the definer's first and each patcher's or replacing file's after an arrow; and the page's tabs, at the row's end.
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

    public RecordHeader()
    {
        AddThemeConstantOverride("separation", 10);
        _title.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_title);
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
        _title.CustomMinimumSize = new Vector2(Math.Min(_title.GetThemeFont("font").GetStringSize(_title.Text, fontSize: 20).X, 520), 0);
        _id.Text = id;
        foreach (var child in _sources.GetChildren())
            child.QueueFree();
        for (var i = 0; i < sources.Count; i++)
        {
            if (i > 0)
                _sources.AddChild(EditorStyle.DimLabel("›"));
            var (plugin, gives) = sources[i];
            _sources.AddChild(EditorStyle.Chip(plugin, $"{plugin}: {gives}", i > 0 ? EditorStyle.Changed : null));
        }
    }
}
