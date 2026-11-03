using AidynRetold.Core.Ui;

namespace AidynRetold.Core.Tests;

public class TallImageTests
{
    private const int Width = 3, Height = 240;

    private static readonly byte[] Parchment = [200, 180, 150, 255], Paint = [90, 40, 30, 255], Flap = [210, 190, 160, 255], Clear = [0, 0, 0, 0];

    /// <summary>
    /// A tab's size: parchment from row 20 to row 220 in every column, clear above and below; column 1 has a frayed flap at row 15, past a gap,
    /// and a painted pixel at row 30. The plain image has the parchment where the paint is.
    /// </summary>
    private static (byte[] Image, byte[] Plain) Tab()
    {
        var image = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
                Set(image, x, y, y is >= 20 and <= 220 ? Parchment : Clear);
        }
        var plain = (byte[])image.Clone();
        Set(image, 1, 15, Flap);
        Set(plain, 1, 15, Flap);
        Set(image, 1, 30, Paint);
        return (image, plain);
    }

    private static void Set(byte[] rgba, int x, int y, byte[] c) => c.CopyTo(rgba, (y * Width + x) * 4);

    private static byte[] At(byte[] rgba, int x, int y) => rgba[((y * Width + x) * 4)..((y * Width + x) * 4 + 4)];

    [Fact]
    public void TheTornEdgesMoveOutAndEverythingPaintedStaysWhereItWas()
    {
        var (image, plain) = Tab();

        var tall = TallImage.Apply(image, plain, Width, Height);

        Assert.Equal(Width * (Height + TallImage.Above + TallImage.Below) * 4, tall.Length);
        // Drawn Above higher, the paint is where it was on the screen.
        Assert.Equal(Paint, At(tall, 1, 30 + TallImage.Above));
        // The flap past its gap moved out with the edge, keeping its own row of the tall image, and the gap went with it.
        Assert.Equal(Flap, At(tall, 1, 15));
        Assert.Equal(Clear, At(tall, 1, 17));
    }

    [Fact]
    public void TheParchmentRunsUnbrokenFromEdgeToEdge()
    {
        var (image, plain) = Tab();

        var tall = TallImage.Apply(image, plain, Width, Height);

        // From the moved top edge, row 20 of the tall image, to the moved bottom edge, 220 + Above + Below, every pixel is opaque.
        for (var x = 0; x < Width; x++)
        {
            for (var y = 20; y <= 220 + TallImage.Above + TallImage.Below; y++)
                Assert.True(At(tall, x, y)[3] == 255, $"({x}, {y}) isn't opaque");
        }
    }
}
