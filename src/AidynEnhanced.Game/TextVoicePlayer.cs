using AidynEnhanced.Core.Ui;
using Godot;

namespace AidynEnhanced.Game;

/// <summary>
/// The conversation box's voice (<see cref="TextReveal"/>, the Enhancements page's Text Voice): a short blip for each one the box
/// counts as its line prints (<see cref="DialogueBox.TakeBlips"/>), pitched by the speaker and jittered a little, at the sound
/// volume. The blip is made here rather than read from a plugin: 55 ms of a soft square, a sine with a quarter of its third
/// harmonic, at <see cref="BaseFrequency"/>, rising in 3 ms and decaying away. It plays beside the core's mixer
/// (<see cref="AudioOutput"/>), which carries the original's sounds alone.
/// </summary>
public sealed partial class TextVoicePlayer : AudioStreamPlayer
{
    /// <summary>The blip's pitch at a speaker pitch of 1, in Hz.</summary>
    public const float BaseFrequency = 660;

    /// <summary>How long the blip lasts, in seconds, and how long its attack and its decay's time constant are.</summary>
    private const float Length = 0.055f, Attack = 0.003f, Decay = 0.02f;

    /// <summary>The blip's loudness at full sound volume, so it sits under the game's own sounds.</summary>
    private const float Gain = 0.5f;

    /// <summary>How far each blip's pitch strays from the speaker's, either way, so a line doesn't drone.</summary>
    private const float Jitter = 0.04f;

    private const int Rate = 44100;

    private uint _seed = 0x2545F491;

    public TextVoicePlayer()
    {
        Name = nameof(TextVoicePlayer);
        MaxPolyphony = 4;
        Stream = MakeBlip();
    }

    /// <summary>Plays one blip at a speaker's pitch (<see cref="TextReveal.PitchOf"/>) and a volume from 0 to 1; silent at 0.</summary>
    public void Blip(float pitch, float volume)
    {
        if (volume <= 0)
            return;
        _seed = _seed * 1664525u + 1013904223u;
        var jitter = ((_seed >> 8) & 0xFFFF) / 65535f * 2 - 1;
        VolumeDb = Mathf.LinearToDb(volume * Gain);
        PitchScale = Mathf.Max(0.1f, pitch * (1 + Jitter * jitter));
        Play();
    }

    private static AudioStreamWav MakeBlip()
    {
        var samples = (int)(Length * Rate);
        var data = new byte[samples * 2];
        for (var i = 0; i < samples; i++)
        {
            var t = (float)i / Rate;
            var phase = Mathf.Tau * BaseFrequency * t;
            var wave = Mathf.Sin(phase) + 0.25f * Mathf.Sin(3 * phase);
            var envelope = Mathf.Min(1, t / Attack) * Mathf.Exp(-t / Decay);
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
