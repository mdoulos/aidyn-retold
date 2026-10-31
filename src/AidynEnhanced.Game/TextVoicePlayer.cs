using AidynEnhanced.Core.Ui;
using Godot;

namespace AidynEnhanced.Game;

/// <summary>
/// The conversation box's voice (<see cref="TextReveal"/>, the Enhancements page's Text Voice): a short blip for each one the box
/// counts as its line prints (<see cref="DialogueBox.TakeBlips"/>), pitched by the speaker and the line's mood and jittered a
/// little, at the sound volume. The blip is made here rather than read from a plugin: a soft square, a sine with a quarter of its
/// third harmonic, at <see cref="BaseFrequency"/>, rising in 3 ms and decaying away over the mood's time (<see cref="MoodPreset.Decay"/>,
/// a multiple of <see cref="Decay"/>), one stream per decay the moods ask for, each on a player of its own so a blip never cuts the
/// last. It plays beside the core's mixer (<see cref="AudioOutput"/>), which carries the original's sounds alone.
/// </summary>
public sealed partial class TextVoicePlayer : Node
{
    /// <summary>The blip's pitch at a speaker pitch of 1, in Hz.</summary>
    public const float BaseFrequency = 660;

    /// <summary>How long the blip's attack is, and its decay's time constant at a mood's decay of 1, in seconds.</summary>
    private const float Attack = 0.003f, Decay = 0.02f;

    /// <summary>The blip's loudness at full sound volume, so it sits under the game's own sounds.</summary>
    private const float Gain = 0.5f;

    private const int Rate = 44100;

    private readonly Dictionary<float, AudioStreamPlayer> _players = [];
    private uint _seed = 0x2545F491;

    public TextVoicePlayer()
    {
        Name = nameof(TextVoicePlayer);
    }

    /// <summary>
    /// Plays one blip at a speaker's pitch (<see cref="TextReveal.PitchOf"/>) and a volume from 0 to 1, silent at 0, strayed from the
    /// pitch by up to <paramref name="jitter"/> either way, with its decay <paramref name="decay"/> times the usual.
    /// </summary>
    public void Blip(float pitch, float volume, float jitter, float decay)
    {
        if (volume <= 0)
            return;
        _seed = _seed * 1664525u + 1013904223u;
        var stray = ((_seed >> 8) & 0xFFFF) / 65535f * 2 - 1;
        var player = PlayerFor(decay);
        player.VolumeDb = Mathf.LinearToDb(volume * Gain);
        player.PitchScale = Mathf.Max(0.1f, pitch * (1 + jitter * stray));
        player.Play();
    }

    private AudioStreamPlayer PlayerFor(float decay)
    {
        decay = Mathf.Clamp(decay, 0.25f, 4);
        if (!_players.TryGetValue(decay, out var player))
        {
            player = new AudioStreamPlayer { Name = $"Blip {decay:0.00}", MaxPolyphony = 4, Stream = MakeBlip(Decay * decay) };
            _players[decay] = player;
            AddChild(player);
        }
        return player;
    }

    /// <summary>A blip whose decay's time constant is <paramref name="decay"/> seconds, lasting until it has died to about 6%.</summary>
    private static AudioStreamWav MakeBlip(float decay)
    {
        var samples = (int)((Attack + decay * 2.8f) * Rate);
        var data = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var t = (float)i / Rate;
            var phase = Mathf.Tau * BaseFrequency * t;
            var wave = Mathf.Sin(phase) + 0.25f * Mathf.Sin(3 * phase);
            var envelope = Mathf.Min(1, t / Attack) * Mathf.Exp(-t / decay);
            var sample = (short)Mathf.Clamp(wave / 1.25f * envelope * short.MaxValue, short.MinValue, short.MaxValue);
            data[2 * i] = (byte)(sample & 0xFF);
            data[2 * i + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Stereo = false,
            Data = data,
        };
    }
}
