using AidynRetold.Core.Controls;
using AidynRetold.Core.Items;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// Choosing items to drop, and dropping them, the user's design (Nov 6, 2026): the trash under the grid starts it, in place of Shift
/// and a click, which dropped one at once and was too easy to do by accident. While it's on, a click on a carried item in the grid, or A
/// with the cursor on it, chooses one of it, and each click or A after chooses one more of its stack, up to the whole stack
/// (<see cref="AddDiscard"/>); Shift and a click, or Appraise, takes one back, the last letting the item go (<see cref="RemoveDiscard"/>).
/// Any number may be chosen. The trash again with items chosen asks whether to drop them (<see cref="AskDrop"/>), naming the one item,
/// how many of one stack, how many of one kind, or how many items (<see cref="DropQuestion"/>), over Yes and No, the cursor on No:
/// Yes drops them, lost for good, and ends choosing; No only takes the question down. A tab clicked, the trash with nothing chosen,
/// B, or the member or list changing ends it, letting every chosen item go.
/// </summary>
public sealed partial class PartyMenu
{
    /// <summary>Whether items are being chosen to drop.</summary>
    public bool Discarding { get; private set; }

    private readonly Dictionary<ItemInstance, int> _discards = [];

    /// <summary>The items chosen to drop, by the stack or instance each row stands for, and how many of each.</summary>
    public IReadOnlyDictionary<ItemInstance, int> Discards => _discards;

    /// <summary>Whether any of a row's stack is chosen.</summary>
    public bool IsDiscard(InventoryRow row) => _discards.ContainsKey(row.Instance);

    /// <summary>How many of a row's stack are chosen to drop, 0 for none.</summary>
    public int DiscardCount(InventoryRow row) => _discards.GetValueOrDefault(row.Instance);

    /// <summary>How many items are chosen to drop, every unit of every stack.</summary>
    public int DiscardTotal => _discards.Values.Sum();

    /// <summary>The trash's question, whether to drop what's chosen, while it's up; null otherwise.</summary>
    public MenuChoice? DropChoice => Overlay.Choice;

    /// <summary>The question's answers, Yes first; the cursor starts on No.</summary>
    public const string DropYes = "Yes", DropNo = "No";

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

    /// <summary>
    /// Chooses one more of a row's stack to drop, up to the whole stack, while choosing; only one that can be dropped
    /// (<see cref="CanDrop"/>). True if it changed.
    /// </summary>
    public bool AddDiscard(InventoryRow row)
    {
        if (!Discarding || !CanDrop(row))
            return false;
        var count = DiscardCount(row);
        if (count >= row.Quantity)
            return false;
        _discards[row.Instance] = count + 1;
        return true;
    }

    /// <summary>Takes one of a row's stack back from those chosen to drop, the last letting the row go. True if it changed.</summary>
    public bool RemoveDiscard(InventoryRow row)
    {
        if (!Discarding || DiscardCount(row) is not (> 0 and var count))
            return false;
        if (count > 1)
            _discards[row.Instance] = count - 1;
        else
            _discards.Remove(row.Instance);
        return true;
    }

    /// <summary>
    /// The trash pressed, by a click or A with the stick on it: starts choosing; with items chosen, asks whether to drop them; with none
    /// chosen, ends choosing.
    /// </summary>
    private void PressTrash()
    {
        if (!Discarding)
            StartDiscarding();
        else if (_discards.Count == 0)
            StopDiscarding();
        else
            AskDrop();
    }

    /// <summary>Puts the question up, in the shop's colours over Yes and No, the cursor on No, so a careless press drops nothing.</summary>
    private void AskDrop()
    {
        var choice = MenuChoice.Question(DropQuestion(), [DropYes, DropNo]);
        choice.Choices.Point(1);
        Overlay.Ask(choice);
    }

    /// <summary>The question's answer: Yes drops every chosen item, lost for good, and ends choosing; No takes the question down.</summary>
    private void AnswerDrop(MenuChoice choice)
    {
        Overlay.CloseChoice();
        if (choice.Choices.Cursor != 0)
            return;
        foreach (var (instance, count) in _discards)
            Party.Inventory.Take(instance.Item.Id, count);
        StopDiscarding();
        Refresh();
        Inventory.Point(Math.Clamp(Inventory.Cursor, 0, Math.Max(0, Inventory.Rows.Count - 1)));
    }

    /// <summary>
    /// The question's words, the user's design (Nov 6, 2026): "Are you sure you want to drop Short Sword?" for one item; "... 3 Healing
    /// Potions?" for some of one stack; for different items all of one kind, "... 2 weapons?", "... 2 pieces of armor?", accessories,
    /// potions, scrolls or food (the grid's filters' kinds, <see cref="FilterKinds"/>); and "... 5 items?" for a mix, or for what
    /// no filter of a kind shows.
    /// </summary>
    public string DropQuestion()
    {
        string what;
        if (_discards.Count == 1)
        {
            var (instance, count) = _discards.First();
            what = count == 1 ? instance.Item.Name : $"{count} {Plural(instance.Item.Name)}";
        }
        else
        {
            var kinds = _discards.Keys.Select(i => KindName(i.Item.Kind)).Distinct().ToList();
            what = $"{DiscardTotal} {(kinds.Count == 1 ? kinds[0] : "items")}";
        }
        return $"Are you sure you want to drop {what}?";
    }

    /// <summary>What a kind's items are called, many of them, by the grid's filter that shows the kind; "items" for what none of a kind shows.</summary>
    private static string KindName(ItemKind kind) =>
        FilterKinds.FirstOrDefault(f => f.Value.Contains(kind)).Key switch
        {
            InventoryFilter.Weapons => "weapons",
            InventoryFilter.Armor => "pieces of armor",
            InventoryFilter.Accessories => "accessories",
            InventoryFilter.Potions => "potions",
            InventoryFilter.Scrolls => "scrolls",
            InventoryFilter.Food => "food items",
            _ => "items",
        };

    /// <summary>
    /// An item's name for more than one of it: "Healing Potions", "Daggers", "Torches", "Berries"; a name ending in armor, mail or a food
    /// that's counted by the piece ("Leather Armor", "Chain Mail", "Fish", "Bread", "Meat", "Cheese") stays as it is.
    /// </summary>
    public static string Plural(string name)
    {
        if (Uncountable.Any(u => name.EndsWith(u, StringComparison.OrdinalIgnoreCase)))
            return name;
        if (name.EndsWith('s') || name.EndsWith('x') || name.EndsWith('z') || name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal))
            return name + "es";
        if (name.Length > 1 && name.EndsWith('y') && !"aeiou".Contains(char.ToLowerInvariant(name[^2])))
            return name[..^1] + "ies";
        return name + "s";
    }

    private static readonly string[] Uncountable = ["Armor", "Mail", "Fish", "Bread", "Meat", "Cheese"];

    /// <summary>
    /// What A and Appraise do with a row while items are being chosen, for the box and the prompts: A chooses it, "Select", or one more of
    /// its stack, "Select One More", up to the whole stack; Appraise lets it go, "Unselect", or takes one back, "Unselect One". Nothing
    /// for what can't be dropped.
    /// </summary>
    private void DiscardHelp(InventoryRow row, Action<string, string[]> add)
    {
        if (!CanDrop(row))
            return;
        var count = DiscardCount(row);
        if (count < row.Quantity)
            add(count == 0 ? "Select" : "Select One More", [GameControls.MenuAccept]);
        if (count > 0)
            add(count > 1 ? "Unselect One" : "Unselect", [GameControls.MenuAppraise]);
    }
}
