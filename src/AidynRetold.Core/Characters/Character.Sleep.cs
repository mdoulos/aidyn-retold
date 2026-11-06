using AidynRetold.Core.Calendar;

namespace AidynRetold.Core.Characters;

/// <summary>
/// How rested a member is, the remade game's (characters.md#sleep), by the time since they last slept (<see cref="Character.SinceSlept"/>):
/// <see cref="Rested"/> shows nothing; <see cref="Tired"/> and <see cref="Overtired"/> show and cost stats, as Hungry and Starving do.
/// </summary>
public enum RestState
{
    Rested,
    Tired,
    Overtired,
}

public sealed partial class Character
{
    /// <summary>The user's design (Nov 5, 2026): Tired a day and a half after sleeping, Overtired after three days.</summary>
    public const ulong TiredAfter = 36UL * GameClock.Hour, OvertiredAfter = 72UL * GameClock.Hour;

    /// <summary>What Tired and Overtired take off each of the four stats hunger costs: the same.</summary>
    public const int TiredLoss = HungryLoss, OvertiredLoss = StarvingLoss;

    // Past Overtired the count means no more, so it stops short of overflowing a save.
    private const ulong SinceSleptMost = OvertiredAfter * 2;

    // What tiredness has taken off each of the four, to give back exactly, apart from hunger's.
    private int _restLoss;

    /// <summary>
    /// The game's time since the member last slept, which only time raises and only sleeping, a camp's or a rest's (<see cref="Sleep"/>), brings
    /// back to nothing. A new game's member is rested.
    /// </summary>
    public ulong SinceSlept { get; private set; }

    /// <summary>The member's band, or Rested wherever the plugins have no food, where hunger is off too: the original game alone has neither.</summary>
    public RestState Rest => !Rules.Magic.HasFood ? RestState.Rested
        : SinceSlept >= OvertiredAfter ? RestState.Overtired
        : SinceSlept >= TiredAfter ? RestState.Tired
        : RestState.Rested;

    /// <summary>The member sleeps, through a camp or a rest: Tired and Overtired go entirely.</summary>
    public void Sleep() => SetSinceSlept(0);

    /// <summary>Sets the time since the member slept, as a save has it, and the stats tiredness costs with it.</summary>
    public void SetSinceSlept(ulong since)
    {
        SinceSlept = Math.Min(since, SinceSleptMost);
        var loss = Rest switch { RestState.Overtired => OvertiredLoss, RestState.Tired => TiredLoss, _ => 0 };
        if (loss == _restLoss)
            return;
        foreach (var stat in HungerStats)
            Stats.ChangeModified(stat, _restLoss - loss);
        _restLoss = loss;
    }

    private void AgeRest(ulong units)
    {
        if (units == 0 || !Rules.Magic.HasFood)
            return;
        SetSinceSlept(SinceSlept + units);
    }
}
