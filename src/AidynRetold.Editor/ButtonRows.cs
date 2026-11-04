using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A card's buttons: two to a row, each exactly half of it, so the row is full; one alone, or the last of an odd number, centred at
/// its own width. A half too narrow for its words trims them, and its tooltip says what it does.
/// </summary>
public partial class ButtonRows : VBoxContainer
{
    public ButtonRows(params Button[] buttons)
    {
        AddThemeConstantOverride("separation", 8);
        for (var i = 0; i < buttons.Length; i += 2)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 8);
            if (i + 1 < buttons.Length)
            {
                // Halves: each expands from the same minimum, its words trimmed rather than widening it, so the two split the row evenly.
                foreach (var half in new[] { buttons[i], buttons[i + 1] })
                {
                    half.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    half.ClipText = true;
                    half.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
                    if (string.IsNullOrEmpty(half.TooltipText))
                        half.TooltipText = half.Text;
                    row.AddChild(half);
                }
            }
            else
            {
                row.AddChild(buttons[i]);
            }
            AddChild(row);
        }
    }
}
