using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>How fast the conversation box prints a line (the Enhancements page's Text Speed).</summary>
public enum TextSpeed
{
    /// <summary>The whole line at once, as the original shows it.</summary>
    Original,
    /// <summary>Half a glyph a tick: 30 a second.</summary>
    Slow,
    /// <summary>A glyph a tick: 60 a second.</summary>
    Normal,
    /// <summary>Two glyphs a tick: 120 a second.</summary>
    Fast,
}

/// <summary>
/// The port's typewriter reveal and its voice, the user's choice (docs/systems/ui.md, the conversation box): a line prints
/// a few glyphs a tick instead of showing whole, A completes it, and a short blip sounds as glyphs appear, pitched by the
/// speaker, in the manner of Golden Sun. The original shows a line whole and plays no sound for it, which Text Speed's
/// Original and Text Voice's Off keep. <see cref="DialogueBox"/> counts the glyphs and the blips, and the runtime plays them.
/// </summary>
public static class TextReveal
{
    /// <summary>How many ticks pass at least between two blips, so fast text doesn't buzz: 20 a second at most.</summary>
    public const int TicksBetweenBlips = 3;

    /// <summary>The height, in scene units, of a speaker whose voice is pitched at 1: about Alaron's.</summary>
    public const float ReferenceHeight = 1.8f;

    /// <summary>The pitch a speaker's height gives at most and at least, as a multiple of the blip's own.</summary>
    public const float LowestPitch = 0.7f, HighestPitch = 1.5f;

    /// <summary>How many glyphs a tick a speed prints: 0 for the whole line at once.</summary>
    public static float GlyphsPerTick(TextSpeed speed) => speed switch
    {
        TextSpeed.Slow => 0.5f,
        TextSpeed.Normal => 1,
        TextSpeed.Fast => 2,
        _ => 0,
    };

    /// <summary>
    /// A speaker's voice's pitch, as a multiple of the blip's own: the record's <see cref="DbRecord.VoicePitch"/> if a mod gave it
    /// one, else by its body's height, the taller the deeper, as the square root of <see cref="ReferenceHeight"/> over its scaled
    /// height, within <see cref="LowestPitch"/> and <see cref="HighestPitch"/>; 1 for no speaker or one without a body.
    /// </summary>
    public static float PitchOf(DbRecord? speaker)
    {
        if (speaker?.VoicePitch is { } pitch)
            return pitch;
        if (speaker?.Body is not { } body || body.Height * body.Scale <= 0)
            return 1;
        return Math.Clamp(MathF.Sqrt(ReferenceHeight / (body.Height * body.Scale)), LowestPitch, HighestPitch);
    }
}
