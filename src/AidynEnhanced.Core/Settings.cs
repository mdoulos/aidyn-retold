using System.Text.Json;
using System.Text.Json.Serialization;
using AidynEnhanced.Core.Sky;
using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;

namespace AidynEnhanced.Core;

/// <summary>
/// The player's settings, which the options menu changes and the runtime carries out: the original's
/// volumes, and the port's graphics and fidelity settings, which live in the same menu rather than in a
/// launcher. They're the player's, not the game's, so a save doesn't hold them: they're kept in
/// <c>settings.json</c> in the data folder (<see cref="DataPaths.Settings"/>). The original writes its volumes and
/// resolution into every save (<c>QuestData::Save</c>), so loading a game set them back.
/// </summary>
/// <remarks>
/// Only the options menu (<see cref="Ui.OptionsMenu"/>) and the runtime read these. The core plays by what the runtime makes
/// of them for each tick, with the command line's for the run over them (<see cref="SessionSettings"/>, ADR 0023), and the
/// runtime draws by the rest.
/// </remarks>
public sealed class Settings
{
    /// <summary>
    /// The volumes' steps, from silent to full: the original's are floats from 0 to 1 that the stick steps by
    /// 0.01 (<c>WidgetOptionsConfig</c>), which these are in hundredths.
    /// </summary>
    public const int MaxVolume = 100;

    /// <summary>
    /// Every sound's volume, which each group's is a share of (<see cref="Audio.SoundGroup"/>): the port's, the user's choice (Oct 31,
    /// 2026), where the original has only the sound effects' and the music's.
    /// </summary>
    public int MasterVolume { get; set; } = MaxVolume;

    /// <summary>The music's volume (<c>VolBGM</c>): 0.65 in a new game.</summary>
    public int MusicVolume { get; set; } = 65;

    /// <summary>The world's own sounds' volume: its placed sounds, and the rain and thunder.</summary>
    public int AmbientVolume { get; set; } = MaxVolume;

    /// <summary>The sound effects' volume: bodies' footsteps, voices, weapons and casts, and what the world and scripts sound.</summary>
    public int EffectsVolume { get; set; } = MaxVolume;

    /// <summary>The menus' sounds' volume.</summary>
    public int InterfaceVolume { get; set; } = MaxVolume;

    /// <summary>The voices' volume: a conversation's blip as a line prints, which is silent at 0.</summary>
    public int DialogueVolume { get; set; } = MaxVolume;

    /// <summary>
    /// Older settings' one sound volume (<c>VolSFX</c>), which every group but the music's had: read as theirs, and never written.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SoundVolume
    {
        get => null;
        set
        {
            if (value is { } volume)
                AmbientVolume = EffectsVolume = InterfaceVolume = DialogueVolume = volume;
        }
    }

    /// <summary>How many lines tall the UI screen is: the original's 240, or more for smaller UI with room for more.</summary>
    public int UiLines { get; set; } = 240;

    /// <summary>
    /// How many lines the 3D world is drawn at, scaled up to the window: the original's 240 or its Expansion Pak's 480, or 0 to draw
    /// it at <see cref="RenderScale"/>.
    /// </summary>
    public int WorldLines { get; set; }

    /// <summary>
    /// The 3D world's size as a part of the window's (or the screen's, in fullscreen), when <see cref="WorldLines"/> is 0: under 1
    /// scaled up, over 1 scaled down, supersampling.
    /// </summary>
    public float RenderScale { get; set; } = 1;

    /// <summary>Older settings' supersampling, at twice the window's resolution: read as a render scale of 2, and never written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Supersampled
    {
        get => false;
        set
        {
            if (value)
                RenderScale = 2;
        }
    }

    /// <summary>Multisample anti-aliasing's samples: 0 (off), 2, 4 or 8.</summary>
    public int Antialiasing { get; set; }

