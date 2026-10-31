using AidynEnhanced.Core.Ui;

namespace AidynEnhanced.Core.Tests;

public class ParchmentSwapTests
{
    private const int ParchmentWidth = 40, ParchmentHeight = 60, Width = 10, Height = 15;

    /// <summary>A parchment whose columns run from dark to light, opaque from row 6 to row 53, transparent white beyond.</summary>
    private static byte[] Parchment()
    {
        var rgba = new byte[ParchmentWidth * ParchmentHeight * 4];
        for (var y = 0; y < ParchmentHeight; y++)
        {
            for (var x = 0; x < ParchmentWidth; x++)
            {
                byte[] c = y is >= 6 and <= 53 ? [(byte)(100 + x * 3), (byte)(90 + x * 3), 80, 255] : [255, 255, 255, 0];
                c.CopyTo(rgba, (y * ParchmentWidth + x) * 4);
            }
        }
        return rgba;
    }

    private static byte[] At(byte[] rgba, int width, int x, int y) => rgba[((y * width + x) * 4)..((y * width + x) * 4 + 4)];

    [Fact]
    public void TheParchmentIsLaidAcrossTheFrameItsTornEdgesOnTheLines()
    {
        var laid = ParchmentSwap.Lay(Parchment(), ParchmentWidth, ParchmentHeight, Width, Height, top: 2, bottom: 13);

        var w = Width * ParchmentSwap.Scale;
        Assert.Equal(w * Height * ParchmentSwap.Scale * 4, laid.Length);
        // Line 2 starts at row 8 and line 13 ends at row 51.
        Assert.Equal(0, At(laid, w, 20, 7)[3]);
        Assert.Equal(255, At(laid, w, 20, 8)[3]);
        Assert.Equal(255, At(laid, w, 20, 51)[3]);
        Assert.Equal(0, At(laid, w, 20, 52)[3]);
        // Beyond the edges, the parchment's own colour rather than the transparent white.
        Assert.Equal(At(laid, w, 20, 8)[..3], At(laid, w, 20, 0)[..3]);
    }

    [Fact]
    public void AMirroredTabsParchmentRunsTheOtherWay()
    {
        var laid = ParchmentSwap.Lay(Parchment(), ParchmentWidth, ParchmentHeight, Width, Height, 2, 13);
        var mirrored = ParchmentSwap.Lay(Parchment(), ParchmentWidth, ParchmentHeight, Width, Height, 2, 13, mirrored: true);

        var w = Width * ParchmentSwap.Scale;
        Assert.Equal(At(laid, w, 3, 30), At(mirrored, w, w - 4, 30));
        Assert.True(ParchmentSwap.Mirrored(PauseTab.Party));
        Assert.False(ParchmentSwap.Mirrored(PauseTab.System));
        Assert.False(ParchmentSwap.Mirrored(PauseTab.World));
    }
}
