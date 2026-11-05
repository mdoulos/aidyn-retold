using AidynRetold.Core.Calendar;

namespace AidynRetold.Core.Characters;

/// <summary>
/// How fed a member is, the remade game's (characters.md#hunger), by their <see cref="Character.Satiety"/>: <see cref="Fed"/> shows
/// nothing; <see cref="WellFed"/> shows, and stops them eating; <see cref="Hungry"/> and <see cref="Starving"/> show and cost stats.
/// </summary>
public enum HungerState
{
    Fed,
    WellFed,
    Hungry,
    Starving,
}

public sealed partial class Character
{
    /// <summary>
    /// The satiety bands (the user's design, Nov 5, 2026): Starving from 1, Hungry from 4, Fed from 7, Well Fed from 10, up to 12, and
    /// where a member starts, at the top of Fed.
    /// </summary>
    public const int SatietyLeast = 1, HungryFrom = 4, FedFrom = 7, WellFedFrom = 10, SatietyMost = 12, SatietyStart = 9;

    /// <summary>How much of the game's time takes a point off: eight hours, a camp's rest, three a day.</summary>
    public const ulong FallEvery = 8UL * GameClock.Hour;

    /// <summary>What Hungry and Starving take off each of the four stats.</summary>
    public const int HungryLoss = 2, StarvingLoss = 5;

    private static readonly Stat[] HungerStats = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Strength];

    // What hunger has taken off each of the four, to give back exactly.
    private int _hungerLoss;

    /// <summary>
    /// How full the member is, <see cref="SatietyLeast"/> to <see cref="SatietyMost"/>, which the player never sees: food adds its points (<c>ItemRecord.Satiety</c>),
    /// and time takes one off every <see cref="FallEvery"/>.
    /// </summary>
    public int Satiety { get; private set; } = SatietyStart;

    /// <summary>The game's time since the last point fell, toward the next.</summary>
    public ulong SinceFell { get; private set; }

    /// <summary>The member's band, or Fed wherever the plugins have no food, so the original game alone has no hunger.</summary>
    public HungerState Hunger => !Rules.Magic.HasFood ? HungerState.Fed
        : Satiety >= WellFedFrom ? HungerState.WellFed
        : Satiety >= FedFrom ? HungerState.Fed
        : Satiety >= HungryFrom ? HungerState.Hungry
        : HungerState.Starving;

    /// <summary>Eats something worth <paramref name="points"/>: a snack 1, a dish 2 or 3. The next point falls as it would have.</summary>
    public void Eat(int points) => SetSatiety(Satiety + points, SinceFell);

    /// <summary>Sets the satiety and the time toward its next fall, as a save has them, and the stats hunger costs with it.</summary>
    public void SetSatiety(int satiety, ulong sinceFell)
    {
        Satiety = Math.Clamp(satiety, SatietyLeast, SatietyMost);
        SinceFell = Math.Min(sinceFell, FallEvery - 1);
        var loss = Hunger switch { HungerState.Starving => StarvingLoss, HungerState.Hungry => HungryLoss, _ => 0 };
        if (loss == _hungerLoss)
            return;
        foreach (var stat in HungerStats)
            Stats.ChangeModified(stat, _hungerLoss - loss);
        _hungerLoss = loss;
    }

    /// <summary>
    /// The satiety a save from before the tracker stands for, by the time since its member last ate: Starving past three days, Hungry
    /// past two, else the top of Fed.
    /// </summary>
    public static int SatietyOf(ulong sinceMeal) => sinceMeal >= 3UL * GameClock.Day ? HungryFrom - 1 : sinceMeal >= 2UL * GameClock.Day ? FedFrom - 1 : SatietyStart;

    private void AgeHunger(ulong units)
    {
        if (units == 0 || !Rules.Magic.HasFood)
            return;
        var since = SinceFell + units;
        SetSatiety(Satiety - (int)Math.Min(since / FallEvery, (ulong)SatietyMost), since % FallEvery);
    }
}
