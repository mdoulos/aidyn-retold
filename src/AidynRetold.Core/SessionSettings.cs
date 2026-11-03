using AidynRetold.Core.Combat;
using AidynRetold.Core.Sky;

namespace AidynRetold.Core;

/// <summary>
/// What the core plays by of the player's settings, for one tick (ADR 0022): the runtime makes it from <see cref="Settings"/>
/// with the command line's for the run over them, and hands it to every <see cref="GameSession.Tick"/>, which hands it on to
/// what reads a setting: the audio's volumes, the rain and snow, the sky's placement, the player's pace, a battle's spell
/// auras, the credits' screen and the conversation box's printing. Nothing in the core reads <see cref="Settings"/> for itself, so a setting changed between
/// two ticks takes effect on the next, and a headless run plays by the one it's given, <see cref="Original"/> by default.
/// What only the runtime draws, the window, the 3D view, the draw distance and the UI screens' filter, isn't here.
/// </summary>
public sealed record SessionSettings
{
    /// <summary>The original's: its volumes and its look and feel, the settings an enhancement leaves at their default.</summary>
    public static readonly SessionSettings Original = new();

    /// <summary>The brisk pace <c>--pace</c> and F5 give for a run, a multiple of the original's: no longer a setting, the user's choice.</summary>
    public const float BriskPace = 1.3f;

    /// <summary>Every sound's volume, in hundredths (<see cref="Settings.MasterVolume"/>).</summary>
    public int MasterVolume { get; init; } = Settings.MaxVolume;

    /// <summary>The music's volume, in hundredths (<see cref="Settings.MusicVolume"/>).</summary>
    public int MusicVolume { get; init; } = 65;

    /// <summary>The world's own sounds' volume, in hundredths (<see cref="Settings.AmbientVolume"/>).</summary>
    public int AmbientVolume { get; init; } = Settings.MaxVolume;

    /// <summary>The sound effects' volume, in hundredths (<see cref="Settings.EffectsVolume"/>).</summary>
    public int EffectsVolume { get; init; } = Settings.MaxVolume;

    /// <summary>The menus' sounds' volume, in hundredths (<see cref="Settings.InterfaceVolume"/>).</summary>
    public int InterfaceVolume { get; init; } = Settings.MaxVolume;

    /// <summary>The voices' volume, in hundredths (<see cref="Settings.DialogueVolume"/>).</summary>
    public int DialogueVolume { get; init; } = Settings.MaxVolume;

    /// <summary>A group's loudness, 0 to 1: its volume times the master volume.</summary>
    public float Level(Audio.SoundGroup group)
    {
        var volume = group switch
        {
            Audio.SoundGroup.Music => MusicVolume,
            Audio.SoundGroup.Ambient => AmbientVolume,
            Audio.SoundGroup.Effects => EffectsVolume,
            Audio.SoundGroup.Interface => InterfaceVolume,
            _ => DialogueVolume,
        };
        return (float)MasterVolume / Settings.MaxVolume * volume / Settings.MaxVolume;
    }

    /// <summary>How many raindrops and snowflakes fall, as a multiple of the original's (<see cref="Particles.WeatherParticles.Density"/>).</summary>
    public float PrecipitationDensity { get; init; } = 1;

    /// <summary>How the sky places the clouds, sun, moon and stars (<see cref="SkyDirector.Placement"/>).</summary>
    public SkyPlacement SkyPlacement { get; init; } = SkyPlacement.Original;

    /// <summary>
    /// How many animation ticks the player plays per tick, 1 being the original's: trek's (<see cref="Movement.PlayerMotion.Pace"/>), and
    /// the body he steers in a battle's turn (<see cref="Battle.Pace"/>).
    /// </summary>
    public float Pace { get; init; } = 1;

    /// <summary>How a battle's lasting spells' and potions' scenes over heads take their turns (<see cref="SpellFx.Turns"/>).</summary>
    public AuraTurns SpellAuras { get; init; } = AuraTurns.Battle;

    /// <summary>How many lines tall the UI screen is, which the credits lay their cards out on: the console's 240.</summary>
    public int UiLines { get; init; } = 240;

    /// <summary>How many glyphs a tick the conversation box prints a line (<see cref="Ui.DialogueBox.GlyphsPerTick"/>): 0, the original's, shows it whole.</summary>
    public float TextRate { get; init; }

    /// <summary>Whether a blip sounds as a line prints (<see cref="Ui.DialogueBox.Voice"/>), as it does unless the Dialogue volume is 0; the original's box is silent.</summary>
    public bool TextVoice { get; init; }

    /// <summary>
    /// The widths each font's glyphs take in the typeface its text is drawn in, by font record, which the runtime measures and hands
    /// over; none for the sheets' own, as a headless run lays its text out by (<see cref="Ui.TextFonts.Widths"/>).
    /// </summary>
    public IReadOnlyDictionary<AidynRetold.Plugins.RecordId, IReadOnlyList<int>>? TextWidths { get; init; }

    /// <summary>What the core plays by of these settings, at the original's pace.</summary>
    public static SessionSettings From(Settings settings) => new()
    {
        MasterVolume = settings.MasterVolume,
        MusicVolume = settings.MusicVolume,
        AmbientVolume = settings.AmbientVolume,
        EffectsVolume = settings.EffectsVolume,
        InterfaceVolume = settings.InterfaceVolume,
        DialogueVolume = settings.DialogueVolume,
        PrecipitationDensity = settings.PrecipitationDensity,
        SkyPlacement = settings.SkyPlacement,
        SpellAuras = settings.SpellAuras,
        UiLines = Math.Max(settings.UiLines, 1),
        TextRate = Ui.TextReveal.GlyphsPerTick(settings.TextSpeed),
        TextVoice = settings.DialogueVolume > 0,
    };
}
