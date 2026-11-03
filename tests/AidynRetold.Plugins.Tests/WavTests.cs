using AidynRetold.Plugins.Assets;

namespace AidynRetold.Plugins.Tests;

public class WavTests
{
    [Fact]
    public void SamplesRoundTrip()
    {
        short[] samples = [0, 1, -1, short.MaxValue, short.MinValue, 1234];
        Assert.Equal(samples, Wav.Decode(Wav.Encode(samples, 22050)));
    }

    [Fact]
    public void StereoIsNotASample() =>
        Assert.Throws<InvalidDataException>(() => Wav.Decode(Wav.EncodeStereo([1, 2, 3, 4], 44100)));

    /// <summary>A recording's WAV reads as a channel each, with its rate.</summary>
    [Fact]
    public void AStereoWavReadsAsAChannelEach()
    {
        var (channels, rate) = Wav.DecodeChannels(Wav.EncodeStereo([1, 2, 3, 4, -5, 6], 48000));

        Assert.Equal(48000, rate);
        Assert.Equal([[1, 3, -5], [2, 4, 6]], channels);
    }
}
