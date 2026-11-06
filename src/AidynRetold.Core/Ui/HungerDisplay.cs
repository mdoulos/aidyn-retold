using AidynRetold.Core.Characters;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

/// <summary>
/// How the remade game shows a member's hunger (<see cref="Character.Hunger"/>), the user's design (Nov 4, 2026): an icon beside the
/// aspect's and school's on the Party tab, the user's art, with its name and what it costs on hover; and a soft pulse over the
/// member's portrait wherever it's drawn, the trek HUD's, the Party tab's and the battle screen's, amber every 3 seconds while
/// Hungry and red every 1.5 while Starving. No message says so: the HUD's portraits come up as a member grows hungrier.
/// </summary>
public static class HungerDisplay
{
    /// <summary>The icons, the ACRE plugin's: a fork and spoon on amber and on red.</summary>
    public static readonly RecordId HungryIcon = Icon("status_hungry"), StarvingIcon = Icon("status_starving"), WellFedIcon = Icon("status_wellfed");

    /// <summary>The icons, for a look that draws them at the original's icons' size.</summary>
    public static readonly IReadOnlyList<RecordId> Icons = [HungryIcon, StarvingIcon, WellFedIcon];

    public static readonly Rgba Hungry = new(255, 176, 48, 255), Starving = new(255, 40, 24, 255);

    /// <summary>How long a pulse takes, in seconds, and how far its tint rises at its height.</summary>
    public const float HungryPeriod = 3f, StarvingPeriod = 1.5f, Peak = 0.55f;

    /// <summary>The status icon for a member's hunger, or none while they're fed.</summary>
    public static RecordId? StatusIcon(HungerState state) => state switch
    {
        HungerState.Hungry => HungryIcon,
        HungerState.Starving => StarvingIcon,
        HungerState.WellFed => WellFedIcon,
        _ => null,
    };

    /// <summary>What the icon's hover says.</summary>
    public static string? Tooltip(HungerState state) => state switch
    {
        HungerState.Hungry => $"Hungry: -{Character.HungryLoss} to Intelligence, Willpower, Dexterity and Strength",
        HungerState.Starving => $"Starving: -{Character.StarvingLoss} to Intelligence, Willpower, Dexterity and Strength",
        HungerState.WellFed => "Well fed: can't eat more for now",
        _ => null,
    };

    /// <summary>
    /// The tint over a hungry member's portrait <paramref name="seconds"/> into the pulse, its alpha rising and falling smoothly from
    /// nothing; none while they're fed.
    /// </summary>
    public static Rgba? Tint(HungerState state, float seconds) => Pulse(StatusPulse.Rank(state), seconds);

    /// <summary>The pulse of a band by how bad it is (<see cref="StatusPulse.Rank(HungerState)"/>): amber at 1, red at 2, none at 0.</summary>
    public static Rgba? Pulse(int rank, float seconds)
    {
        if (rank <= 0)
            return null;
        var period = rank >= 2 ? StarvingPeriod : HungryPeriod;
        var phase = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * (seconds % period) / period);
        var color = rank >= 2 ? Starving : Hungry;
        return color with { A = (byte)(255 * Peak * phase) };
    }

    private static RecordId Icon(string key) => RecordId.Parse($"{MenuImages.OwnNamespace}:image/{key}");
}
