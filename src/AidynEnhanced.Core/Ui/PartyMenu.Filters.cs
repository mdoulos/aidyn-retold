using AidynEnhanced.Core.Controls;
using AidynEnhanced.Core.Items;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>What the grid shows in the port's look (<see cref="PartyMenu.Filter"/>): what's carried, of a kind, or what's worn.</summary>
public enum InventoryFilter
{
    All,
    Weapons,
    Armor,
    Accessories,
    Potions,
    Scrolls,
    Other,
    Equipped,
}

/// <summary>The order the grid shows its items in, in the port's look (<see cref="PartyMenu.Sort"/>).</summary>
public enum InventorySort
{
    /// <summary>By kind, then name, as the original's list (<see cref="InventoryOrder"/>).</summary>
    Type,
    Name,

    /// <summary>The dearest first.</summary>
    Value,
}

/// <summary>
/// The grid's filter and order in the port's look, the user's choice (Nov 3, 2026), under the grid where the quick toggle for what's
/// worn was: what's worn is in the grid only with the Equipped filter, the silhouette being where it's reached.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>What the grid shows: all that's carried, unless the player chooses a kind, or what the party wears.</summary>
    public InventoryFilter Filter { get; private set; }

    /// <summary>The order the grid shows it in.</summary>
    public InventorySort Sort { get; private set; }

    /// <summary>The control under the grid the stick is on (<see cref="PartyFocus.Foot"/>): 0 the filter, 1 the order.</summary>
    public int FootChoice { get; private set; }

    /// <summary>The kinds each filter of a kind shows.</summary>
    private static readonly Dictionary<InventoryFilter, ItemKind[]> FilterKinds = new()
    {
        [InventoryFilter.Weapons] = [ItemKind.Weapon],
        [InventoryFilter.Armor] = [ItemKind.Armor, ItemKind.Shield, ItemKind.Helmet, ItemKind.Cloak, ItemKind.Gloves, ItemKind.Boots, ItemKind.Belt],
        [InventoryFilter.Accessories] = [ItemKind.Ring, ItemKind.Amulet, ItemKind.Wand],
        [InventoryFilter.Potions] = [ItemKind.Potion],
        [InventoryFilter.Scrolls] = [ItemKind.Scroll],
        [InventoryFilter.Other] = [ItemKind.Misc, ItemKind.Key],
    };

    /// <summary>Whether a filter shows a row: a carried one of its kinds, or for <see cref="InventoryFilter.Equipped"/> a worn one.</summary>
    public static bool Shows(InventoryFilter filter, InventoryRow row) => filter switch
    {
        InventoryFilter.All => row.Wearer is null,
        InventoryFilter.Equipped => row.Wearer is not null,
        _ => row.Wearer is null && FilterKinds[filter].Contains(row.Item.Kind),
    };

    /// <summary>Rows in an order: as they come, by kind and name, or by name, or the dearest first.</summary>
    private static IEnumerable<InventoryRow> Sorted(IEnumerable<InventoryRow> rows, InventorySort sort) => sort switch
    {
        InventorySort.Name => rows.OrderBy(r => r.Item.Name, StringComparer.Ordinal),
        InventorySort.Value => rows.OrderByDescending(r => r.Item.Price).ThenBy(r => r.Item.Name, StringComparer.Ordinal),
        _ => rows,
    };

    /// <summary>
    /// The grid's rows in the port's look from all the party's: what fits the slot selected, or what the filter shows, in the order
    /// chosen; what's worn in the order of who wears it.
    /// </summary>
    private IReadOnlyList<InventoryRow> GridRows(IReadOnlyList<InventoryRow> rows) =>
        _rowsFitting is { } fits ? [.. Sorted(rows.Where(r => r.Wearer is null && Fits(r.Item, fits)), Sort)]
        : Filter == InventoryFilter.Equipped ? [.. rows.Where(r => r.Wearer is not null)]
        : [.. Sorted(rows.Where(r => Shows(Filter, r)), Sort)];

    /// <summary>Steps the filter round its choices, the grid starting again at its first item.</summary>
    public void CycleFilter(int by)
    {
        var count = Enum.GetValues<InventoryFilter>().Length;
        Filter = (InventoryFilter)(((int)Filter + by % count + count) % count);
        Refresh();
        Inventory.Point(0);
    }

    /// <summary>Steps the order round its choices, the grid starting again at its first item.</summary>
    public void CycleSort(int by)
    {
        var count = Enum.GetValues<InventorySort>().Length;
        Sort = (InventorySort)(((int)Sort + by % count + count) % count);
        Refresh();
        Inventory.Point(0);
    }

    /// <summary>A click on one of the controls under the grid, or A with the stick on it: steps it on.</summary>
    public void ChooseFoot(int choice, int by = 1)
    {
        if (HasPopup || !HasFoot)
            return;
        FootChoice = Math.Clamp(choice, 0, 1);
        if (FootChoice == 0)
            CycleFilter(by);
        else
            CycleSort(by);
    }

    /// <summary>Whether the grid has its filter and order under it, which the stick can step down onto: not while it shows what fits a slot.</summary>
    private bool HasFoot => Spatial && List == PartyList.Inventory && Fitting is null;

    /// <summary>A step of the stick on the controls under the grid: left and right between them, and up back onto the grid.</summary>
    private void StepFoot(int across, int down)
    {
        if (down < 0)
            _focus = PartyFocus.List;
        else if (across != 0)
            FootChoice = Math.Clamp(FootChoice + across, 0, 1);
    }

    /// <summary>The prompts with the stick on the controls under the grid.</summary>
    private void FootPrompts(Action<string, string[]> add) => add("Change", [GameControls.MenuAccept]);
}
