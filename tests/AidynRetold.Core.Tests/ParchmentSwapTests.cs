using AidynRetold.Core.Ui;

namespace AidynRetold.Core.Tests;

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
        // Line 2 starts at its first row and line 13 ends at its last: the edge, filtered, is more than half opaque on the line's
        // side and less beyond it, clear a line out and opaque a line in.
        var (first, last) = (2 * ParchmentSwap.Scale, 13 * ParchmentSwap.Scale - 1);
        Assert.InRange(At(laid, w, 20, first - 1)[3], 0, 127);
        Assert.InRange(At(laid, w, 20, first)[3], 128, 255);
        Assert.InRange(At(laid, w, 20, last)[3], 128, 255);
        Assert.InRange(At(laid, w, 20, last + 1)[3], 0, 127);
        Assert.Equal((0, 0), (At(laid, w, 20, first - ParchmentSwap.Scale)[3], At(laid, w, 20, last + ParchmentSwap.Scale)[3]));
        Assert.Equal((255, 255), (At(laid, w, 20, first + ParchmentSwap.Scale)[3], At(laid, w, 20, last - ParchmentSwap.Scale)[3]));
        // Beyond the edges, the parchment's own colour rather than the transparent white.
        Assert.Equal(At(laid, w, 20, first)[..3], At(laid, w, 20, 0)[..3]);
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
