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
/// How a line is printed and sounded under a mood (<see cref="TextReveal.Presets"/>): the moods are mostly tempo and pitch, with the
/// blip's shape and tremble only nudged, since a blip voice tires the ear.
/// </summary>
/// <param name="Rate">How fast the line prints, as a multiple of the speed setting's.</param>
/// <param name="PitchStart">The blips' pitch at the line's start, as a multiple of the speaker's.</param>
/// <param name="PitchEnd">The blips' pitch at the line's end, reached evenly through it: falling reads sad, rising pleading.</param>
/// <param name="Decay">How long each blip rings, as a multiple of the usual: short and clipped is stern, long and soft is sad.</param>
/// <param name="Jitter">How far each blip strays from its pitch, either way: a tremble.</param>
/// <param name="CommaPause">How many ticks the printing holds after a comma, a semicolon or a colon.</param>
/// <param name="StopPause">How many ticks it holds after a full stop, an exclamation mark or a question mark that ends a sentence.</param>
public sealed record MoodPreset(float Rate, float PitchStart, float PitchEnd, float Decay, float Jitter, int CommaPause, int StopPause);

/// <summary>The waveform a voice tone's blip is made of.</summary>
public enum Waveform
{
    /// <summary>A sine with a quarter of its third harmonic: the plain blip.</summary>
    Soft,
    Sine,
    Square,
    Saw,
    Triangle,
}

/// <summary>
/// A speaker's tone (<see cref="TextReveal.Tones"/>): what the blip is made of and how it rises and dies, which sets who is speaking
/// where the mood sets how. A hard, instant attack and a short ring are clipped and precise; a slow swell and a long ring are
/// gentle; a square is bright, a saw buzzy, a triangle hollow, and noise makes it gravelly.
/// </summary>
/// <param name="Name">The name a record's <c>voiceTone</c> gives.</param>
/// <param name="Attack">How long the blip takes to rise, in seconds.</param>
/// <param name="Decay">How long it rings, as a multiple of the usual, which the mood's multiplies further.</param>
/// <param name="Noise">How much of the blip is noise, 0 to 1.</param>
/// <param name="Gain">Its loudness, as a multiple of the usual.</param>
/// <param name="Mix">How much of the blip is the waveform, the rest the plain soft square: 1 for all of it.</param>
/// <param name="Lilt">How far every other blip is raised and the ones between lowered, as a share of the pitch: a sing-song.</param>
/// <param name="Pitch">A multiple on the speaker's pitch: a tone can sit lower or higher than the voice and height give.</param>
public sealed record VoiceTone(string Name, Waveform Waveform, float Attack, float Decay, float Noise, float Gain, float Mix = 1, float Lilt = 0, float Pitch = 1);

/// <summary>
/// The port's typewriter reveal and its voice, the user's choice (docs/systems/ui.md, the conversation box): a line prints
/// a few glyphs a tick instead of showing whole, A completes it, and a short blip sounds as glyphs appear, pitched by the
/// speaker, in the manner of Golden Sun. The original shows a line whole and plays no sound for it, which Text Speed's
/// Original and Text Voice's Off keep. <see cref="DialogueBox"/> counts the glyphs and the blips, and the runtime plays them.
/// A line is said in a mood (<see cref="MoodPreset"/>): its node's (<see cref="DialogueNode.Mood"/>), else one guessed from its
/// text (<see cref="Guess"/>).
/// </summary>
public static class TextReveal
{
    /// <summary>How each mood prints and sounds. Neutral is the plain typewriter, with the slightest hold at punctuation.</summary>
    public static readonly IReadOnlyDictionary<TextMood, MoodPreset> Presets = new Dictionary<TextMood, MoodPreset>
    {
        [TextMood.Neutral] = new(Rate: 1, PitchStart: 1, PitchEnd: 1, Decay: 1, Jitter: 0.04f, CommaPause: 3, StopPause: 8),
        [TextMood.Stern] = new(Rate: 1.25f, PitchStart: 0.95f, PitchEnd: 0.95f, Decay: 0.6f, Jitter: 0.01f, CommaPause: 2, StopPause: 10),
        [TextMood.Pleading] = new(Rate: 1.15f, PitchStart: 1, PitchEnd: 1.12f, Decay: 1.2f, Jitter: 0.08f, CommaPause: 6, StopPause: 6),
        [TextMood.Sad] = new(Rate: 0.7f, PitchStart: 1, PitchEnd: 0.88f, Decay: 1.6f, Jitter: 0.03f, CommaPause: 8, StopPause: 16),
        [TextMood.Glad] = new(Rate: 1.2f, PitchStart: 1.05f, PitchEnd: 1.1f, Decay: 0.9f, Jitter: 0.06f, CommaPause: 2, StopPause: 6),
    };

