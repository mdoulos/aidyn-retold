namespace AidynRetold.Core.Ui;

/// <summary>What A does with an item on the Party tab, named (<see cref="PartyMenu.ActionOf"/>): the item box's and the hover's word for it.</summary>
public enum ItemAction
{
    Unequip,
    Equip,
    Drink,
    /// <summary>Food, the remade game's.</summary>
    Eat,
    Read,
}

public static class ItemActions
{
    /// <summary>The action's label, as the prompts and the box say it.</summary>
    public static string Label(this ItemAction action) => action.ToString();
}
