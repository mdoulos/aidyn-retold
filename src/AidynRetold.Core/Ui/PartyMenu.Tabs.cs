namespace AidynRetold.Core.Ui;

public sealed partial class PartyMenu
{
    /// <summary>
    /// The stats list's rows' pitch, as the original's has them, and under the rows, 4 below, the experience lines, 11 apart.
    /// </summary>
    public const int StatPitch = 11, ExperienceGap = 4, ExperiencePitch = 11;

    /// <summary>
    /// The port's Stats, Skills and Spells rows' pitch, the user's choice (Nov 6, 2026): text with capitals 3 tall about 2 apart, as dense
    /// as the equipment card's, where the rows were the original's 13 and 11 apart in the menus' text; the experience lines follow at it.
    /// The inventory's list, the shops' and the loot's, keeps the original's 13 for its icons.
    /// </summary>
    public const int ListPitch = 5;

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

    /// <summary>Where on the stats list, from its top, the experience lines are: 4 under its rows, at the look's pitch.</summary>
    public int ExperienceAt => Stats.Rows.Count * (OriginalLook ? StatPitch : ListPitch) + ExperienceGap;

    /// <summary>How far apart the experience lines are: the original's 11, or the port's rows' pitch.</summary>
    public int ExperienceRowPitch => OriginalLook ? ExperiencePitch : ListPitch;

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
    // From x 161 until Nov 6, 2026, then 1 to the right with everything under it, the user's eye.
    public static readonly GridLayout InventoryGrid = new(new ScreenRect(162, 46, 302, 206), 20, 7);

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
                CurrentGrid.Apply(Inventory);
            else
                (Inventory.Columns, Inventory.Visible) = (1, LayoutOf(PartyList.Inventory).Visible);
            // The grid's rows are the port's (GridRows), the list's the original's.
            Refresh();
        }
    }

    /// <summary>
    /// Whether the inventory list shows what the party wears, with the wearer's portrait, as the original's does: the original's look's
    /// list. The port's grid shows what's worn only with its Equipped filter (<see cref="Filter"/>), the silhouette being where it's
    /// reached, by the stick as by the mouse.
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

    /// <summary>How many items the party carries, every stack's count, which the grid's foot shows.</summary>
    public int CarriedCount => Party.Inventory.Stacks.Sum(s => s.Quantity);

    /// <summary>
    /// The silhouette's slot selected in place of the cursor's row, as a click on it selects it, the user's choice (Nov 2, 2026): A and
    /// C-down are then the worn item's there, as they'd be its row's, and moving the cursor, the tab or the member lets it go. In the
    /// port's look it's where the stick is on the silhouette (<see cref="Focus"/>), and may be empty, and only the stick leaving the
    /// silhouette or the tab changing lets it go. Null while the cursor's row is the selection.
    /// </summary>
    public EquipmentSlot? SelectedSlot
    {
        get;
        private set
        {
            field = value;
            SyncFitting();
        }
    }

    /// <summary>
    /// Selects a slot of the silhouette with something worn in it, as a click on it does; one with nothing in it, nothing, but in the
    /// port's look, where an empty slot is selected too.
    /// </summary>
    public void SelectSlot(EquipmentSlot slot)
    {
        if (Message is null && !DetailsPane.Showing && Crafting is null && (Spatial || Worn(slot) is not null))
            FocusSlot(slot);
    }
}
