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

    /// <summary>
    /// The inventory's grid, the user's design (Nov 2, 2026): a cell 18 square for each of the list's rows, a stack or a worn item,
    /// 8 to a line, filling the list's frame under the tabs with 9 lines, and scrolling a line at a time past them.
    /// </summary>
    public static readonly GridLayout InventoryGrid = new(new ScreenRect(161, 46, 305, 208), 18, 8);

    /// <summary>
    /// Whether the inventory is the grid (<see cref="InventoryGrid"/>) rather than a list: the Party tab's own, which the pause menu
    /// sets; the shops' and the loot's lists of the party's items stay lists, as the stick's left and right go between them and the pile.
    /// On the grid, up and down move the cursor a line, and left and right along it and off its ends onto the tabs beside.
    /// </summary>
    public bool Grid
    {
        get;
        set
        {
            field = value;
            if (value)
                InventoryGrid.Apply(Inventory);
            else
                (Inventory.Columns, Inventory.Visible) = (1, Layout(PartyList.Inventory).Visible);
        }
    }

    /// <summary>
    /// Whether the inventory shows what the party wears, with the wearer's portrait, as the original's list does: the user's setting
    /// (<see cref="Settings.WornInInventory"/>), on by default with a controller, so worn items are in the stick's reach, and off with
    /// the keyboard and mouse, which take them off from the silhouette (<see cref="AimAtSlot"/>). The pause menu sets it.
    /// </summary>
    public bool ShowWorn
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Refresh();
        }
    } = true;

    /// <summary>The silhouette's slot a click aimed the next A at, or null for the cursor's row.</summary>
    private EquipmentSlot? _slotAim;

    /// <summary>
    /// Aims the next A at what the member shown wears in a slot of the silhouette, as a click on the slot does before it presses A,
    /// so A takes it off as it would the worn item's row; or null to aim A at the cursor's row again.
    /// </summary>
    public void AimAtSlot(EquipmentSlot? slot) => _slotAim = slot;
}
