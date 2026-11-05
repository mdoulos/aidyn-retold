using AidynRetold.Core.Items;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// How the remade game shows food spoiling (<see cref="ItemStack.Freshness"/>), the user's design (Nov 5, 2026): a little clock in the
/// bottom right of a stack's cell on the Party tab's grid, green while the oldest unit has more than half its life left, yellow at
/// half or less, red when it won't survive the next camp; and the slip over the stack says how long it keeps. A food that keeps
/// for good shows no clock, which itself says what to stock up on.
/// </summary>
public static class FoodClock
{
    /// <summary>The clock, the ACRE plugin's, drawn white and tinted by the band.</summary>
    public static readonly RecordId Icon = RecordId.Parse($"{MenuImages.OwnNamespace}:image/food_clock");

    public static readonly Rgba Fresh = new(72, 200, 88, 255), Half = new(240, 196, 40, 255), Spoiling = new(232, 52, 40, 255);

    /// <summary>The clock's tint for a band.</summary>
    public static Rgba Tint(Freshness freshness) => freshness switch
    {
        Freshness.Spoiling => Spoiling,
        Freshness.Half => Half,
        _ => Fresh,
    };
}
