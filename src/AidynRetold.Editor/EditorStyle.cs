using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// The editor's own look, for what its pages share: dim secondary text, a plugin's chip, a section that folds, and a toolbar row
/// with even spacing. Godot's default theme does the rest.
/// </summary>
public static class EditorStyle
{
    /// <summary>Text that says what something is rather than what it's called: IDs, counts, notes.</summary>
    public static readonly Color Dim = new(0.62f, 0.64f, 0.68f);

    /// <summary>What's to be fixed: a check that failed, a file that couldn't be written.</summary>
    public static readonly Color Problem = new(1f, 0.5f, 0.45f);

    /// <summary>What's well: a check that passed.</summary>
    public static readonly Color Good = new(0.55f, 0.85f, 0.55f);

    /// <summary>What a plugin other than the ROM plugin changed: a patched field, a replaced file.</summary>
    public static readonly Color Changed = new(1f, 0.75f, 0.4f);

    private static readonly Color ChipFill = new(0.24f, 0.26f, 0.31f), ChipEdge = new(0.34f, 0.37f, 0.44f);

    /// <summary>A label in <see cref="Dim"/>, at <paramref name="size"/> if given.</summary>
    public static Label DimLabel(string text = "", int? size = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeColorOverride("font_color", Dim);
        if (size is { } s)
            label.AddThemeFontSizeOverride("font_size", s);
        return label;
    }

    /// <summary>A small rounded tag: a plugin's ID, a count, a state.</summary>
    public static PanelContainer Chip(string text, string tooltip = "", Color? color = null)
    {
        var panel = new PanelContainer { TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = ChipFill, BorderColor = ChipEdge, BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
            ContentMarginLeft = 7, ContentMarginRight = 7, ContentMarginTop = 1, ContentMarginBottom = 1,
        });
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Pass };
        label.AddThemeFontSizeOverride("font_size", 13);
        if (color is { } c)
            label.AddThemeColorOverride("font_color", c);
        panel.AddChild(label);
        return panel;
    }

    /// <summary>A row of controls, evenly spaced, each centred on the row.</summary>
    public static HBoxContainer Row(int separation = 8)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    /// <summary>Space that takes what's left of a row, pushing what follows to its end.</summary>
    public static Control Spacer() => new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
}
