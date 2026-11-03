using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A section of an inspector that folds: its title, with a count or note after it, over its content. Clicking the title folds it.
/// </summary>
public partial class Section : VBoxContainer
{
    private readonly Button _title = new() { Flat = true, Alignment = HorizontalAlignment.Left, ToggleMode = true, ButtonPressed = true };
    private readonly Label _note = EditorStyle.DimLabel(size: 13);
    private readonly string _name;

    public Section(string name, Control content)
    {
        _name = name;
        Content = content;
        AddThemeConstantOverride("separation", 4);
        var head = EditorStyle.Row(6);
        _title.AddThemeFontSizeOverride("font_size", 15);
        _title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(_title);
        head.AddChild(_note);
        AddChild(head);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddChild(content);
        AddChild(margin);
        AddChild(new HSeparator());
        _title.Toggled += open =>
        {
            margin.Visible = open;
            UpdateTitle();
            Toggled?.Invoke(open);
        };
        UpdateTitle();
    }

    public Control Content { get; }

    /// <summary>The section was folded or unfolded.</summary>
    public event Action<bool>? Toggled;

    /// <summary>What follows the title, dim: a count, where it comes from.</summary>
    public string Note
    {
        get => _note.Text;
        set => _note.Text = value;
    }

    public bool Open
    {
        get => _title.ButtonPressed;
        set => _title.ButtonPressed = value;
    }

    private void UpdateTitle() => _title.Text = (_title.ButtonPressed ? "▾ " : "▸ ") + _name;
}
