using AidynRetold.Core.Controls;
using AidynRetold.Core.Items;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>The item box beside the Party tab's selected item, and what the buttons do with an item.</summary>
public sealed partial class PartyMenu
{
    /// <summary>
    /// The item box, the user's design (Nov 5 and 6, 2026): with a key or a controller, the selected item, the worn one in the silhouette's
    /// slot selected or the inventory cursor's while the cursor shows, with its card (<see cref="Card"/>) and which buttons do what with
    /// it (<see cref="ItemHelp"/>), as one box beside it; null with nothing selected, with the mouse in use (the hover's card stands
    /// instead), a popup or the crafting up, a member sliding in, or in the original's look. It had buttons to click until Nov 6, 2026,
    /// which a controller can't, so it says which buttons to press instead, as the hover says what the mouse does.
    /// </summary>
    public (InventoryRow Row, ItemCard Card, IReadOnlyList<MenuPrompt> Help)? BoxShown =>
        OriginalLook || Pointing || Overlay.HasPopup || Crafting is not null || Slide is not null || BoxRow() is not { } row
            || (SelectedSlot is null && !CursorShows) ? null
        : (row, Card(row), ItemHelp(row));

    /// <summary>
    /// Which buttons do what with an item, for the box to say, each named on the device in use (<see cref="KeyName"/>): A's action with
    /// it, or on a worn slot Change for A and Unequip for Appraise; the details where it has any; and while items are being chosen to
    /// drop (<see cref="Discarding"/>), Select or Unselect. None without <see cref="KeyName"/>.
    /// </summary>
    public IReadOnlyList<MenuPrompt> ItemHelp(InventoryRow row)
    {
        var help = new List<MenuPrompt>();
        if (KeyName is null)
            return help;
        if (Discarding)
        {
            if (CanDrop(row))
                Add(IsDiscard(row) ? "Unselect" : "Select", GameControls.MenuAccept);
            return help;
        }
        if (Spatial && SelectedSlot is { } slot && Worn(slot) is not null)
        {
            Add("Change", GameControls.MenuAccept);
            Add("Unequip", GameControls.MenuAppraise);
        }
        else if (ActionFor(row) is { } action)
        {
            Add(action.Label(), GameControls.MenuAccept);
        }
        if (HasDetails(row))
            Add("See Details", GameControls.MenuDetails);
        return help;

        void Add(string label, params string[] controls)
        {
            if (Prompt(label, controls) is { } prompt)
                help.Add(prompt);
        }
    }

    /// <summary>What A does with a row, named, for the mouse's hover to say (<see cref="ActionFor"/>).</summary>
    public ItemAction? ActionOf(InventoryRow row) => ActionFor(row);

    /// <summary>Whether the row has details to show.</summary>
    public bool HasDetails(InventoryRow row) => MenuDetailsBuilder.Item(row.Instance, Shown, _text, _spells, Party.Inventory.Catalog) is not null;

    /// <summary>
    /// Whether a row can be dropped, so chosen to drop (<see cref="ToggleDiscard"/>): a carried item that's no key item and needn't be
    /// taken, out of a battle's pause.
    /// </summary>
    public bool CanDrop(InventoryRow row) =>
        row.Wearer is null && row.Item.KeyItem is null && !row.Item.Traits.MustTake && row.Item.Kind != ItemKind.Key && Combat is null;

    /// <summary>
    /// The menus' Appraise on the tab, the user's design (Nov 6, 2026): takes off what's worn in the silhouette's slot selected, in the
    /// port's look, where A on the slot chooses what goes in it; in the original's look A takes it off. True if it did.
    /// </summary>
    bool IPartyWindow.Appraise() => Unequip();

    /// <summary>Takes off what's worn in the slot selected, in the port's look. True if there was something to take off.</summary>
    public bool Unequip()
    {
        if (HasPopup || !Spatial || SelectedSlot is not { } slot || Worn(slot) is not { } worn)
            return false;
        Use(new InventoryRow(worn, 1, Member));
        return true;
    }

    /// <summary>The item the box is for: the worn one in the silhouette's slot selected, or the inventory cursor's row.</summary>
    private InventoryRow? BoxRow() => SelectedSlot is { } slot
        ? Worn(slot) is { } worn ? new InventoryRow(worn, 1, Member) : null
        : List == PartyList.Inventory ? Inventory.Current : null;

    /// <summary>
    /// What A does with a row, named: a worn item comes off, a potion is drunk, a scroll read, and the rest put on; a flask is taken in
    /// hand and a scroll worn in a battle's pause. Nothing for what can't be worn.
    /// </summary>
    private ItemAction? ActionFor(InventoryRow row) => row.Wearer is not null ? ItemAction.Unequip
        : Picking is not null ? ItemAction.Equip : row.Item.Kind switch
    {
        ItemKind.Potion => Combat is not null && Potions.IsFlask(row.Item) ? ItemAction.Equip : ItemAction.Drink,
        ItemKind.Food => ItemAction.Eat,
        ItemKind.Scroll => Combat is not null ? ItemAction.Equip : ItemAction.Read,
        ItemKind.Misc or ItemKind.Key => null,
        _ => ItemAction.Equip,
    };
}
