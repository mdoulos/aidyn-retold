using AidynRetold.Core.Characters;
using AidynRetold.Core.Items;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// The silhouette as the place to equip, in the port's look, the user's choice (Nov 3, 2026): a slot selected shows on the grid only
/// what fits it, A on the slot goes onto them to choose one, and A on one puts it on in that slot; and the stats card shows what
/// putting an item on would make the member's stats (<see cref="StatChanges"/>).
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// The slot A on a slot is choosing an item for, while the stick is on the grid of what fits it, or null. A on an item puts it on in
    /// the slot and B goes back to the slot, either way leaving the stick on the slot.
    /// </summary>
    public EquipmentSlot? Picking
    {
        get;
        private set
        {
            field = value;
            SyncFitting();
        }
    }

    /// <summary>
    /// The slot whose fitting items the grid shows, in the port's look: the slot the stick or a click is on, or the one being chosen
    /// for (<see cref="Picking"/>); null for the whole inventory.
    /// </summary>
    public EquipmentSlot? Fitting => Spatial && List == PartyList.Inventory ? SelectedSlot ?? Picking : null;

    /// <summary>The slot the grid's rows were last built to fit, and where the cursor was on the whole inventory before they were.</summary>
    private EquipmentSlot? _rowsFitting;

    private int _wholeCursor;

    /// <summary>
    /// Builds the grid's rows again where the slot they fit has changed: the grid of what fits a slot starts at its first item, and the
    /// whole inventory again where its cursor was.
    /// </summary>
    private void SyncFitting()
    {
        if (Fitting == _rowsFitting)
            return;
        if (_rowsFitting is null)
            _wholeCursor = Inventory.Cursor;
        Refresh();
        if (Grid)
            CurrentGrid.Apply(Inventory);
        Inventory.Point(_rowsFitting is null ? _wholeCursor : 0);
    }

    /// <summary>
    /// The grid as it stands: the whole inventory's (<see cref="InventoryGrid"/>), or with a slot's fitting items one line shorter
    /// (<see cref="FittingGrid"/>), leaving room under it for the way back to the whole, the user's design (Nov 5, 2026).
    /// </summary>
    public GridLayout CurrentGrid => Fitting is null ? InventoryGrid : FittingGrid;

    /// <summary>The grid a line shorter, for a slot's fitting items.</summary>
    public static GridLayout FittingGrid => InventoryGrid with
    {
        Bounds = new ScreenRect(InventoryGrid.Bounds.Left, InventoryGrid.Bounds.Top, InventoryGrid.Bounds.Right, InventoryGrid.Bounds.Bottom - InventoryGrid.Cell),
    };

    /// <summary>
    /// Shows the whole inventory again, the slot unselected and the stick back on the grid: the Inventory tab clicked while it shows
    /// what fits a slot, or the See Full Inventory button under the grid (the user's design, Nov 5, 2026).
    /// </summary>
    public void ClearFitting()
    {
        if (Fitting is null)
            return;
        SelectedSlot = null;
        Picking = null;
        _focus = PartyFocus.List;
    }

    /// <summary>
    /// Whether an item goes in a slot: a weapon in the weapon's, armor and a shield in theirs, each kind of gear in its own, a ring in
    /// either ring's, and a scroll with a spell in any of the scrolls'.
    /// </summary>
    public static bool Fits(Item item, EquipmentSlot slot) => slot switch
    {
        EquipmentSlot.Weapon => item.Kind == ItemKind.Weapon,
        EquipmentSlot.Armor => item.Kind == ItemKind.Armor,
        EquipmentSlot.Shield => item.Kind == ItemKind.Shield,
        EquipmentSlot.Gloves => item.Kind == ItemKind.Gloves,
        EquipmentSlot.Helmet => item.Kind == ItemKind.Helmet,
        EquipmentSlot.Amulet => item.Kind == ItemKind.Amulet,
        EquipmentSlot.Cloak => item.Kind == ItemKind.Cloak,
        EquipmentSlot.Belt => item.Kind == ItemKind.Belt,
        EquipmentSlot.Boots => item.Kind == ItemKind.Boots,
        EquipmentSlot.Wand => item.Kind == ItemKind.Wand,
        EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => item.Kind == ItemKind.Ring,
        _ => item.Kind == ItemKind.Scroll && item.Bonuses.Spell is not null,
    };

    /// <summary>
    /// A on an item while choosing for a slot: puts it on the member shown in that slot, as A on its row would, a ring or a scroll taking
    /// the slot's own one's place, which comes off first and goes back on if the new one is refused, as rings and scrolls are otherwise
    /// added, never swapped. The stick goes back to the slot.
    /// </summary>
    private void PutInto(EquipmentSlot slot, InventoryRow row)
    {
        if (Shown is not { } member)
            return;
        var numbers = Numbers;
        var slots = WornIcons;
        bool done;
        if (Combat is { } combat)
            done = UseInCombat(combat, member, row) ?? Equip(member, row);
        else if (row.Item.Kind is ItemKind.Ring or ItemKind.Scroll && Worn(slot) is { } worn)
        {
            if (!Party.Unequip(member, worn))
            {
                Say(NoRoom);
                done = false;
            }
            else if (!(done = Equip(member, row)))
            {
                Party.Equip(member, worn.Id);
                Refresh();
            }
        }
        else
            done = Equip(member, row);
        if (done)
            ChangeHeader(numbers, slots);
        SelectedSlot = slot;
        Picking = null;
    }

    /// <summary>
    /// What putting an item on would change of the member shown's stats, by stat, none unchanged: its stat bonus but Stamina's, which is
    /// its battery, and armor's or a shield's Dexterity, less those of what it replaces: what's in <paramref name="into"/> if it's given,
    /// else what it would replace, the weapon, the armor, the shield or the first of its kind of gear, rings and scrolls being added.
    /// Empty for what isn't put on, or with no member shown.
    /// </summary>
    public IReadOnlyDictionary<Stat, int> StatChanges(Item item, EquipmentSlot? into = null)
    {
        var changes = new Dictionary<Stat, int>();
        if (Shown is not { } member || item.Kind is ItemKind.Potion or ItemKind.Food or ItemKind.Misc or ItemKind.Key
            || item.Kind == ItemKind.Scroll && into is null)
            return changes;
        var equipment = member.Equipment;
        var replaced = into is { } slot ? Worn(slot)?.Item : item.Kind switch
        {
            ItemKind.Weapon => equipment.Weapon?.Item,
            ItemKind.Armor => equipment.Armor?.Item,
            ItemKind.Shield => equipment.Shield?.Item,
            ItemKind.Ring or ItemKind.Scroll => null,
            _ => equipment.Gear.OfType<ItemInstance>().FirstOrDefault(g => g.Item.Kind == item.Kind)?.Item,
        };
        Add(item, 1);
        if (replaced is not null)
            Add(replaced, -1);
        foreach (var stat in changes.Where(c => c.Value == 0).Select(c => c.Key).ToList())
            changes.Remove(stat);
        return changes;

        void Add(Item what, int sign)
        {
            if (what.Bonuses.Stat is { } bonus && bonus.Stat != Item.StaminaStat)
                changes[StatId.FromRecord(bonus.Stat)] = changes.GetValueOrDefault(StatId.FromRecord(bonus.Stat)) + sign * bonus.Amount;
            if (what.Armor is { Dexterity: not 0 } armor)
                changes[Stat.Dexterity] = changes.GetValueOrDefault(Stat.Dexterity) + sign * armor.Dexterity;
        }
    }

    /// <summary>
    /// What the stats card shows putting the cursor's item on would do, in the port's look: the changes of the item the stick is on in the
    /// grid, into the slot being chosen for; empty where there's none.
    /// </summary>
    public IReadOnlyDictionary<Stat, int> CursorStatChanges =>
        Spatial && Focus == PartyFocus.List && List == PartyList.Inventory && CursorShows && Inventory.Current is { Wearer: null } row
            ? StatChanges(row.Item, Picking)
            : new Dictionary<Stat, int>();
}