    /// <summary>Borderless fullscreen at the screen's own resolution, or a window.</summary>
    public bool Fullscreen { get; set; }

    /// <summary>
    /// The window's size while it's windowed and not maximized, or 0 × 0 for whatever size it opens at. The runtime keeps it as the
    /// player resizes the window by hand, too.
    /// </summary>
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    /// <summary>Whether the window is maximized, by the options or by hand: <see cref="WindowWidth"/> × <see cref="WindowHeight"/> is then its size restored.</summary>
    public bool Maximized { get; set; }

    public bool VSync { get; set; } = true;

    /// <summary><see cref="FrameRateLimit"/>'s value for the screen's own refresh rate.</summary>
    public const int DisplayRate = -1;

    /// <summary>The most frames drawn a second: a number, <see cref="DisplayRate"/> for the screen's refresh rate, or 0 for no limit.</summary>
    public int FrameRateLimit { get; set; } = DisplayRate;

    // The world's detail, which Graphic Quality sets all at once (GraphicQuality), Medium's in new settings.

    /// <summary>Whether the Overworld's terrain is drawn out to the 5 × 5 chunks around the player rather than the original's 3 × 3.</summary>
    public bool ExtendedDrawDistance { get; set; } = true;

    /// <summary>How far scene objects are drawn, as a multiple of the original's distances.</summary>
    public float ObjectDistance { get; set; } = 2;

    /// <summary>Whether scene objects always draw their nearest, most detailed level.</summary>
    public bool NearestDetail { get; set; }

    /// <summary>
    /// Whether a model that brings a normal map, as a mod's remodelled character does, is lit per pixel through it rather than per
    /// vertex as the original's models are. None of the original's models has one, so only a mod's look differently.
    /// </summary>
    public bool SurfaceDetail { get; set; } = true;

    /// <summary>
    /// Whether a model with a skeleton, as a mod's remodelled character has, blends into each new animation rather than cutting to it
    /// as the original's models do. None of the original's models has a skeleton, so only a mod's look different.
    /// </summary>
    public bool AnimationBlending { get; set; } = true;

    /// <summary>How many raindrops and snowflakes fall, as a multiple of the original's (<see cref="Particles.WeatherParticles.Density"/>).</summary>
    public float PrecipitationDensity { get; set; } = 4;

    /// <summary>
    /// The Graphic Quality last chosen, which the menus' look goes by (<see cref="OriginalLook"/>): set by hand, the world's detail
    /// settings leave it as it was, the user's choice (Nov 2, 2026), and the Video page shows Custom. Medium in new settings, and in older
    /// ones, which had none, so their menus stay as they were.
    /// </summary>
    public GraphicQuality GraphicQuality { get; set; } = GraphicQuality.Medium;

    /// <summary>
    /// Whether the game looks as the original did, Graphic Quality's Original, the user's choice (Nov 2, 2026): the menus' layout, text
    /// and art the original's, as well as the world's detail.
    /// </summary>
    [JsonIgnore]
    public bool OriginalLook => GraphicQuality == GraphicQuality.Original;

    /// <summary>The Graphic Quality the world's detail settings make together, or null where they've been set by hand to none of them.</summary>
    [JsonIgnore]
    public GraphicQuality? Quality => Enum.GetValues<GraphicQuality>().Cast<GraphicQuality?>().FirstOrDefault(q => Matches(Detail(q!.Value)));

    /// <summary>Chooses a Graphic Quality: the menus' look, and the world's detail settings all set to its.</summary>
    public void SetQuality(GraphicQuality quality)
    {
        GraphicQuality = quality;
        (ExtendedDrawDistance, ObjectDistance, NearestDetail, SurfaceDetail, AnimationBlending, PrecipitationDensity) = Detail(quality);
    }

