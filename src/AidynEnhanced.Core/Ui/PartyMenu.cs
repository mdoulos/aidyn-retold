using AidynEnhanced.Core.Characters;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Core.Rng;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>The Party tab's lists, in the order the stick steps through them (<c>DollMenuLists::InitMenu</c>).</summary>
public enum PartyList
{
    Inventory,
    Spells,
    Skills,
    Stats,
}

/// <summary>
/// The pause menu's Party tab (<c>WidgetDollMenu</c>): the member shown, with their portrait, hit
/// points, level and the party's gold, what they wear on the silhouette, and one of four lists with
/// a cursor, whose row C-down describes, the same for the menu's reuses in barter and training. The
/// port lays it out with tabs over its lists, the user's design (Oct 31 and Nov 2, 2026): the inventory, the
/// stats, the skills and the spells (<c>PartyMenu.Tabs.cs</c>).
/// </summary>
/// <remarks>
/// C-left and C-right change the member (<c>CHorizontal</c>), and the stick changes the tab, as the
/// original's changes the list (<c>DollMenuLists::LRToggle</c>). Changing either keeps each list's cursor, as the original
/// rebuilds the lists in place. Both are animated (<c>PartyMenu.Motion.cs</c>), and the details and the skills'
/// popups fade (<see cref="PopupFade"/>).
/// </remarks>
public sealed partial class PartyMenu : IPartyWindow
{
    /// <summary>
    /// The lists' bounds in the 320 × 240 frame: under the tabs and their arrows, and as far down as 12 rows of 13 reach, across the
    /// column the user drew for them. The original's are at (160, 101), bounded to x 300 and y 205 (<c>DollMenuLists</c>).
    /// </summary>
    public static readonly ScreenRect ListBounds = new(160, 56, 306, 212);

    /// <summary>
    /// The original's list's bounds (<c>DollMenuLists</c>), under its title: x 160 to 300, y 101 to 205, eight rows of 13, which the
    /// original look has (<see cref="OriginalLook"/>).
    /// </summary>
    public static readonly ScreenRect OriginalList = new(160, 101, 300, 205);

