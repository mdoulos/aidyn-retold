using AidynRetold.Core.Calendar;

namespace AidynRetold.Core.Characters;

/// <summary>How long since a member last ate, as the remade game counts it (<see cref="Character.Hunger"/>).</summary>
public enum HungerState
{
    Fed,
    /// <summary>Two days without eating: Intelligence, Willpower, Dexterity and Strength each 2 lower.</summary>
    Hungry,
    /// <summary>Three days: each 5 lower.</summary>
    Starving,
}

/// <summary>
/// The remade game's hunger, the user's design (Nov 4, 2026): each member counts the game's time since they last ate anything
/// (<see cref="Eat"/>), which runs as their effects' does, in trek and as camping or a rest passes its hours, not in a battle. Two
/// days on they're <see cref="HungerState.Hungry"/>, three days <see cref="HungerState.Starving"/>, which lowers the four stats a
/// combatant acts by, never below 1 (<see cref="CharacterStats.Floor"/>); Endurance and Stamina, hit points, aren't touched. It runs
/// only where the plugins have food (<see cref="Magic.MagicRules.HasFood"/>), so the original game alone has none.
/// </summary>
public sealed partial class Character
{
    /// <summary>When a member turns Hungry, and Starving, in the game clock's units.</summary>
    public const ulong HungryAfter = 2UL * GameClock.Day, StarvingAfter = 3UL * GameClock.Day;

    /// <summary>What each of the four stats loses while Hungry and while Starving.</summary>
    public const int HungryLoss = 2, StarvingLoss = 5;

    private static readonly Stat[] HungerStats = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Strength];

    // What hunger has taken off each of the four, to give back exactly.
    private int _hungerLoss;

    /// <summary>The game's time since the member last ate, in the clock's units, held at a little past Starving's.</summary>
    public ulong SinceMeal { get; private set; }

    /// <summary>How hungry the member is now.</summary>
    public HungerState Hunger => !Rules.Magic.HasFood ? HungerState.Fed
        : SinceMeal >= StarvingAfter ? HungerState.Starving
        : SinceMeal >= HungryAfter ? HungerState.Hungry
        : HungerState.Fed;

    /// <summary>The member eats: they're fed, and the stats hunger took come back.</summary>
    public void Eat() => SetSinceMeal(0);

    /// <summary>Sets the time since the member last ate, as a save holds it, and what hunger takes with it.</summary>
    public void SetSinceMeal(ulong since)
    {
        SinceMeal = Math.Min(since, StarvingAfter + GameClock.Day);
        var loss = Hunger switch { HungerState.Starving => StarvingLoss, HungerState.Hungry => HungryLoss, _ => 0 };
        if (loss == _hungerLoss)
            return;
        foreach (var stat in HungerStats)
            Stats.ChangeModified(stat, _hungerLoss - loss);
        _hungerLoss = loss;
    }

    /// <summary>Time passing for the member's hunger, as for its effects: <paramref name="units"/> of the game clock.</summary>
    private void AgeHunger(ulong units)
    {
        if (units > 0 && Rules.Magic.HasFood)
            SetSinceMeal(SinceMeal + units);
    }
}
