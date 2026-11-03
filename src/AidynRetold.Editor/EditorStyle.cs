using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// What the editor's pages share of its look (<see cref="EditorTheme"/>): dim secondary text, a plugin's chip, rows with even spacing.
/// Labels and chips take the theme's type variations, so a change of mode reaches them; the colours here are the mode's, for what
/// code draws itself.
/// </summary>
public static class EditorStyle
{
    /// <summary>Text that says what something is rather than what it's called: IDs, counts, notes.</summary>
    public static Color Dim => EditorTheme.Current.Dim;

    /// <summary>What's to be fixed: a check that failed, a file that couldn't be written.</summary>
    public static Color Problem => EditorTheme.Current.Problem;

    /// <summary>What's well: a check that passed.</summary>
    public static Color Good => EditorTheme.Current.Good;

    /// <summary>What a plugin other than the ROM plugin changed: a patched field, a replaced file.</summary>
    public static Color Changed => EditorTheme.Current.Changed;

    /// <summary>A label in the theme's dim text, at <paramref name="size"/> if given.</summary>
    public static Label DimLabel(string text = "", int? size = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = "DimLabel" };
        if (size is { } s)
            label.AddThemeFontSizeOverride("font_size", s);
        return label;
    }

    /// <summary>A small rounded tag: a plugin's ID, a count, a state; edged and lettered in the changed colour for what a plugin changed.</summary>
    public static PanelContainer Chip(string text, string tooltip = "", bool changed = false)
    {
        var panel = new PanelContainer
        {
            TooltipText = tooltip, MouseFilter = Control.MouseFilterEnum.Pass, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            ThemeTypeVariation = changed ? "ChangedChip" : "Chip",
        };
        panel.AddChild(new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Pass, ThemeTypeVariation = changed ? "ChangedChipLabel" : "ChipLabel" });
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
