using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A card's buttons: two to a row, or as many as asked, each an equal share of it, so the row is full; one alone, or the last few of
/// a row that isn't full, centred at their own widths. A share too narrow for its words trims them, and its tooltip says what it does.
/// </summary>
public partial class ButtonRows : VBoxContainer
{
    public ButtonRows(params Button[] buttons)
        : this(2, buttons)
    {
    }

    /// <param name="across">How many buttons stand in a full row.</param>
    public ButtonRows(int across, params Button[] buttons)
    {
        AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < buttons.Length; i += across)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 8);
            var count = Math.Min(across, buttons.Length - i);
            foreach (var button in buttons.Skip(i).Take(count))
            {
                // Shares: each expands from the same minimum, its words trimmed rather than widening it, so they split the row evenly.
                if (count == across && across > 1)
                {
                    button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    button.ClipText = true;
                    button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    if (string.IsNullOrEmpty(button.TooltipText))
                        button.TooltipText = button.Text;
                }
                row.AddChild(button);
            }
            AddChild(row);
        }
    }
}
