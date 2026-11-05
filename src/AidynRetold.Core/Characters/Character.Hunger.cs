using AidynRetold.Core.Calendar;

namespace AidynRetold.Core.Characters;

/// <summary>
/// How fed a member is, the remade game's (characters.md#hunger): <see cref="Fed"/> is the ordinary state, and hides whether a meal
/// was eaten lately (<see cref="Character.IsFed"/>); <see cref="WellFed"/> shows, after two meals, and stops them eating more until it
/// wears off. The order is how badly off they are, for the HUD.
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
    /// <summary>The game's time without eating anything before a member is Hungry, and Starving.</summary>
    public const ulong HungryAfter = 2UL * GameClock.Day, StarvingAfter = 3UL * GameClock.Day;

    /// <summary>How long a meal counts for (the user's design, Nov 5, 2026): two within it make a member Well Fed, for as long.</summary>
    public const ulong FullFor = GameClock.Day / 3;

    /// <summary>What Hungry and Starving take off each of the four stats.</summary>
    public const int HungryLoss = 2, StarvingLoss = 5;

    private static readonly Stat[] HungerStats = [Stat.Intelligence, Stat.Willpower, Stat.Dexterity, Stat.Strength];

    // What hunger has taken off each of the four, to give back exactly.
    private int _hungerLoss;

    /// <summary>The game's time since the member last ate anything, as the effects count it.</summary>
    public ulong SinceMeal { get; private set; }

    /// <summary>The meals eaten lately, up to two, which <see cref="FullFor"/> after the last one are forgotten.</summary>
    public int Meals { get; private set; }

    /// <summary>The game's time since the last meal, which <see cref="Meals"/> counts by.</summary>
    public ulong SinceMealEaten { get; private set; }

    /// <summary>Whether a meal was eaten lately: the hidden Fed status, which the next meal makes Well Fed.</summary>
    public bool IsFed => Rules.Magic.HasFood && Meals > 0;

    /// <summary>How the member stands: Well Fed after two meals, else by the time since they last ate anything.</summary>
    public HungerState Hunger => !Rules.Magic.HasFood ? HungerState.Fed
        : Meals >= 2 ? HungerState.WellFed
        : SinceMeal >= StarvingAfter ? HungerState.Starving
        : SinceMeal >= HungryAfter ? HungerState.Hungry
        : HungerState.Fed;

    /// <summary>Eats something, which feeds the member; a meal (<c>ItemTraits.Meal</c>) counts toward Well Fed too.</summary>
    public void Eat(bool meal = false)
    {
        SetSinceMeal(0);
        if (meal)
            SetMeals(Math.Min(2, Meals + 1), 0);
    }

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

    /// <summary>The meals eaten lately and the time since the last, as a save has them: past <see cref="FullFor"/>, none count.</summary>
    public void SetMeals(int meals, ulong since)
    {
        SinceMealEaten = Math.Min(since, FullFor);
        Meals = SinceMealEaten >= FullFor ? 0 : Math.Clamp(meals, 0, 2);
    }

    private void AgeHunger(ulong units)
    {
        if (units == 0 || !Rules.Magic.HasFood)
            return;
        SetSinceMeal(SinceMeal + units);
        if (Meals > 0)
            SetMeals(Meals, SinceMealEaten + units);
    }
}