    /// <summary>
    /// Whether the tab is laid out as the original's, Graphic Quality's Original (<see cref="Settings.OriginalLook"/>), the user's choice
    /// (Nov 2, 2026): its lists where the original's are (<see cref="OriginalList"/>), sliding the original's 140.
    /// </summary>
    public bool OriginalLook
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            if (!Grid)
                Inventory.Visible = LayoutOf(PartyList.Inventory).Visible;
            Stats.Visible = LayoutOf(PartyList.Stats).Visible;
            Skills.Visible = LayoutOf(PartyList.Skills).Visible;
            Spells.Visible = LayoutOf(PartyList.Spells).Visible;
            Refresh();
        }
    }

    /// <summary>A list's layout as the tab is laid out: the port's, or the original's (<see cref="OriginalLook"/>).</summary>
    public ListLayout LayoutOf(PartyList list) => OriginalLook ? new(OriginalList, list == PartyList.Stats ? StatPitch : 13) : Layout(list);

    /// <summary>How far a list slides as the stick changes it: across the port's list's frame, or the original's 140.</summary>
    public int SlideDistance => OriginalLook ? OriginalListDistance : ListDistance;

    /// <summary>A list's place and its rows' pitch: 11 for the stats, 13 for the rest, so the lists show 12 rows.</summary>
    public static ListLayout Layout(PartyList list) => new(ListBounds, list == PartyList.Stats ? StatPitch : 13);

    /// <summary>The colour of a row, and of the header's numbers.</summary>
    public static readonly Rgba TextColor = MenuDetailsBuilder.TextColor;

    /// <summary>What the cursor's row pulses toward, from <see cref="HighlightFrom"/>, over <see cref="MenuPulse.Frames"/>.</summary>
    public static readonly Rgba HighlightColor = new(151, 141, 191, 255);

    /// <summary>The colour the cursor's row pulses from.</summary>
    public static readonly Rgba HighlightFrom = new(68, 42, 34, 255);

    private readonly MenuText _text;
    private readonly Func<RecordId, SpellRecord?> _spells;
    private readonly Func<RecordId, RecordId?> _portraits;
    private readonly MenuPulse _pulse = new();
    private readonly FrameGate _frame = new(Cadence.MenuFrameTicks);

    /// <param name="portraits">Each entity's portrait.</param>
    public PartyMenu(Party party, MenuText text, Func<RecordId, SpellRecord?> spells, Func<RecordId, RecordId?> portraits, int member = 0)
    {
        Party = party;
        _text = text;
        _spells = spells;
        _portraits = portraits;
        Member = Math.Clamp(member, 0, Party.MaxSize - 1);
        // The original steps back to a slot with a member (DollMenu::InitMenu).
        while (Member > 0 && party.Slots.ElementAtOrDefault(Member) is null)
            Member--;
        Inventory = Layout(PartyList.Inventory).List<InventoryRow>([]);
        Stats = Layout(PartyList.Stats).List<StatRow>([]);
        Skills = Layout(PartyList.Skills).List<SkillRow>([]);
        Spells = Layout(PartyList.Spells).List<SpellRow>([]);
        Refresh();
    }

    /// <param name="random">The stream skill checks and potions draw from; a fresh one if none.</param>
    public static PartyMenu From(PluginSet plugins, Party party, int member = 0, IRandom? random = null) => new(party, MenuText.From(plugins),
        id => plugins.TryGet<SpellRecord>(id), id => plugins.TryGet<EntityRecord>(id)?.Portrait, member)
    {
        Recipes = [.. plugins.All<RecipeRecord>(RecordTypes.Recipe).OrderBy(r => int.Parse(r.Id.Key, System.Globalization.CultureInfo.InvariantCulture))],
        Random = random ?? new Lcg(),
    };

    public Party Party { get; }

    /// <summary>The tab is its own header and lists.</summary>
    PartyMenu IPartyWindow.Menu => this;

    bool IPartyWindow.ChangesMember => true;

    /// <summary>
    /// The stick's left shows the next list and its right the one before (<see cref="ChangeList"/>); in the port's look it goes to what's
    /// that way on the tab (<see cref="Focus"/>), and on a list changes it as ever.
    /// </summary>
    void IPartyWindow.Sideways(int direction)
    {
        if (_aim is null && StepSpatially(direction, 0))
            return;
        ChangeList(-direction);
    }

    /// <summary>The party slot of the member shown (<c>gPartyPicker</c>).</summary>
    public int Member { get; private set; }

    public Character? Shown => Party.Slots.ElementAtOrDefault(Member);

    public MenuText Text => _text;

    public PartyList List { get; private set; } = PartyList.Inventory;

    public MenuList<InventoryRow> Inventory { get; }

    public MenuList<StatRow> Stats { get; }

    public MenuList<SkillRow> Skills { get; }

    public MenuList<SpellRow> Spells { get; }

    /// <summary>The lines under the stats list.</summary>
    public ExperienceLines? Experience { get; private set; }

    /// <summary>The party's slots with a member, from the one shown round, as the header's portraits go.</summary>
    public IReadOnlyList<int> PortraitOrder =>
    [
        .. Enumerable.Range(0, Party.MaxSize).Select(i => (Member + i) % Party.MaxSize).Where(i => Party.Slots.ElementAtOrDefault(i) is not null),
    ];

    /// <summary>An entity's portrait.</summary>
    public RecordId? Portrait(RecordId entity) => _portraits(entity);

    /// <summary>The share of the bar the member's hit points fill, of <paramref name="width"/> (<c>WidgetHealthGold</c>).</summary>
    public int HealthBar(int width) => HealthBar(Shown, width);

    /// <summary>The share of the bar a member's hit points fill.</summary>
    public static int HealthBar(Character? member, int width) => member is null ? 0 : HealthBar(member.Hp, member.MaxHp, width);

    /// <summary>The share of the bar <paramref name="hp"/> of <paramref name="maxHp"/> fills: hp ÷ max × width in single precision, truncated.</summary>
    public static int HealthBar(int hp, int maxHp, int width) => maxHp > 0 ? (int)((float)hp / maxHp * width) : 0;

    /// <summary>What a slot of the silhouette shows: the worn item's icon, or the slot's own image of what it takes.</summary>
    public RecordId SlotImage(EquipmentSlot slot) => ShownIcon(slot) ?? MenuImages.EmptySlots[(int)slot];

    /// <summary>
    /// The icon a slot of the silhouette shows for the member shown: what it wears, but in a battle the weapon's slot shows the flask
    /// or potion its combatant holds in hand (<c>DollEquipmentMenu::GetEntityItemId</c>'s weapon slot, <c>0x8003E66C</c>, which
    /// gives the Fire Flask's icon, <c>0x1000</c>, for any held; the port gives the held one's own, the same in the original game).
    /// </summary>
    private RecordId? ShownIcon(EquipmentSlot slot) =>
        slot == EquipmentSlot.Weapon && Shown is { } member && Combat?.Held?.Invoke(member) is { } held ? ItemIcon(held)
        : Worn(slot) is { } worn ? ItemIcon(worn.Item) : null;

    /// <summary>An item's icon as the menu draws it, a potion's or scroll's own picture in place of the shared one (<see cref="MenuImages.ItemIcon"/>).</summary>
    public RecordId ItemIcon(Item item) => MenuImages.ItemIcon(item, _spells);

    /// <summary>What a slot of the silhouette shows for a member.</summary>
    public static RecordId SlotImage(EquipmentSlot slot, Character? member) => Worn(slot, member)?.Item.Icon ?? MenuImages.EmptySlots[(int)slot];

    /// <summary>What the member shown wears in a slot.</summary>
    public ItemInstance? Worn(EquipmentSlot slot) => Worn(slot, Shown);

    /// <summary>
    /// What a member wears in a slot (<c>DollEquipmentMenu::GetEntityItemId</c>): a gear slot takes the first,
    /// second or third piece of its kind in the gear's order.
    /// </summary>
    public static ItemInstance? Worn(EquipmentSlot slot, Character? member)
    {
        if (member?.Equipment is not { } equipment)
            return null;
        return slot switch
        {
            EquipmentSlot.Weapon => equipment.Weapon,
            EquipmentSlot.Armor => equipment.Armor,
            EquipmentSlot.Shield => equipment.Shield,
            EquipmentSlot.Gloves => Nth(ItemKind.Gloves, 0),
            EquipmentSlot.Ring1 => Nth(ItemKind.Ring, 0),
            EquipmentSlot.Ring2 => Nth(ItemKind.Ring, 1),
            EquipmentSlot.Helmet => Nth(ItemKind.Helmet, 0),
            EquipmentSlot.Amulet => Nth(ItemKind.Amulet, 0),
            EquipmentSlot.Cloak => Nth(ItemKind.Cloak, 0),
            EquipmentSlot.Belt => Nth(ItemKind.Belt, 0),
            EquipmentSlot.Boots => Nth(ItemKind.Boots, 0),
            EquipmentSlot.Wand => Nth(ItemKind.Wand, 0),
            EquipmentSlot.Scroll1 => Nth(ItemKind.Scroll, 0),
            EquipmentSlot.Scroll2 => Nth(ItemKind.Scroll, 1),
            EquipmentSlot.Scroll3 => Nth(ItemKind.Scroll, 2),
            _ => null,
        };

        ItemInstance? Nth(ItemKind kind, int n) => equipment.Gear.OfType<ItemInstance>().Where(i => i.Item.Kind == kind).ElementAtOrDefault(n);
    }

    /// <summary>The list showing's cursor row, if it has any rows.</summary>
    public int Cursor => List switch
    {
        PartyList.Inventory => Inventory.Cursor,
        PartyList.Stats => Stats.Cursor,
        PartyList.Skills => Skills.Cursor,
        _ => Spells.Cursor,
    };

    /// <summary>
    /// Shows the next member, or with a negative step the one before: C-left shows the next and C-right the one before
    /// (<c>PartyMenu.OnCLeft</c> at <c>0x8003e1c8</c>, <c>OnCRight</c> <c>0x8003e1f4</c>). In the original's look, not while the
    /// header or the lists are still changing, nor while the numbers or the silhouette are after something done to the member shown;
    /// in the port's, those finish at once and the press is taken (<see cref="Settle"/>), and a slot selected stays so. A click on a
    /// small portrait aims the press at its member (<see cref="AimAtMember"/>), so it shows that one.
    /// </summary>
    public void ChangeMember(int by)
    {
        var aim = _memberAim;
        _memberAim = null;
        if (HasPopup || Changing && OriginalLook)
            return;
        var order = Enumerable.Range(0, Party.MaxSize).Where(i => Party.Slots.ElementAtOrDefault(i) is not null).ToList();
        if (order.Count < 2)
            return;
        Settle();
        var at = order.IndexOf(Member);
        if (!Spatial)
            SelectedSlot = null;
        _clicked = false;
        StartMemberChange();
        Member = aim is { } slot && order.Contains(slot) ? slot : order[((at + by) % order.Count + order.Count) % order.Count];
        Refresh();
    }

    /// <summary>The party slot a click aimed the next member change at, or null for the next or the one before.</summary>
    private int? _memberAim;

    /// <summary>
    /// Aims the next member change at a party slot, as a click on a small portrait does before it presses C-left, the user's choice
    /// (Nov 2, 2026), so the press shows that member, however far round, the portraits moving and sounding as C-left's do; or null to
    /// aim it at the next or the one before again.
    /// </summary>
    public void AimAtMember(int? slot) => _memberAim = slot;

    /// <summary>
    /// Shows the next list, or with a negative step the one before: the stick's left shows the next, sliding the lists
    /// right, and its right the one before, sliding them left (<c>PartyMenu.ListsOnLeft</c> at <c>0x800319f0</c>,
    /// <c>ListsOnRight</c> <c>0x80031a10</c>). A click on a tab aims the press at it (<see cref="AimAt"/>), so it shows that one,
    /// sliding the press's way. In the original's look not while they're still sliding; in the port's the slide finishes at once.
    /// </summary>
    /// <param name="among">The lists the window has, in their order, where it hasn't all four: the post-battle window's three.</param>
    public void ChangeList(int by, IReadOnlyList<PartyList>? among = null)
    {
        var aim = _aim;
        _aim = null;
        if (HasPopup || Slide is not null && OriginalLook)
            return;
        Slide = null;
        SelectedSlot = null;
        Picking = null;
        if (_focus == PartyFocus.Foot)
            _focus = PartyFocus.List;
        var lists = (among ?? Enum.GetValues<PartyList>()).ToList();
        var count = lists.Count;
        var at = Math.Max(0, lists.IndexOf(List));
        var next = aim is { } tab && lists.Contains(tab) ? tab : lists[((at + by) % count + count) % count];
        if (next == List)
            return;
        StartListSlide(by);
        List = next;
        SyncFitting();
    }

    /// <summary>Shows a list.</summary>
    public void ShowList(PartyList list)
    {
        List = list;
        SyncFitting();
    }

    /// <summary>Moves the cursor of the popup or the actiontip, or of the list showing.</summary>
    public void MoveCursor(int by)
    {
        if (Message is not null || DetailsPane.Showing)
            return;
        if (Tip is { } tip)
        {
            tip.Choices.Move(by);
            return;
        }
        if (Crafting is { } crafting)
        {
            if (crafting.Fade.IsOpen)
                crafting.Rows.Move(by);
            return;
        }
        if (StepSpatially(0, Math.Sign(by)))
            return;
        SelectedSlot = null;
        switch (List)
        {
            case PartyList.Inventory when Grid:
                if (GridLayout.Step(Inventory.Cursor, Inventory.Rows.Count, InventoryGrid.Columns, 0, Math.Sign(by)) is { } cell)
                    Inventory.Point(cell);
                break;
            case PartyList.Inventory: Inventory.Move(by); break;
            case PartyList.Stats: Stats.Move(by); break;
            case PartyList.Skills: Skills.Move(by); break;
            default: Spells.Move(by); break;
        }
    }

    /// <summary>
    /// Whether the player's last input was the mouse rather than a key or a controller (<see cref="IPauseMenuHost.Pointing"/>), which the
    /// pause menu sets as it lays the tab out.
    /// </summary>
    public bool Pointing { get; set; }

    /// <summary>Whether a click has put the grid's cursor on a cell since the member shown changed.</summary>
    private bool _clicked;

    /// <summary>
    /// Whether the grid's cursor shows, its red square and its item's name: always with a key or a controller, which move it; with the
    /// mouse only once a click has selected a cell, the user's choice (Nov 3, 2026), nothing being selected until then and the names
    /// following the mouse.
    /// </summary>
    public bool CursorShows => !Pointing || _clicked;

    /// <summary>
    /// The first press on the grid's hidden cursor from a key or a controller, which only shows it, as the stick, A, C-down or C-up would
    /// otherwise move from, or use, an item the player never saw selected. True if it was taken.
    /// </summary>
    public bool ShowCursor(MenuAction action)
    {
        if (CursorShows || List != PartyList.Inventory || !Grid || HasPopup || Focus != PartyFocus.List || Slide is not null
            || action is not (MenuAction.Up or MenuAction.Down or MenuAction.Left or MenuAction.Right or MenuAction.Accept
                or MenuAction.Details or MenuAction.Appraise))
            return false;
        Pointing = false;
        return true;
    }

    /// <summary>
    /// Scrolls the inventory's grid by lines, as the mouse's wheel does, rather than moving the cursor. A cursor a click put on a cell
    /// stays on its item; a hidden one goes with the view, so a key shows it where the player's looking.
    /// </summary>
    public void ScrollGrid(int lines)
    {
        if (Message is not null || DetailsPane.Showing || HasPopup || List != PartyList.Inventory || !Grid)
            return;
        Inventory.ScrollLines(lines);
        if (!_clicked)
            Inventory.Point(Inventory.First);
    }

    /// <summary>Puts the cursor of the popup, or of the list showing, on a row.</summary>
    public void Point(int row)
    {
        if (Message is not null || DetailsPane.Showing || Tip is not null)
            return;
        // A click on the grid of what fits the slot selected chooses for it.
        if (Spatial && List == PartyList.Inventory && SelectedSlot is { } fitting)
            Picking = fitting;
        SelectedSlot = null;
        if (Crafting is { } crafting)
        {
            if (crafting.Fade.IsOpen)
                crafting.Rows.Point(row);
            return;
        }
        _focus = PartyFocus.List;
        switch (List)
        {
            case PartyList.Inventory:
                Inventory.Point(row);
                _clicked |= Grid;
                break;
            case PartyList.Stats: Stats.Point(row); break;
            case PartyList.Skills: Skills.Point(row); break;
            default: Spells.Point(row); break;
        }
    }

    /// <summary>What C-down shows for the cursor's row, or a crafting popup's recipe's product, or null if it shows nothing.</summary>
    public MenuDetails? Details()
    {
        if (Shown is not { } shown)
            return null;
        if (Crafting is { } crafting)
        {
            // WidgetPotionCraft::CDownFunc and WidgetArmorCraft::CDownFunc; the Healer's popup has none.
            return crafting.Rows.Current?.Recipe is { } recipe && Party.Inventory.Catalog.TryGet(recipe.Product) is { } product
                ? MenuDetailsBuilder.Item(new ItemInstance(product), shown, _text, _spells, Party.Inventory.Catalog)
                : null;
        }
        if (SelectedSlot is { } selected && Worn(selected) is { } worn)
            return MenuDetailsBuilder.Item(worn, shown, _text, _spells, Party.Inventory.Catalog);
        return List switch
        {
            PartyList.Inventory when Inventory.Current is { } row =>
                MenuDetailsBuilder.Item(row.Instance, shown, _text, _spells, Party.Inventory.Catalog),
            PartyList.Stats when Stats.Current is { } row => MenuDetailsBuilder.Stat(shown.Rules[row.Stat]),
            PartyList.Skills when Skills.Current is { } row => MenuDetailsBuilder.Skill(shown.Rules[row.Skill]),
            PartyList.Spells when Spells.Current is { } row => MenuDetailsBuilder.Spell(row.Spell.Spell, _text, Party.Inventory.Catalog),
            _ => null,
        };
    }

    /// <summary>What C-down shows for an item, for the member shown.</summary>
    public MenuDetails? ItemDetails(ItemInstance item) =>
        Shown is { } shown ? MenuDetailsBuilder.Item(item, shown, _text, _spells, Party.Inventory.Catalog) : null;

    /// <summary>Rebuilds the rows from the party, as a member change or anything that changes the party does.</summary>
    public void Refresh()
    {
        var rows = PartyMenuRows.Inventory(Party, Member);
        _rowsFitting = Fitting;
        Inventory.Replace(Spatial ? GridRows(rows) : ShowWorn ? rows : [.. rows.Where(r => r.Wearer is null)], resetPastEnd: false);
        if (Shown is { } shown)
        {
            Stats.Replace(PartyMenuRows.Stats(shown), resetPastEnd: false);
            Skills.Replace(PartyMenuRows.Skills(shown), resetPastEnd: true);
            Spells.Replace(PartyMenuRows.Spells(shown), resetPastEnd: true);
            Experience = PartyMenuRows.Experience(shown, _text);
        }
        // In the port's look an empty slot can be selected, as the stick goes onto it.
        if (SelectedSlot is { } slot && Worn(slot) is null && !Spatial)
            SelectedSlot = null;
    }

    /// <summary>
    /// Steps the cursor's pulse, the member's and list's changes, and the popup's and details' fades, by a tick. The
    /// fades step once a menu update, a tick (<see cref="PopupFade"/>). The pulse steps twice a frame, as each
    /// of the original's lists steps its scroll menu twice (<c>PartyMenu.ListBaseStep</c>, <c>0x80030888</c>, ends by
    /// stepping the list's children, and the list's own step steps them again, as at <c>0x80035770</c>): 19 frames a
    /// cycle, 3.2 a second, which the user timed at about 3.4 (Oct 16, 2026), twice the other menus'.
    /// </summary>
    public void Step()
    {
        _pulse.Step();
        _pulse.Step();
        StepPopups();
        if (_frame.Tick())
            StepMotion();
    }

    /// <summary>
    /// The cursor's row's colour now: from <see cref="HighlightFrom"/> toward <see cref="HighlightColor"/>
    /// by whole steps, a 20th of the way rounded toward zero per frame (<c>WidgetFastScrollMenu::Tick</c>).
    /// </summary>
    public Rgba HighlightNow => _pulse.Now(HighlightFrom, HighlightColor);
}