    /// <summary>
    /// What each Graphic Quality sets, the user's design (Nov 2, 2026): Original the original's look, Medium a step past it on each, and
    /// High each at its most.
    /// </summary>
    private static (bool Extended, float Objects, bool Nearest, bool Surface, bool Blending, float Precipitation) Detail(GraphicQuality quality) => quality switch
    {
        GraphicQuality.Original => (false, 1, false, false, false, 1),
        GraphicQuality.Medium => (true, 2, false, true, true, 4),
        _ => (true, 3, true, true, true, Particles.WeatherParticles.MaxDensity),
    };

    private bool Matches((bool Extended, float Objects, bool Nearest, bool Surface, bool Blending, float Precipitation) detail) =>
        detail == (ExtendedDrawDistance, ObjectDistance, NearestDetail, SurfaceDetail, AnimationBlending, PrecipitationDensity);

    /// <summary>How the sky places its objects: no longer on the options menu, the user's choice, but kept for F6 and <c>--sky</c>.</summary>
    public SkyPlacement SkyPlacement { get; set; } = SkyPlacement.Projected;

    /// <summary>
    /// How fast the conversation box prints a line (<see cref="Ui.TextReveal"/>): whole at once as the original's, or a few glyphs a
    /// tick. Normal, the user's choice.
    /// </summary>
    public Ui.TextSpeed TextSpeed { get; set; } = Ui.TextSpeed.Normal;

    /// <summary>Older settings' switch for the conversation's blip, now the Dialogue volume at 0: read, and never written.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? TextVoice
    {
        get => null;
        set
        {
            if (value == false)
                DialogueVolume = 0;
        }
    }

    /// <summary>
    /// Whether the title starts with the original's intro cards (<see cref="Ui.IntroCards"/>): the Expansion Pak's, the legal text and
    /// the N64's, THQ's and H2O's logos. Off, the user's choice, it starts at its logo and menu.
    /// </summary>
    public bool IntroCards { get; set; }

    /// <summary>
    /// How the lasting spells' and potions' scenes over heads take their turns in a battle: one at a time across the battle, as the
    /// original's, or each character's at once (<see cref="Combat.SpellFx.Turns"/>).
    /// </summary>
    public Combat.AuraTurns SpellAuras { get; set; } = Combat.AuraTurns.Battle;

    /// <summary>A setting changed.</summary>
    public event Action? Changed;

    /// <summary>Says a setting changed, for whoever carries them out.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>A copy of these settings, without whoever hears of this one's changes.</summary>
    public Settings Copy()
    {
        var copy = (Settings)MemberwiseClone();
        copy.Changed = null;
        copy.Controls = Controls.Copy();
        return copy;
    }

    /// <summary>The settings in a file, or the defaults if there's none or it can't be read.</summary>
    public static Settings Load(string path)
    {
        try
        {
            var settings = File.Exists(path) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), PluginJson.Options) ?? new() : new();
            // Older settings' line counts past 480 (720, 1080 and so on) were the window's own or under it: the window's own now.
            if (settings.WorldLines is not (0 or 240 or 480))
                settings.WorldLines = 0;
            return settings;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } folder)
            Directory.CreateDirectory(folder);
        File.WriteAllText(path, JsonSerializer.Serialize(this, PluginJson.Options) + "\n");
    }

    /// <summary>The player's bindings, on the keyboard and mouse and on a controller (the options' Controls page, ADR 0026).</summary>
    public AidynEnhanced.Core.Controls.ControlBindings Controls { get; set; } = new();

    /// <summary>How far the mouse turns the camera, as a multiple of the drag the runtime always had.</summary>
    public float MouseSensitivity { get; set; } = 1;
}

/// <summary>
/// The Video page's Graphic Quality, which sets the world's detail settings together (<see cref="Settings.SetQuality"/>), each of which
/// the Advanced Graphic Settings page sets alone.
/// </summary>
public enum GraphicQuality
{
    /// <summary>The original's draw distances, models, lighting and rain, and its menus (<see cref="Settings.OriginalLook"/>).</summary>
    Original,

    Medium,

    High,
}
