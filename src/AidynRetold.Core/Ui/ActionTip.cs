namespace AidynRetold.Core.Ui;

/// <summary>What an actiontip offers to do with an item.</summary>
public enum ActionTipChoice
{
    Unequip,
    Equip,
    Drink,
    /// <summary>Food, the remade game's.</summary>
    Eat,
    Read,

    /// <summary>C-down's details, held open until A, B or a click lets them go (<see cref="DetailsPane.Pin"/>).</summary>
    Details,
}

/// <summary>
/// The actiontip, the user's design (Nov 2, 2026; the UI Summary's): a small menu on a slip of parchment beside an item, which a right
/// click on it opens, or on a controller the menus' Appraise on the cursor's, saying what can be done with it: what A does, named,
/// and Details. Its cursor moves as a popup's does, A or a click chooses, and B, a right click or a click elsewhere closes it.
/// </summary>
public sealed class ActionTip(IReadOnlyList<ActionTipChoice> choices)
{
    public MenuList<ActionTipChoice> Choices { get; } = new(choices);

    /// <summary>A choice's label.</summary>
    public static string Label(ActionTipChoice choice) => choice.ToString();
}
