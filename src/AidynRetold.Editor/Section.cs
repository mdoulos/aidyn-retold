using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A card that folds, as the inspector's sections and a record's page are made of: its title, with a count or note after it, in the
/// card's head over a line, and its content in the card's body, both padded as the theme's cards are (<c>Card</c>, <c>CardHead</c>,
/// <c>CardBody</c>). Clicking the title folds it to its head.
/// </summary>
public partial class Section : PanelContainer
{
    private readonly Button _title = new()
    {
        ThemeTypeVariation = "SectionTitle", Alignment = HorizontalAlignment.Left, ToggleMode = true, ButtonPressed = true, FocusMode = FocusModeEnum.None,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };
    private readonly Label _note = EditorStyle.DimLabel();
    private readonly PanelContainer _head = new() { ThemeTypeVariation = "CardHead" };
    private readonly PanelContainer _body = new() { ThemeTypeVariation = "CardBody" };
    private readonly string _name;

    /// <param name="foldable">Whether the title folds it; a page's card that's always open says no.</param>
    /// <param name="flush">
    /// Whether the content fills the body to the card's edges, unpadded: a table, which reads better spanning the card than inset in
    /// it, drawn as a <c>FlushTree</c>. What else such content holds pads itself.
    /// </param>
    /// <param name="fill">Whether, while it's open, it takes the height its column has left, and its content with it.</param>
    public Section(string name, Control content, bool foldable = true, bool flush = false, bool fill = false)
    {
        _name = name;
        Content = content;
        ThemeTypeVariation = "Card";
        if (fill)
        {
            SizeFlagsVertical = SizeFlags.ExpandFill;
            _body.SizeFlagsVertical = SizeFlags.ExpandFill;
            content.SizeFlagsVertical = SizeFlags.ExpandFill;
        }
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        // Folded, it's only its head, however much height there is.
        if (fill)
            Toggled += open => SizeFlagsVertical = open ? SizeFlags.ExpandFill : SizeFlags.Fill;
        var head = EditorStyle.Row(8);
        _title.Disabled = !foldable;
        head.AddChild(_title);
        head.AddChild(_note);
        _head.AddChild(head);
        column.AddChild(_head);
        if (flush)
            _body.ThemeTypeVariation = "CardBodyFlush";
        _body.AddChild(content);
        column.AddChild(_body);
        AddChild(column);
        _title.Toggled += open =>
        {
            _body.Visible = open;
            _head.ThemeTypeVariation = open ? "CardHead" : "CardHeadFolded";
            UpdateTitle();
            Toggled?.Invoke(open);
        };
        _foldable = foldable;
        UpdateTitle();
    }

    private readonly bool _foldable;

    public Control Content { get; }

    /// <summary>Its title, without the fold's arrow.</summary>
    public string Title => _name;

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

    private void UpdateTitle() => _title.Text = !_foldable ? _name : (_title.ButtonPressed ? "▾  " : "▸  ") + _name;
}
