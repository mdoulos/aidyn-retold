namespace AidynRetold.Core.Ui;

/// <summary>
/// A pause menu tab's image made taller for the full-height menu, the user's choice (Oct 31, 2026): its torn top
/// and bottom edges moved out by <see cref="Above"/> and <see cref="Below"/>, so on the original's 240 lines the parchment nearly
/// reaches the screen's top and bottom, and the gaps filled with its own plain parchment. Everything painted into it stays where it
/// was, under the widgets drawn over it.
/// </summary>
/// <remarks>
/// It works a column at a time, as the torn edges wander: the edge and its darkened rim, <see cref="Rim"/> rows in from either end
/// of the parchment unbroken through the column's middle, move out, with any frayed flap beyond, and the gap is the plain image's
/// parchment from rows well inside it, the same rows in every column, so it runs on across columns and from one tab into the next as
/// the images do. It fades over <see cref="Blend"/> rows into the parchment just inside the edge at either end, so it meets the rim
/// and the body without a seam.
/// </remarks>
public static class TallImage
{
    /// <summary>
    /// How far the top and bottom edges move out, in lines: the System tab's torn edge reaches row 13 at the top and row 222 at the
    /// bottom, and moved out it reaches lines 6 and 234, where the user marked the parchment's edge (Oct 31, 2026), the game showing
    /// beyond it. The other tabs' edges move as far, keeping their own tears.
    /// </summary>
    public const int Above = 7, Below = 12;

    /// <summary>How many rows in from the edge its darkened rim runs, which moves out with it.</summary>
    private const int Rim = 5;

    /// <summary>How many rows the gap fades over into the parchment at either end.</summary>
    private const int Blend = 6;

    /// <summary>
    /// How far inside the gaps' rows their parchment is read from: 80 rows down for the top's and 90 up for the bottom's, both
    /// well within every tab's plain parchment.
    /// </summary>
    private const int FillTop = 80, FillBottom = 90;

    /// <summary>
    /// The image of RGBA bytes made taller, <see cref="Above"/> + its height + <see cref="Below"/> rows, drawn <see cref="Above"/>
    /// higher than it was. <paramref name="plain"/> is the same image with nothing painted in (<see cref="PlainImage"/>), which the
    /// gaps are filled from; it may be the image itself.
    /// </summary>
    public static byte[] Apply(ReadOnlySpan<byte> image, ReadOnlySpan<byte> plain, int width, int height)
    {
        if (image.Length != width * height * 4 || plain.Length != image.Length)
            throw new ArgumentException("The images aren't both width × height RGBA bytes.");
        var tall = new byte[width * (height + Above + Below) * 4];
        for (var x = 0; x < width; x++)
        {
            // The edges are the ends of the parchment unbroken through the column's middle: a frayed flap past a gap goes with the edge.
            var middle = height / 2;
            var (top, bottom) = (middle, middle);
            while (top > 0 && image[((top - 1) * width + x) * 4 + 3] >= 128)
                top--;
            while (bottom < height - 1 && image[((bottom + 1) * width + x) * 4 + 3] >= 128)
                bottom++;
            if (image[(middle * width + x) * 4 + 3] < 128)
            {
                for (var y = 0; y < height; y++)
                    Copy(image, width, x, y, tall, y + Above);
                continue;
            }
            var splitTop = Math.Min(top + Rim, height);
            var splitBottom = Math.Max(bottom - Rim + 1, splitTop);
            // The top edge where it was in the tall image, which is drawn Above higher; the body and the bottom edge below it.
            for (var y = 0; y < splitTop; y++)
                Copy(image, width, x, y, tall, y);
            for (var y = splitTop; y < splitBottom; y++)
                Copy(image, width, x, y, tall, y + Above);
            for (var y = splitBottom; y < height; y++)
                Copy(image, width, x, y, tall, y + Above + Below);
            // The gaps, in the rows of the screen they cover: the top one above the body, the bottom one below it.
            for (var y = splitTop - Above; y < splitTop; y++)
                Fill(plain, width, height, x, y + FillTop, splitTop, y - (splitTop - Above), splitTop - 1 - y, below: false, tall, y + Above);
            for (var y = splitBottom; y < splitBottom + Below; y++)
                Fill(plain, width, height, x, y - FillBottom, splitBottom - 1, y - splitBottom, splitBottom + Below - 1 - y, below: true, tall, y + Above);
        }
        return tall;
    }

    /// <summary>
    /// One pixel of a gap: the plain parchment at <paramref name="source"/>, faded at either end into the parchment just inside the
    /// edge, <paramref name="near"/> and the rows on from it into the body (up from it for the bottom gap), as the image runs there.
    /// </summary>
    /// <param name="fromStart">How many rows from the gap's first row, its end by the rim for the top gap and by the body for the bottom.</param>
    /// <param name="fromEnd">How many rows from the gap's last row.</param>
    private static void Fill(ReadOnlySpan<byte> plain, int width, int height, int x, int source, int near, int fromStart, int fromEnd, bool below,
        byte[] tall, int row)
    {
        Span<float> c = stackalloc float[4];
        Read(plain, width, height, x, source, c);
        Fade(plain, width, height, x, below ? near - fromStart : near + fromStart, fromStart, c);
        Fade(plain, width, height, x, below ? near - fromEnd : near + fromEnd, fromEnd, c);
        var at = (row * width + x) * 4;
        for (var i = 0; i < 4; i++)
            tall[at + i] = (byte)Math.Clamp((int)MathF.Round(c[i]), 0, 255);
    }

    /// <summary>Mixes the colour toward the plain image's at a row, all the way at 0 rows from the gap's end and none from <see cref="Blend"/>.</summary>
    private static void Fade(ReadOnlySpan<byte> plain, int width, int height, int x, int row, int distance, Span<float> c)
    {
        if (distance >= Blend)
            return;
        Span<float> edge = stackalloc float[4];
        Read(plain, width, height, x, row, edge);
        var t = (float)distance / Blend;
        for (var i = 0; i < 4; i++)
            c[i] = edge[i] + (c[i] - edge[i]) * t;
    }

    private static void Read(ReadOnlySpan<byte> rgba, int width, int height, int x, int y, Span<float> c)
    {
        var at = (Math.Clamp(y, 0, height - 1) * width + x) * 4;
        for (var i = 0; i < 4; i++)
            c[i] = rgba[at + i];
    }

    private static void Copy(ReadOnlySpan<byte> from, int width, int x, int y, byte[] to, int toY) =>
        from.Slice((y * width + x) * 4, 4).CopyTo(to.AsSpan((toY * width + x) * 4, 4));
}
