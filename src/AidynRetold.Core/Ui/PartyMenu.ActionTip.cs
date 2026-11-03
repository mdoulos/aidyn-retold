using AidynRetold.Core.Items;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>The actiontip over the Party tab's inventory and silhouette (<see cref="ActionTip"/>).</summary>
public sealed partial class PartyMenu
{
    /// <summary>The actiontip that's up, or null.</summary>
    public ActionTip? Tip { get; private set; }

    /// <summary>
    /// Opens the actiontip on the selected item, the worn one in the silhouette's slot selected or the inventory cursor's: what A does
    /// with it, if anything, and its details. Not with a popup up, nor in the original's look, which has none. True if it opened.
    /// </summary>
    public bool OpenTip()
    {
        if (HasPopup || OriginalLook || TipRow() is not { } row)
            return false;
        List<ActionTipChoice> choices = [];
        if (ActionFor(row) is { } action)
            choices.Add(action);
        if (Details() is not null)
            choices.Add(ActionTipChoice.Details);
        if (choices.Count == 0)
            return false;
        Tip = new ActionTip(choices);
        return true;
    }

    /// <summary>Closes the actiontip.</summary>
    public void CloseTip() => Tip = null;

    /// <summary>Puts the actiontip's cursor on a choice, as hovering does.</summary>
    public void PointTip(int choice) => Tip?.Choices.Point(choice);

    /// <summary>The menus' Appraise on the tab: the actiontip on the cursor's item, the controller's way to it.</summary>
    bool IPartyWindow.Appraise() => OpenTip();

    /// <summary>The item the actiontip is for: the worn one in the silhouette's slot selected, or the inventory cursor's row.</summary>
    private InventoryRow? TipRow() => SelectedSlot is { } slot
        ? Worn(slot) is { } worn ? new InventoryRow(worn, 1, Member) : null
        : List == PartyList.Inventory ? Inventory.Current : null;

    /// <summary>
    /// What A does with a row, named: a worn item comes off, a potion is drunk, a scroll read, and the rest put on; a flask is taken in
    /// hand and a scroll worn in a battle's pause. Nothing for what can't be worn.
    /// </summary>
    private ActionTipChoice? ActionFor(InventoryRow row) => row.Wearer is not null ? ActionTipChoice.Unequip
        : Picking is not null ? ActionTipChoice.Equip : row.Item.Kind switch
    {
        ItemKind.Potion => Combat is not null && Potions.IsFlask(row.Item) ? ActionTipChoice.Equip : ActionTipChoice.Drink,
        ItemKind.Scroll => Combat is not null ? ActionTipChoice.Equip : ActionTipChoice.Read,
        ItemKind.Misc or ItemKind.Key => null,
        _ => ActionTipChoice.Equip,
    };

    /// <summary>
    /// A on the actiontip: closes it and does the choice, as A on the item would, or holds its details open. Unequip takes off what's in
    /// the slot selected, where A on the slot chooses what goes in it in the port's look.
    /// </summary>
    private void ChooseTip(ActionTip tip)
    {
        Tip = null;
        if (tip.Choices.Current == ActionTipChoice.Details)
            DetailsPane.Pin(Details() is not null);
        else if (tip.Choices.Current == ActionTipChoice.Unequip && SelectedSlot is { } slot && Worn(slot) is { } worn)
            Use(new InventoryRow(worn, 1, Member));
        else
            Accept();
    }
}