    /// <summary>The words that mark a line sad, pleading or glad when its punctuation doesn't settle it (<see cref="Guess"/>), matched whole, in any case.</summary>
    private static readonly string[] SadWords = ["sorry", "alas", "dead", "died", "dying", "death", "lost", "gone", "grief", "mourn", "weep", "forgive", "never"];
    private static readonly string[] PleadingWords = ["please", "beg", "help", "mercy", "spare", "must", "need"];
    private static readonly string[] GladWords = ["thank", "thanks", "wonderful", "glad", "welcome", "splendid", "excellent", "happy", "joy", "delighted"];

    /// <summary>The mood a line is said in: its node's, else <see cref="Guess"/>ed from its text.</summary>
    public static TextMood MoodOf(string text, TextMood? tagged) => tagged ?? Guess(text);

    /// <summary>
    /// Guesses how a line is said from its text alone, for a node no mod has tagged: shouting (a word of two or more capitals,
    /// not a name's initials or "I") or an exclamation mark is stern, unless the line also asks or pleads; an ellipsis is sad; a
    /// question mark, or a pleading word, is pleading; a sad or a glad word is sad or glad; else neutral.
    /// </summary>
    public static TextMood Guess(string text)
    {
        var words = Words(text);
        var shouts = words.Any(w => w.Length >= 2 && w.All(char.IsUpper) && w.Any(char.IsLetter));
        var exclaims = text.Contains('!');
        var asks = text.Contains('?');
        var trailsOff = text.Contains("...") || text.Contains('…');
        var lower = words.Select(w => w.ToLowerInvariant()).ToHashSet();
        var pleads = asks || lower.Overlaps(PleadingWords);
        if (shouts || exclaims)
            return pleads && !shouts ? TextMood.Pleading : TextMood.Stern;
        if (trailsOff)
            return TextMood.Sad;
        if (pleads)
            return TextMood.Pleading;
        if (lower.Overlaps(SadWords))
            return TextMood.Sad;
        if (lower.Overlaps(GladWords))
            return TextMood.Glad;
        return TextMood.Neutral;
    }

    /// <summary>The pitch a mood gives a blip, as a multiple of the speaker's, <paramref name="progress"/> of the way through the line (0 to 1).</summary>
    public static float PitchAt(MoodPreset mood, float progress) =>
        mood.PitchStart + (mood.PitchEnd - mood.PitchStart) * Math.Clamp(progress, 0, 1);

