using AidynEnhanced.Core.Ui;
using Godot;

namespace AidynEnhanced.Game;

/// <summary>
/// The conversation box's voice (<see cref="TextReveal"/>, the Enhancements page's Text Voice): a short blip for each one the box
/// counts as its line prints (<see cref="DialogueBox.TakeBlips"/>), pitched by the speaker and the line's mood and jittered a
/// little, at the sound volume. The blip is made here rather than read from a plugin, in the speaker's tone
/// (<see cref="VoiceTone"/>): its waveform at <see cref="BaseFrequency"/>, with noise if the tone has it, rising over the tone's
/// attack and decaying away over the tone's and the mood's time (a multiple of <see cref="Decay"/>). There's one stream per tone and
/// decay asked for, each on a player of its own so a blip never cuts the last. It plays beside the core's mixer
/// (<see cref="AudioOutput"/>), which carries the original's sounds alone.
/// </summary>
public sealed partial class TextVoicePlayer : Node
{
    /// <summary>The blip's pitch at a speaker pitch of 1, in Hz.</summary>
    public const float BaseFrequency = 660;

    /// <summary>The decay's time constant at a decay of 1, in seconds.</summary>
    private const float Decay = 0.02f;

    /// <summary>The blip's loudness at full sound volume and a tone gain of 1, so it sits under the game's own sounds.</summary>
    private const float Gain = 0.5f;

    private const int Rate = 44100;

    private readonly Dictionary<(string Tone, float Decay), AudioStreamPlayer> _players = [];
    private uint _seed = 0x2545F491;

    public TextVoicePlayer()
    {
        Name = nameof(TextVoicePlayer);
    }

    /// <summary>
    /// Plays one blip in a tone, at a speaker's pitch (<see cref="TextReveal.PitchOf"/>) and a volume from 0 to 1, silent at 0,
    /// strayed from the pitch by up to <paramref name="jitter"/> either way, with its decay <paramref name="decay"/> times the tone's.
    /// </summary>
    public void Blip(VoiceTone tone, float pitch, float volume, float jitter, float decay)
    {
        if (volume <= 0)
            return;
        _seed = _seed * 1664525u + 1013904223u;
        var stray = ((_seed >> 8) & 0xFFFF) / 65535f * 2 - 1;
        var player = PlayerFor(tone, decay);
        player.VolumeDb = Mathf.LinearToDb(volume * Gain * tone.Gain);
        player.PitchScale = Mathf.Max(0.1f, pitch * (1 + jitter * stray));
        player.Play();
    }

    private AudioStreamPlayer PlayerFor(VoiceTone tone, float decay)
    {
        decay = Mathf.Clamp(decay * tone.Decay, 0.25f, 4);
        var key = (tone.Name, decay);
        if (!_players.TryGetValue(key, out var player))
        {
            player = new AudioStreamPlayer { Name = $"Blip {tone.Name} {decay:0.00}", MaxPolyphony = 4, Stream = MakeBlip(tone, Decay * decay) };
            _players[key] = player;
            AddChild(player);
        }
        return player;
    }

    /// <summary>A blip in a tone whose decay's time constant is <paramref name="decay"/> seconds, lasting until it has died to about 6%.</summary>
    private static AudioStreamWav MakeBlip(VoiceTone tone, float decay)
    {
        var samples = (int)((tone.Attack + decay * 2.8f) * Rate);
        var data = new byte[samples * 2];
        uint noise = 0x9E3779B9;
        for (var i = 0; i < samples; i++)
        {
            var t = (float)i / Rate;
            var phase = Mathf.Tau * BaseFrequency * t;
            var wave = Wave(tone.Waveform, phase);
            if (tone.Noise > 0)
            {
                noise = noise * 1664525u + 1013904223u;
                var hiss = ((noise >> 8) & 0xFFFF) / 65535f * 2 - 1;
                wave = wave * (1 - tone.Noise) + hiss * tone.Noise;
            }
            var envelope = Mathf.Min(1, t / Mathf.Max(tone.Attack, 1e-5f)) * Mathf.Exp(-t / decay);
            var sample = (short)Mathf.Clamp(wave * envelope * short.MaxValue, short.MinValue, short.MaxValue);
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

    /// <summary>A waveform's sample at a phase, within −1 and 1: the soft square is a sine with a quarter of its third harmonic.</summary>
    private static float Wave(Waveform waveform, float phase)
    {
        var cycle = phase / Mathf.Tau % 1;
        return waveform switch
        {
            Waveform.Sine => Mathf.Sin(phase),
            Waveform.Square => cycle < 0.5f ? 0.7f : -0.7f,
            Waveform.Saw => (cycle * 2 - 1) * 0.7f,
            Waveform.Triangle => (cycle < 0.5f ? cycle * 4 - 1 : 3 - cycle * 4) * 0.9f,
            _ => (Mathf.Sin(phase) + 0.25f * Mathf.Sin(3 * phase)) / 1.25f,
        };
    }
}
