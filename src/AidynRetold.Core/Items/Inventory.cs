using AidynRetold.Core.Flags;
using AidynRetold.Plugins;

namespace AidynRetold.Core.Items;

/// <summary>
/// A stack of one item in an inventory: one instance, which the whole stack shares, and how many. For a food that spoils (the remade
/// game's, <see cref="Spoilage"/>), how long each unit has been held, oldest first: taking from the stack takes the oldest, so the one
/// about to spoil is eaten first.
/// </summary>
public sealed class ItemStack
{
    private readonly List<ulong> _ages = [];

    public ItemStack(ItemInstance instance, int quantity)
    {
        Instance = instance;
        SetQuantity(quantity);
    }

    public ItemInstance Instance { get; }

    public int Quantity { get; private set; }

    public Item Item => Instance.Item;

    /// <summary>For a food that spoils, the game's time each unit has been held, oldest first, as many as the quantity; else none.</summary>
    public IReadOnlyList<ulong> Ages => _ages;

    /// <summary>How long the oldest unit has before it spoils, or null for an item that keeps.</summary>
    public ulong? Left => Item.Spoils && _ages.Count > 0 ? Item.KeepsFor - Math.Min(_ages[0], Item.KeepsFor) : null;

    /// <summary>How close the oldest unit is to spoiling, or null for an item that keeps.</summary>
    public Freshness? Freshness => Left is { } left ? Spoilage.Of(left, Item.KeepsFor) : null;

    /// <summary>Sets the ages as a save holds them, oldest first whatever their order, padded fresh or cut to the quantity.</summary>
    public void SetAges(IEnumerable<ulong> ages)
    {
        if (!Item.Spoils)
            return;
        _ages.Clear();
        _ages.AddRange(ages.OrderByDescending(a => a).Take(Quantity));
        while (_ages.Count < Quantity)
            _ages.Add(0);
    }

    // More units come fresh; fewer takes the oldest.
    internal void SetQuantity(int quantity)
    {
        if (Item.Spoils)
        {
            if (quantity > Quantity)
                _ages.AddRange(Enumerable.Repeat(0UL, quantity - Quantity));
            else if (quantity < Quantity)
                _ages.RemoveRange(0, Math.Min(Quantity - quantity, _ages.Count));
        }
        Quantity = quantity;
    }

    // Time passing for every unit: how many are now past their time, which are the first that many.
    internal int Age(ulong units)
    {
        if (!Item.Spoils)
            return 0;
        for (var i = 0; i < _ages.Count; i++)
            _ages[i] += units;
        return _ages.Count(a => a >= Item.KeepsFor);
    }

    public override string ToString() => $"{Item.Name} × {Quantity}";
}

/// <summary>
/// The party's inventory (<c>PartyInventory</c>). Each item is one stack of up to
/// <see cref="Plugins.Records.InventoryRecord.StackLimit"/>. Most items share
/// <see cref="Plugins.Records.InventoryRecord.GeneralSlots"/> slots; those with a slot of their own
/// (<see cref="Plugins.Records.ItemTraits.OwnSlot"/>) don't count against them. Gold is the party's.
/// </summary>
/// <remarks>
/// A key item sets its region flag and its held flag when the party first gains it, and clears the
/// held flag when the last is taken (<c>AddKeyItem2</c>). The Aspect potion
/// becomes a Healing potion as it's gained (<see cref="Plugins.Records.InventoryRecord.Replaced"/>).
/// </remarks>
public sealed class PartyInventory(ItemCatalog catalog, FlagNetwork? flags = null)
{
    private readonly List<ItemStack> _stacks = [];

    public ItemCatalog Catalog { get; } = catalog;

    /// <summary>The stacks in the order they were first gained. The menus sort them.</summary>
    public IReadOnlyList<ItemStack> Stacks => _stacks;

    public ItemStack? Find(RecordId item) => _stacks.FirstOrDefault(s => s.Item.Id == item);

    public bool Has(RecordId item) => Find(item) is not null;

    public int Count(RecordId item) => Find(item)?.Quantity ?? 0;

    /// <summary>How many of the general slots are used.</summary>
    public int GeneralSlotsUsed => _stacks.Count(s => !s.Item.Traits.OwnSlot);

    /// <summary>
    /// Adds items (<c>PartyInventory::AddItem</c>): to their stack, up to the limit, or as a new stack
    /// with <paramref name="state"/>'s battery and charges, or a new item's. False if a new stack needs a
    /// general slot and there's none.
    /// </summary>
    public bool Add(RecordId item, int quantity = 1, ItemInstance? state = null) => Add(Catalog[item], quantity, state);

