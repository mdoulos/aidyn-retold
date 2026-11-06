using System.Globalization;
using AidynRetold.Core.Calendar;

namespace AidynRetold.Core.Items;

/// <summary>
/// How close the oldest unit of a stack is to spoiling (<see cref="ItemStack.Freshness"/>), which the Party tab's grid shows as a clock,
/// the user's design (Nov 5, 2026): green, yellow or red.
/// </summary>
public enum Freshness
{
    /// <summary>More than half its life left.</summary>
    Fresh,

    /// <summary>Half or less.</summary>
    Half,

    /// <summary>It won't survive the next camp: <see cref="Spoilage.Soon"/> or less.</summary>
    Spoiling,
}

/// <summary>Food that spoiled and left the inventory: how many of what.</summary>
public readonly record struct Tossed(Item Item, int Quantity);

/// <summary>
/// The remade game's spoilage, the user's design (Nov 5, 2026; items.md#cooking-and-food): a food that spoils keeps for so many game days
/// once gained (<see cref="Item.KeepsFor"/>), each unit of a stack ageing as the members' effects do, in trek and through a camp's hours;
/// a unit past its time leaves, the oldest of a stack first, and the player is told what was tossed.
/// </summary>
public static class Spoilage
{
    /// <summary>How long before it spoils a unit counts as spoiling: a camp's eight hours, so red means "eat it before you sleep".</summary>
    public const ulong Soon = 8UL * GameClock.Hour;

    /// <summary>The band for a unit with <paramref name="left"/> of its <paramref name="keepsFor"/> to go.</summary>
    public static Freshness Of(ulong left, ulong keepsFor) =>
        left <= Soon ? Freshness.Spoiling : left * 2 <= keepsFor ? Freshness.Half : Freshness.Fresh;

    /// <summary>
    /// What the slip over a stack says of the oldest unit's time left, the user's wording (Nov 5, 2026): "Expires in 3 days", to the
    /// nearest day, or under a day "Expires in 8 hours", to the next hour.
    /// </summary>
    public static string Describe(ulong left)
    {
        if (left <= GameClock.Day)
        {
            var hours = Math.Max(1, (left + GameClock.Hour - 1) / GameClock.Hour);
            return $"Expires in {hours.ToString(CultureInfo.InvariantCulture)} {(hours == 1 ? "hour" : "hours")}";
        }
        var days = Math.Max(1, (left + GameClock.Day / 2) / GameClock.Day);
        return $"Expires in {days.ToString(CultureInfo.InvariantCulture)} {(days == 1 ? "day" : "days")}";
    }

    /// <summary>The notice of what spoiled: "Spoiled and tossed: 2 Meat Stew, 1 Fish."</summary>
    public static string Notice(IReadOnlyList<Tossed> tossed) =>
        "Spoiled and tossed: " + string.Join(", ", tossed.Select(t => $"{t.Quantity.ToString(CultureInfo.InvariantCulture)} {t.Item.Name}")) + ".";
}
