using AidynRetold.Core.Characters;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// The little dialog that changes the colour behind a member's portrait, the user's design (Nov 9, 2026): the Party tab opens it for a
/// right click on a portrait that has a <see cref="PortraitLook"/>. The stick's left and right, or its arrows, step through the
/// backgrounds and wrap, or a swatch picks one, each taking effect at once on the member, which a save keeps. A or B closes it.
/// </summary>
public sealed class PortraitPicker
{
    private readonly PortraitLook _look;

    public PortraitPicker(Character member, PortraitLook look)
    {
        Member = member;
        _look = look;
        Selected = member.PortraitBackground is { } chosen ? Math.Max(0, IndexOf(chosen)) : 0;
    }

    public Character Member { get; }

    /// <summary>The backgrounds to choose from.</summary>
    public IReadOnlyList<RecordId> Backgrounds => _look.Backgrounds;

    /// <summary>Which of the backgrounds is the member's.</summary>
    public int Selected { get; private set; }

    /// <summary>The next background, or with a negative step the one before, round from the last to the first.</summary>
    public void Step(int by)
    {
        if (Backgrounds.Count == 0)
            return;
        Selected = ((Selected + by) % Backgrounds.Count + Backgrounds.Count) % Backgrounds.Count;
        Member.PortraitBackground = Backgrounds[Selected];
    }

    /// <summary>Stands just before a background, so that the press a swatch's click sends on steps onto it, with the arrows' sound.</summary>
    public void AimAt(int background) =>
        Selected = Backgrounds.Count == 0 ? 0 : ((background - 1) % Backgrounds.Count + Backgrounds.Count) % Backgrounds.Count;

    private int IndexOf(RecordId background)
    {
        for (var i = 0; i < Backgrounds.Count; i++)
            if (Backgrounds[i] == background)
                return i;
        return -1;
    }
}
