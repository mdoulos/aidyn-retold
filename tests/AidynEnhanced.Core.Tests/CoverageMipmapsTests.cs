using AidynEnhanced.Core.Ui;

namespace AidynEnhanced.Core.Tests;

public class CoverageMipmapsTests
{
    private static readonly byte[] Red = [255, 0, 0, 255], Grey = [128, 128, 128, 0];

    /// <summary>The chain is laid out as Godot's: each level half the last, rounded down and at least 1, to 1 × 1.</summary>
    [Theory]
    [InlineData(250, 250, new[] { 250 * 250, 125 * 125, 62 * 62, 31 * 31, 15 * 15, 7 * 7, 3 * 3, 1 })]
    [InlineData(5, 2, new[] { 10, 2, 1 })]
    public void TheChainIsLaidOutAsGodotsIs(int width, int height, int[] texels)
    {
        Assert.Equal(texels.Sum() * 4, CoverageMipmaps.Chain(new byte[width * height * 4], width, height).Length);
    }

    /// <summary>
    /// A smaller texel's colour is its four's weighted by their alpha, so a clear pixel's grey doesn't darken the art beside it, where
    /// averaging the colour alone made the red half grey.
    /// </summary>
    [Fact]
    public void AClearPixelsColourDoesntReachTheSmallerSizes()
    {
        byte[] image = [.. Red, .. Grey, .. Grey, .. Grey];
        var chain = CoverageMipmaps.Chain(image, 2, 2);
        Assert.Equal([255, 0, 0, 64], chain[16..20]);
    }

    /// <summary>
    /// A smaller level covers the whole of the last, an odd side's last column too, which Godot's leaves out though the GPU stretches
    /// the level over it: three pixels' one texel holds the red in the third at a third of its alpha.
    /// </summary>
    [Fact]
    public void ASmallerLevelCoversTheWholeOfTheLast()
    {
        byte[] image = [.. Grey, .. Grey, .. Red];
        var chain = CoverageMipmaps.Chain(image, 3, 1);
        Assert.Equal([255, 0, 0, 85], chain[12..16]);
    }

    /// <summary>
    /// A clear pixel beside the art takes its colour, so filtering between them mixes in none of the grey; one with nothing beside it
    /// keeps its own, and the art keeps its.
    /// </summary>
    [Fact]
    public void AClearPixelBesideTheArtTakesItsColour()
    {
        byte[] image = [.. Red, .. Grey, .. Grey];
        var chain = CoverageMipmaps.Chain(image, 3, 1);
        Assert.Equal([.. Red, 255, 0, 0, 0, .. Grey], chain[..12]);
    }
}
