using AidynRetold.Core.Characters;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// The little dialog that changes a member's portrait, the user's design (Nov 9, 2026): the Party tab opens it for a right click on a
/// portrait that has a <see cref="PortraitLook"/>. The stick's left and right, or its arrows, step through the backgrounds and wrap, or a
/// swatch picks one. Where the portrait has overlays to wear, each is a row under the colours, which the stick's up and down reach: A or
/// the stick's left or right puts it on or takes it off, or a click on it does, each on its own. All of it takes effect at once on the
/// member, which a save keeps. B closes it, and so does A on the colours.
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

    /// <summary>What it can wear.</summary>
    public IReadOnlyList<PortraitOverlay> Overlays => _look.Overlays ?? [];

    /// <summary>Which row the stick is on: 0 the colours, and from 1 the overlays.</summary>
    public int Row { get; private set; }

    /// <summary>Whether the stick is on an overlay's row.</summary>
    public bool OnOverlay => Row > 0;

    /// <summary>Whether it's wearing an overlay.</summary>
    public bool Wears(int overlay) => _look.Worn(Member.PortraitWorn).Contains(Overlays[overlay].Image);

    /// <summary>Moves between the colours and the overlays, round from the last to the first.</summary>
    public void MoveRow(int by) => Row = ((Row + by) % (Overlays.Count + 1) + Overlays.Count + 1) % (Overlays.Count + 1);

    /// <summary>Puts the stick on the colours, for an arrow's click.</summary>
    public void FocusColors() => Row = 0;

    /// <summary>Puts the stick on an overlay's row, so that the press a click sends on acts on it.</summary>
    public void AimAtOverlay(int overlay) => Row = overlay + 1;

    /// <summary>Puts on the overlay the stick is on, or takes it off.</summary>
    public void Toggle()
    {
        if (!OnOverlay)
            return;
        var image = Overlays[Row - 1].Image;
        var worn = _look.Worn(Member.PortraitWorn);
        Member.PortraitWorn = worn.Contains(image) ? [.. worn.Where(w => w != image)] : [.. worn, image];
    }

    /// <summary>On the colours, the next background, or with a negative step the one before, round from the last to the first; on an overlay, puts it on or takes it off.</summary>
    public void Step(int by)
    {
        if (OnOverlay)
        {
            Toggle();
            return;
        }
        if (Backgrounds.Count == 0)
            return;
        Selected = ((Selected + by) % Backgrounds.Count + Backgrounds.Count) % Backgrounds.Count;
        Member.PortraitBackground = Backgrounds[Selected];
    }

    /// <summary>Stands just before a background, so that the press a swatch's click sends on steps onto it, with the arrows' sound.</summary>
    public void AimAt(int background)
    {
        Row = 0;
        Selected = Backgrounds.Count == 0 ? 0 : ((background - 1) % Backgrounds.Count + Backgrounds.Count) % Backgrounds.Count;
    }

    private int IndexOf(RecordId background)
    {
        for (var i = 0; i < Backgrounds.Count; i++)
            if (Backgrounds[i] == background)
                return i;
        return -1;
    }
}
