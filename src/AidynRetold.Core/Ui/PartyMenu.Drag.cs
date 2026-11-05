using AidynRetold.Core.Items;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// Dragging items with the mouse in the port's look, the user's choice (Nov 3, 2026): a carried item onto a slot of the silhouette or a
/// member's portrait, and what's worn in a slot off onto the grid. The view follows the mouse and says where it was let go.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// A carried item let go on a slot of the silhouette: put on the member shown in that slot, as choosing it for the slot does, if it
    /// fits, the grid showing what it did and the slot selected as it was.
    /// </summary>
    public void DropOnSlot(InventoryRow row, EquipmentSlot slot)
    {
        if (HasPopup || row.Wearer is not null || !Fits(row.Item, slot))
            return;
        var selected = SelectedSlot;
        PutInto(slot, row);
        SelectedSlot = selected;
    }

    /// <summary>
    /// A carried item let go on a member's portrait: on the member shown, what A on it does; on another, a potion is drunk by them, a
    /// scroll read and anything else put on them, saying why where it can't be. Not in a battle's pause, where only the member whose turn
    /// it is may change anything, but for the member shown.
    /// </summary>
    public void DropOnMember(InventoryRow row, int slot)
    {
        if (HasPopup || row.Wearer is not null || Party.Slots.ElementAtOrDefault(slot) is not { } member)
            return;
        if (slot == Member)
        {
            Use(row);
            return;
        }
        if (Combat is not null)
            return;
        switch (row.Item.Kind)
        {
            case ItemKind.Potion or ItemKind.Food:
                Say(Potions.Drink(Party, member, row.Item, Random) switch
                {
                    DrinkResult.NotNow => Messages?.PotionNotNow,
                    DrinkResult.NotInTrek => Messages?.PotionNotInTrek,
                    DrinkResult.WellFed => string.Format(System.Globalization.CultureInfo.InvariantCulture, WellFedLine, member.Name),
                    _ => null,
                });
                break;
            case ItemKind.Scroll:
                Read(member, row.Item);
                break;
            default:
                Equip(member, row);
                break;
        }
        Refresh();
    }

    /// <summary>What's worn in a slot let go on the grid: taken off, as the slot's Unequip does.</summary>
    public void DropOffSlot(EquipmentSlot slot)
    {
        if (!HasPopup && Worn(slot) is { } worn)
            Use(new InventoryRow(worn, 1, Member));
    }
}
