using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Audio;

/// <summary>A sample as an instrument plays it, with its loop.</summary>
/// <param name="Samples">16-bit mono PCM.</param>
/// <param name="Loop">Where it loops for ever; null if it plays once.</param>
/// <param name="Gain">What its samples are multiplied by: 1 for the original's, a recording's levelling for a recording's.</param>
public sealed record Instrument(short[] Samples, SampleLoop? Loop, float Gain = 1);

/// <summary>
/// A sequence ready to play: a <see cref="SequenceRecord"/> with its events and samples loaded, or a recording in its place.
/// </summary>
/// <param name="Stream">The event stream (docs/formats/audio.md).</param>
public sealed record Sequence(int Tracks, IReadOnlyList<Instrument> Instruments, byte[] Stream, int LoopPosition = 0)
{
    /// <summary>
    /// How loud a recording plays at full volume, as the root mean square of its samples on each side: the original's music's,
    /// 0x05CE's, Gwernia's, over a play at full volume through the engine (-28.6 dB of full scale), so a recording sits under
    /// the sound effects as the music it replaces does.
    /// </summary>
    public const float RecordingLoudness = 1217;

    /// <summary>
    /// How often a recording's rows come: tempo 250 (docs/formats/audio.md), every 10 of the sequencer's milliseconds, which is
    /// as close as it comes back round to its start.
    /// </summary>
    private const byte RecordingTempo = 250, RowCalls = 125000 / (RecordingTempo * 25 * 2);

    /// <summary>
    /// Loads <paramref name="id"/>'s record, its <c>.dcm</c> events and its instruments' WAVs, or a recording a plugin has in its
    /// place, the sequence's own <c>.wav</c> (<see cref="Recording"/>).
    /// Samples several sequences share are read once per <paramref name="samples"/> cache.
    /// </summary>
    public static Sequence Load(PluginSet plugins, RecordId id, Dictionary<RecordId, short[]>? samples = null)
    {
        if (plugins.FindAsset(id, "wav") is { } recording)
        {
            var (channels, rate) = Wav.DecodeChannels(File.ReadAllBytes(recording));
            return Recording(channels, rate);
        }
        var record = plugins.Get<SequenceRecord>(id);
        var events = plugins.FindAsset(id, "dcm") ?? throw new FileNotFoundException($"{id} has no .dcm asset.");
        samples ??= [];
        var instruments = record.Instruments.Select(i =>
        {
            if (!samples.TryGetValue(i.Sample, out var pcm))
            {
                var wav = plugins.FindAsset(i.Sample, "wav") ?? throw new FileNotFoundException($"{i.Sample} has no .wav asset.");
                samples[i.Sample] = pcm = Wav.Decode(File.ReadAllBytes(wav));
            }
            return new Instrument(pcm, i.Loop);
        }).ToList();
        return new Sequence(record.Tracks, instruments, File.ReadAllBytes(events), record.LoopPosition);
    }

    /// <summary>
    /// A recording played as a sequence, the user's choice (Oct 31, 2026): a track for each channel, the left panned hard left
    /// and the right hard right (a mono one's centred), playing its samples once at its own rate at full volume, and rows of
    /// nothing until it's done, so it loops as the sequence does, 10 to 20 ms after it ends. Played where a map object, the chunk or a
    /// script plays the sequence, it's as loud and panned as the sequence would be there, levelled to
    /// <see cref="RecordingLoudness"/>.
    /// </summary>
    /// <param name="channels">16-bit PCM, one array a channel, one or two of them.</param>
    /// <param name="rate">Its sample rate in Hz, at most 65,535.</param>
    public static Sequence Recording(IReadOnlyList<short[]> channels, int rate)
    {
        if (channels.Count is not (1 or 2))
            throw new ArgumentException("A recording is mono or stereo.", nameof(channels));
        byte[] pans = channels.Count == 1 ? [0x80] : [0x00, 0xFF];
        // How loud each side plays at full volume before the levelling, averaged over the two: each channel's on its side, or a
        // mono one's on both.
        double loudness = 0;
        for (var c = 0; c < channels.Count; c++)
        {
            var (left, right) = AudioEngine.Gains(AudioEngine.FullVolume, pans[c]);
            loudness += Rms(channels[c]) * (left + right) / 2;
        }
        var gain = loudness > 0 ? (float)(RecordingLoudness / loudness) : 1;

        var stream = new List<byte>();
        var pitch = EncodePitch(rate);
        for (var c = 0; c < channels.Count; c++)
        {
            // Pitch, volume (225, whose square root is 15), pan and instrument, and a note; the first track sets the tempo.
            stream.Add((byte)(0x40 | 0x20 | 0x10 | 0x08 | 0x02 | (c == 0 ? 0x01 : 0)));
            stream.AddRange([(byte)pitch, (byte)(pitch >> 8), 225, pans[c], (byte)c]);
            if (c == 0)
                stream.AddRange([0x40, RecordingTempo]);
        }
        // Rows of nothing, the last read once the samples have played: a row comes every RowCalls of the sequencer's 44-sample calls.
        var length = channels.Max(c => c.Length);
        var calls = (double)length / rate * AudioEngine.OriginalRate / AudioEngine.StepSamples;
        var rows = (int)Math.Ceiling(calls / RowCalls);
        stream.AddRange(new byte[rows * channels.Count]);
        return new Sequence(channels.Count, [.. channels.Select(c => new Instrument(c, null, gain))], [.. stream]);
    }

    private static double Rms(short[] samples)
    {
        double sum = 0;
        foreach (var s in samples)
            sum += (double)s * s;
        return samples.Length == 0 ? 0 : Math.Sqrt(sum / samples.Length);
    }

    /// <summary>A rate in Hz as a pitch event's word: the finest base and shift that reach it (docs/formats/audio.md).</summary>
    private static ushort EncodePitch(int rate)
    {
        int[] bases = [0x0000, 0x4000, 0xC000, 0x1C000];
        for (var shift = 0; shift < bases.Length; shift++)
        {
            if (rate >= bases[shift] && (rate - bases[shift]) >> shift <= 0x3FFF)
                return (ushort)(shift << 14 | (rate - bases[shift]) >> shift);
        }
        throw new ArgumentOutOfRangeException(nameof(rate), rate, "A recording's rate is at most 65,535 Hz.");
    }
}
