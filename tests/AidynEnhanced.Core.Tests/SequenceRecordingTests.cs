using AidynEnhanced.Core.Audio;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Assets;

namespace AidynEnhanced.Core.Tests;

/// <summary>A recording played in a sequence's place (<see cref="Sequence.Recording"/>).</summary>
public class SequenceRecordingTests
{
    private const int Rate = 44100;

    private static short[] Constant(short value, double seconds) => [.. Enumerable.Repeat(value, (int)(Rate * seconds))];

    /// <summary>Renders so many seconds of a sequence played at full volume in the middle, each side's samples apart.</summary>
    private static (short[] Left, short[] Right) Render(Sequence sequence, double seconds, bool loop = false)
    {
        var engine = new AudioEngine(Rate);
        engine.Play(sequence, 0xFF, 0x80, loop);
        var stereo = new short[(int)(Rate * seconds) * 2];
        engine.Render(stereo);
        return ([.. stereo.Where((_, i) => i % 2 == 0)], [.. stereo.Where((_, i) => i % 2 == 1)]);
    }

    private static double Rms(ReadOnlySpan<short> samples)
    {
        double sum = 0;
        foreach (var s in samples)
            sum += (double)s * s;
        return Math.Sqrt(sum / samples.Length);
    }

    /// <summary>
    /// A stereo recording's left plays on the left and its right on the right, levelled so the two sides together are as loud as
    /// the original's music.
    /// </summary>
    [Fact]
    public void AStereoRecordingPlaysEachChannelOnItsSideLevelledToTheMusic()
    {
        var recording = Sequence.Recording([Constant(8000, 1), Constant(-4000, 1)], Rate);
        Assert.Equal(2, recording.Tracks);

        var (left, right) = Render(recording, 0.8);
        // Past the start's glide.
        var (l, r) = (Rms(left.AsSpan(Rate / 10)), Rms(right.AsSpan(Rate / 10)));
        Assert.Equal(2, l / r, 0.01);
        Assert.Equal(Sequence.RecordingLoudness, (l + r) / 2, Sequence.RecordingLoudness * 0.01);
        Assert.True(right[Rate / 2] < 0);
    }

    /// <summary>A mono recording plays in the middle, as the original's middle is, a step to the right of even.</summary>
    [Fact]
    public void AMonoRecordingPlaysInTheMiddle()
    {
        var (left, right) = Render(Sequence.Recording([Constant(6000, 1)], Rate), 0.5);

        var (l, r) = (Rms(left.AsSpan(Rate / 10)), Rms(right.AsSpan(Rate / 10)));
        Assert.Equal(1, l / r, 0.02);
        Assert.Equal(Sequence.RecordingLoudness, (l + r) / 2, Sequence.RecordingLoudness * 0.01);
    }

    /// <summary>
    /// A play lasts as long as the recording and one or two of its 10 ms rows: its last row is read once it has played, and a
    /// looping one starts again a row after that.
    /// </summary>
    [Fact]
    public void ARecordingLoopsARowAfterItEnds()
    {
        var recording = Sequence.Recording([Constant(4000, 0.5), Constant(4000, 0.5)], Rate);
        var real = AudioEngine.Milliseconds(recording) * (double)AudioEngine.StepSamples / AudioEngine.OriginalRate;
        Assert.InRange(real, 0.51, 0.52);

        var (left, _) = Render(recording, 1.2, loop: true);
        Assert.NotEqual(0, left[(int)(Rate * 0.25)]);
        Assert.Equal(0, left[(int)(Rate * 0.505)]);
        Assert.NotEqual(0, left[(int)(Rate * 0.75)]);
    }

    /// <summary>A plugin's WAV named for a sequence plays in its place, begun loading as what plays it loads.</summary>
    [Fact]
    public void ASequencesOwnWavPlaysInItsPlace()
    {
        using var temp = new TempPlugins();
        var dir = temp.Add("aidyn", null);
        var id = RecordId.Parse("aidyn:sequence/0x05CE");
        var wav = Path.Combine(dir, "assets", "aidyn", "sequence", "0x05CE.wav");
        Directory.CreateDirectory(Path.GetDirectoryName(wav)!);
        File.WriteAllBytes(wav, Wav.EncodeStereo([.. Enumerable.Range(0, Rate).SelectMany(_ => new short[] { 3000, -3000 })], Rate));
        var bank = new SoundBank(PluginSet.Load([dir]));

        bank.Warm(id);
        var sequence = bank.Get(id);

        Assert.NotNull(sequence);
        Assert.Equal(2, sequence.Tracks);
        Assert.Equal((short)3000, sequence.Instruments[0].Samples[0]);
        Assert.Equal((short)-3000, sequence.Instruments[1].Samples[0]);
        Assert.Same(sequence, bank.Get(id));
    }
}