    /// <inheritdoc cref="Add(RecordId, int, ItemInstance?)"/>
    public bool Add(Item item, int quantity = 1, ItemInstance? state = null)
    {
        item = Replace(Catalog, item);
        if (Find(item.Id) is { } stack)
        {
            Change(stack, quantity);
            return true;
        }
        if (!item.Traits.OwnSlot && GeneralSlotsUsed >= Catalog.Rules.GeneralSlots)
            return false;
        if (item.KeyItem is { Region: { } region })
            flags?.TrySet(region, true);
        var instance = new ItemInstance(item);
        if (state is not null)
            instance.TakeStateOf(state);
        _stacks.Add(new ItemStack(instance, quantity));
        if (item.KeyItem is { } key)
            flags?.TrySet(key.Held, true);
        return true;
    }

    /// <summary>
    /// Takes items from a stack (<c>PartyInventory::TakeItem</c>), removing it when none are left. False
    /// if the party has none. Taking more than there are takes the stack.
    /// </summary>
    public bool Take(RecordId item, int quantity = 1)
    {
        if (Find(item) is not { } stack)
            return false;
        Change(stack, -quantity);
        return true;
    }

    /// <summary>Empties the inventory, as the debug menu does. Key items' flags stay as they are (<c>ClearInventory</c>).</summary>
    public void Clear() => _stacks.Clear();

    /// <summary>
    /// The game's time passing for the food that spoils, the remade game's (<see cref="Spoilage"/>), as it passes for the members'
    /// effects: every unit ages, and those past their time leave, the oldest of a stack first. What left, for the player to be told.
    /// </summary>
    public IReadOnlyList<Tossed> Age(ulong units)
    {
        if (units == 0)
            return [];
        List<Tossed>? tossed = null;
        foreach (var stack in _stacks.ToList())
        {
            var gone = stack.Age(units);
            if (gone == 0)
                continue;
            (tossed ??= []).Add(new Tossed(stack.Item, gone));
            Change(stack, -gone);
        }
        return tossed ?? [];
    }

    // PartyInventory::IncItemQuantity: up to the limit, and gone at 0, when a key item's flag clears.
    private void Change(ItemStack stack, int quantity)
    {
        stack.SetQuantity(Math.Min(stack.Quantity + quantity, Catalog.Rules.StackLimit));
        if (stack.Quantity > 0)
            return;
        _stacks.Remove(stack);
        if (stack.Item.KeyItem is { } key)
            flags?.TrySet(key.Held, false);
    }

    internal static Item Replace(ItemCatalog catalog, Item item) =>
        catalog.Rules.Replaced.FirstOrDefault(r => r.Item == item.Id) is { } replacement ? catalog[replacement.With] : item;
}

/// <summary>
/// A pile of items: a container's or a beaten party's loot, or a shop's stock (<c>GenericInventory</c>).
/// It holds up to 64 stacks. It won't take a refused item (<see cref="Plugins.Records.InventoryRecord.Refused"/>),
/// and the Aspect potion becomes a Healing potion.
/// </summary>
public sealed class ItemPile(ItemCatalog catalog)
{
    public const int Capacity = 64;

    private readonly List<ItemStack> _stacks = [];

    public IReadOnlyList<ItemStack> Stacks => _stacks;

    public ItemStack? Find(RecordId item) => _stacks.FirstOrDefault(s => s.Item.Id == item);

    /// <summary>Adds items to their stack or a new one (<c>GenericInventory::AddItem</c>). False if it refuses them or is full.</summary>
    public bool Add(RecordId item, int quantity = 1) => Add(catalog[item], quantity);

    /// <inheritdoc cref="Add(RecordId, int)"/>
    public bool Add(Item item, int quantity = 1)
    {
        if (catalog.Rules.Refused.Contains(item.Id))
            return false;
        item = PartyInventory.Replace(catalog, item);
        if (Find(item.Id) is { } stack)
        {
            Change(stack, quantity);
            return true;
        }
        if (_stacks.Count == Capacity)
            return false;
        _stacks.Add(new ItemStack(new ItemInstance(item), quantity));
        return true;
    }

    /// <summary>Takes items from a stack, removing it when none are left. False if there's none.</summary>
    public bool Take(RecordId item, int quantity = 1)
    {
        if (Find(item) is not { } stack)
            return false;
        Change(stack, -quantity);
        return true;
    }

    /// <summary>Whether the pile holds nothing loot can be left behind with (<c>IInventory::HasNoKeyItem</c>).</summary>
    public bool CanLeave => !_stacks.Any(s => s.Item.Traits.MustTake);

    private void Change(ItemStack stack, int quantity)
    {
        stack.SetQuantity(Math.Min(stack.Quantity + quantity, catalog.Rules.StackLimit));
        if (stack.Quantity <= 0)
            _stacks.Remove(stack);
    }
}