    private static List<string> Words(string text)
    {
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var letter = i < text.Length && (char.IsLetter(text[i]) || text[i] == '\'');
            if (letter && start < 0)
                start = i;
            else if (!letter && start >= 0)
            {
                words.Add(text[start..i].Trim('\''));
                start = -1;
            }
        }
        return words;
    }

    /// <summary>How many ticks pass at least between two blips, so fast text doesn't buzz: 20 a second at most.</summary>
    public const int TicksBetweenBlips = 3;

    /// <summary>The height, in scene units, of a speaker whose voice is pitched at 1: about Alaron's.</summary>
    public const float ReferenceHeight = 1.8f;

    /// <summary>The pitch a speaker's height gives at most and at least, as a multiple of the blip's own.</summary>
    public const float LowestPitch = 0.7f, HighestPitch = 1.5f;

    /// <summary>A man's and a woman's base pitch, which the height then scales (<see cref="PitchOf"/>), the user's choice: men well below the blip's own.</summary>
    public const float MalePitch = 0.72f, FemalePitch = 1.15f;

    /// <summary>A big man's pitch, the guards' and the giants', which the height doesn't scale: as deep as each other, the user's choice.</summary>
    public const float DeepPitch = 0.5f;

    /// <summary>
    /// The named tones a record's <c>voiceTone</c> picks from, the user's choice by ear (Oct 30, 2026). Plain is the blip as it was.
    /// Soldier is clipped and precise, the guards' and Abrecan's. Soft is gentle; feminine is soft at a quarter of its strength,
    /// Brenna's. Sultry is smooth, slow, a little breathy and lower, Keelin's. Sly is short and perky with a sing-song, Dougal's.
    /// Grand is big: full, loud, lower and ringing long, Donovan's. Bright, hollow and gravel (a twelfth noise) are tried on the
    /// rest of the party for comparing.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, VoiceTone> Tones = new[]
    {
        new VoiceTone("plain", Waveform.Soft, Attack: 0.003f, Decay: 1, Noise: 0, Gain: 1),
        new VoiceTone("soldier", Waveform.Square, Attack: 0.0004f, Decay: 0.65f, Noise: 0, Gain: 0.9f),
        new VoiceTone("soft", Waveform.Sine, Attack: 0.009f, Decay: 1.35f, Noise: 0, Gain: 0.85f),
        new VoiceTone("feminine", Waveform.Sine, Attack: 0.0045f, Decay: 1.09f, Noise: 0, Gain: 0.96f, Mix: 0.25f),
        new VoiceTone("sultry", Waveform.Sine, Attack: 0.012f, Decay: 1.6f, Noise: 0.06f, Gain: 0.9f, Mix: 0.9f, Lilt: 0.02f, Pitch: 0.9f),
        new VoiceTone("sly", Waveform.Square, Attack: 0.001f, Decay: 0.6f, Noise: 0, Gain: 0.85f, Mix: 0.6f, Lilt: 0.07f),
        new VoiceTone("grand", Waveform.Square, Attack: 0.002f, Decay: 1.9f, Noise: 0, Gain: 1.7f, Mix: 0.7f, Pitch: 0.88f),
        new VoiceTone("bright", Waveform.Square, Attack: 0.002f, Decay: 1, Noise: 0, Gain: 0.8f),
        new VoiceTone("hollow", Waveform.Triangle, Attack: 0.004f, Decay: 1.2f, Noise: 0, Gain: 1),
        new VoiceTone("gravel", Waveform.Soft, Attack: 0.003f, Decay: 0.9f, Noise: 0.09f, Gain: 1),
    }.ToDictionary(t => t.Name);

    /// <summary>The tone every speaker has unless its record names another.</summary>
    public static VoiceTone PlainTone => Tones["plain"];

    /// <summary>A speaker's tone: its record's <see cref="DbRecord.VoiceTone"/> if it names one of <see cref="Tones"/>, else plain.</summary>
    public static VoiceTone ToneOf(DbRecord? speaker) =>
        speaker?.VoiceTone is { } name && Tones.TryGetValue(name, out var tone) ? tone : PlainTone;

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
    /// one; <see cref="DeepPitch"/> for a big man's voice, whatever his height; else its <see cref="DbRecord.Voice"/>'s base,
    /// <see cref="MalePitch"/> or <see cref="FemalePitch"/>, or 1 for a creature, times its body's height's factor, the taller the
    /// deeper: the square root of <see cref="ReferenceHeight"/> over its scaled height, within <see cref="LowestPitch"/> and
    /// <see cref="HighestPitch"/>, or 1 without a body. 1 for no speaker.
    /// </summary>
    public static float PitchOf(DbRecord? speaker)
    {
        if (speaker is null)
            return 1;
        if (speaker.VoicePitch is { } pitch)
            return pitch;
        if (speaker.Voice == Voice.Deep)
            return DeepPitch;
        var voice = speaker.Voice switch
        {
            Voice.Male => MalePitch,
            Voice.Female => FemalePitch,
            _ => 1,
        };
        if (speaker.Body is not { } body || body.Height * body.Scale <= 0)
            return voice;
        return voice * Math.Clamp(MathF.Sqrt(ReferenceHeight / (body.Height * body.Scale)), LowestPitch, HighestPitch);
    }
}
