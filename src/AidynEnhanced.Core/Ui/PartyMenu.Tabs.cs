namespace AidynEnhanced.Core.Ui;

public sealed partial class PartyMenu
{
    /// <summary>
    /// The stats list's rows' pitch, as the original's has them, and under the rows, 4 below, the experience lines, 11 apart.
    /// </summary>
    public const int StatPitch = 11, ExperienceGap = 4, ExperiencePitch = 11;

    /// <summary>
    /// The tabs over the list, left to right, the user's design (Nov 2, 2026): the inventory, the stats, the skills and the spells, a
    /// tab for each of the original's four lists. The stick steps through them as it did the lists, which is this order wrapping round.
    /// </summary>
    public static readonly IReadOnlyList<PartyList> Tabs = [PartyList.Inventory, PartyList.Stats, PartyList.Skills, PartyList.Spells];

    /// <summary>Which of <see cref="Tabs"/> shows a list.</summary>
    public static int TabOf(PartyList list) => Tabs.ToList().IndexOf(list);

    /// <summary>
    /// The stick's way, 1 right or −1 left, that a click on one of <see cref="Tabs"/> presses to reach it from another, toward it,
    /// so the lists slide its way, or 0 for the same tab. The press goes to the tab clicked (<see cref="AimAt"/>).
    /// </summary>
    public static int StepToTab(int from, int to) => Math.Sign(to - from);

    /// <summary>Where on the stats list, from its top, the experience lines are: 4 under its rows.</summary>
    public int ExperienceAt => Stats.Rows.Count * StatPitch + ExperienceGap;

    /// <summary>The tab a click aimed the next stick press at, or null for the next or the one before.</summary>
    private PartyList? _aim;

    /// <summary>
    /// Aims the stick's next press at a tab, as a click on it does before it presses the stick its way, so the press goes there,
    /// however far, sliding and sounding as the stick's does; or null to aim it at the next or the one before again.
    /// </summary>
    public void AimAt(PartyList? tab) => _aim = tab;
}
