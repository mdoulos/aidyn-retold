using AidynRetold.Core.Characters;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// How the remade game shows a member's tiredness (<see cref="Character.Rest"/>), the user's design (Nov 5, 2026), as hunger's
/// (<see cref="HungerDisplay"/>): an icon beside hunger's on the Party tab, the user's art, with its name and cost on hover, and the
/// same pulse over the portrait, amber while Tired and red while Overtired; the worse of hunger and tiredness pulses, never both
/// (<see cref="StatusPulse"/>).
/// </summary>
public static class RestDisplay
{
    /// <summary>The icons, the ACRE plugin's: three Zs on amber and on red.</summary>
    public static readonly RecordId TiredIcon = Icon("status_tired"), OvertiredIcon = Icon("status_overtired");

    public static readonly IReadOnlyList<RecordId> Icons = [TiredIcon, OvertiredIcon];

    /// <summary>The status icon for a member's tiredness, or none while they're rested.</summary>
    public static RecordId? StatusIcon(RestState state) => state switch
    {
        RestState.Tired => TiredIcon,
        RestState.Overtired => OvertiredIcon,
        _ => null,
    };

    /// <summary>What the icon's hover says.</summary>
    public static string? Tooltip(RestState state) => state switch
    {
        RestState.Tired => $"Tired: -{Character.TiredLoss} to Intelligence, Willpower, Dexterity and Strength",
        RestState.Overtired => $"Overtired: -{Character.OvertiredLoss} to Intelligence, Willpower, Dexterity and Strength",
        _ => null,
    };

    /// <summary>The tint over a tired member's portrait <paramref name="seconds"/> into the pulse; none while they're rested.</summary>
    public static Rgba? Tint(RestState state, float seconds) => HungerDisplay.Pulse(StatusPulse.Rank(state), seconds);

    private static RecordId Icon(string key) => RecordId.Parse($"{MenuImages.OwnNamespace}:image/{key}");
}

/// <summary>
/// Hunger and tiredness together on one portrait, the user's design (Nov 5, 2026): whichever is worse pulses, amber for Hungry or Tired
/// and red for Starving or Overtired, never both at once; and its colour alone, steady, for the trek HUD's dot.
/// </summary>
public static class StatusPulse
{
    /// <summary>How bad a band is: 0 for nothing to show, 1 amber, 2 red.</summary>
    public static int Rank(HungerState state) => state switch { HungerState.Starving => 2, HungerState.Hungry => 1, _ => 0 };

    /// <inheritdoc cref="Rank(HungerState)"/>
    public static int Rank(RestState state) => state switch { RestState.Overtired => 2, RestState.Tired => 1, _ => 0 };

    /// <summary>The worse of the two, as <see cref="Rank(HungerState)"/> counts.</summary>
    public static int Worst(HungerState hunger, RestState rest) => Math.Max(Rank(hunger), Rank(rest));

    /// <summary>The colour of the worse band, steady: amber, red, or none while both are fine.</summary>
    public static Rgba? Color(HungerState hunger, RestState rest) => Worst(hunger, rest) switch
    {
        2 => HungerDisplay.Starving,
        1 => HungerDisplay.Hungry,
        _ => null,
    };

    /// <summary>The pulse of the worse band <paramref name="seconds"/> in, or none while both are fine.</summary>
    public static Rgba? Tint(HungerState hunger, RestState rest, float seconds) => HungerDisplay.Pulse(Worst(hunger, rest), seconds);
}
