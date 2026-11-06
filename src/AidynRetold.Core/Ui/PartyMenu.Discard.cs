using AidynRetold.Core.Items;

namespace AidynRetold.Core.Ui;

/// <summary>
/// Choosing items to drop, the user's design (Nov 6, 2026): the trash under the grid starts it, in place of Shift and a click, which
/// dropped one at once and was too easy to do by accident. While it's on, a click on a carried item in the grid, or A with the cursor on
/// it, selects it or lets it go, any number of them; a tab clicked, the trash again, B, or the member or list changing ends it. What's
/// done with the selection comes later: for now they're only chosen.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>Whether items are being chosen to drop.</summary>
    public bool Discarding { get; private set; }

    private readonly HashSet<ItemInstance> _discards = [];

    /// <summary>The items chosen to drop, by the stack or instance each row stands for.</summary>
    public IReadOnlyCollection<ItemInstance> Discards => _discards;

    /// <summary>Whether a row is among those chosen.</summary>
    public bool IsDiscard(InventoryRow row) => _discards.Contains(row.Instance);

    /// <summary>
    /// Starts choosing, on the port's grid with no popup up: the whole inventory shows, nothing chosen yet, and the stick is on the grid.
    /// </summary>
    public void StartDiscarding()
    {
        if (HasPopup || !Spatial || List != PartyList.Inventory)
            return;
        Discarding = true;
        _discards.Clear();
        SelectedSlot = null;
        Picking = null;
        _focus = PartyFocus.List;
    }

    /// <summary>Ends choosing, letting every chosen item go.</summary>
    public void StopDiscarding()
    {
        Discarding = false;
        _discards.Clear();
    }

    /// <summary>Chooses a row, or lets a chosen one go, while choosing; only one that can be dropped (<see cref="CanDrop"/>). True if it changed.</summary>
    public bool ToggleDiscard(InventoryRow row)
    {
        if (!Discarding || !CanDrop(row))
            return false;
        if (!_discards.Remove(row.Instance))
            _discards.Add(row.Instance);
        return true;
    }
}
