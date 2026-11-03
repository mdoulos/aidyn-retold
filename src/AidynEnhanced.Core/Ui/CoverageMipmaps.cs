namespace AidynEnhanced.Core.Ui;

/// <summary>
/// The mipmaps of a piece of UI art drawn far smaller than it's painted, made by coverage, the user's report (Nov 2, 2026): each
/// smaller texel's colour is its four's weighted by their alpha, and at every size a clear texel takes its neighbours' colour, so
/// filtering at the art's edge never mixes in the colour a clear pixel holds.
/// </summary>
/// <remarks>
/// Godot's own (<c>Image.GenerateMipmaps</c>) went wrong two ways on art of a size not a power of two, which showed as dark smudges on
/// the right and bottom edges of the aspects' and schools' icons, 250 square. It averages colour without alpha, and
/// <c>FixAlphaEdges</c> colours clear pixels only 4 in from the art, so the greys the icons' clear pixels hold came through at the
/// smaller sizes. And it halves each level from the last two texels to one, leaving out an odd side's last row or column, which the
/// GPU then stretches over the whole: the 250's level 4, 15 texels, holds its first 240 pixels, and level 7, one texel, its first 128,
/// so each smaller level's art lies further right and down. The chain here is laid out as Godot's is, each level half the last's
/// size, rounded down and at least 1, to 1 × 1, but each covers the whole of the last (<see cref="Halve"/>).
/// </remarks>
public static class CoverageMipmaps
{
    /// <summary>
    /// The image of RGBA bytes with its mipmaps after it, as Godot takes an image with mipmaps. The image itself is kept but for its
    /// clear pixels' colour.
    /// </summary>
    public static byte[] Chain(byte[] rgba, int width, int height)
    {
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("The image isn't width × height RGBA bytes.", nameof(rgba));
        var level = (byte[])rgba.Clone();
        Bleed(level, width, height);
        var levels = new List<byte[]> { level };
        var (w, h) = (width, height);
        while (w > 1 || h > 1)
        {
            var (half, halfWidth, halfHeight) = (Halve(level, w, h), Math.Max(1, w >> 1), Math.Max(1, h >> 1));
            Bleed(half, halfWidth, halfHeight);
            levels.Add(half);
            (level, w, h) = (half, halfWidth, halfHeight);
        }
        var chain = new byte[levels.Sum(l => l.Length)];
        var at = 0;
        foreach (var l in levels)
        {
            l.CopyTo(chain, at);
            at += l.Length;
        }
        return chain;
    }

    /// <summary>
    /// A level half the size, rounded down, over the whole of the last: each texel the share of the last's it covers, its alpha their
    /// alpha averaged by how much of each it covers and its colour weighted by that and their alpha. Where a side is odd its texels
    /// cover a little more than two each, so the level reaches the last's far edges, which the GPU stretches it to.
    /// </summary>
    private static byte[] Halve(byte[] src, int width, int height)
    {
        var (w, h) = (Math.Max(1, width >> 1), Math.Max(1, height >> 1));
        var dst = new byte[w * h * 4];
        var (stepX, stepY) = ((double)width / w, (double)height / h);
        for (var y = 0; y < h; y++)
        {
            var (top, bottom) = (y * stepY, (y + 1) * stepY);
            var lastRow = Math.Min(height, (int)Math.Ceiling(bottom - 1e-9));
            for (var x = 0; x < w; x++)
            {
                var (left, right) = (x * stepX, (x + 1) * stepX);
                var lastColumn = Math.Min(width, (int)Math.Ceiling(right - 1e-9));
                double r = 0, g = 0, b = 0, alpha = 0, area = 0, plainR = 0, plainG = 0, plainB = 0;
                for (var j = (int)top; j < lastRow; j++)
                {
                    var down = Math.Min(bottom, j + 1) - Math.Max(top, j);
                    for (var i = (int)left; i < lastColumn; i++)
                    {
                        var share = (Math.Min(right, i + 1) - Math.Max(left, i)) * down;
                        var s = (j * width + i) * 4;
                        var weight = src[s + 3] * share;
                        (r, g, b, alpha, area) = (r + src[s] * weight, g + src[s + 1] * weight, b + src[s + 2] * weight, alpha + weight, area + share);
                        (plainR, plainG, plainB) = (plainR + src[s] * share, plainG + src[s + 1] * share, plainB + src[s + 2] * share);
                    }
                }
                var d = (y * w + x) * 4;
                // A texel with nothing in it keeps its texels' colour, as it is; Bleed colours it if it's beside the art.
                (dst[d], dst[d + 1], dst[d + 2]) = alpha > 0
                    ? (Byte(r / alpha), Byte(g / alpha), Byte(b / alpha))
                    : (Byte(plainR / area), Byte(plainG / area), Byte(plainB / area));
                dst[d + 3] = Byte(alpha / area);
            }
        }
        return dst;
    }

    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);

    /// <summary>
    /// Each clear texel beside the art takes its eight neighbours' colour, weighted by their alpha, in place. Only clear texels change,
    /// and only texels that aren't clear are read, so the order doesn't matter.
    /// </summary>
    private static void Bleed(byte[] px, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var o = (y * width + x) * 4;
                if (px[o + 3] != 0)
                    continue;
                int r = 0, g = 0, b = 0, sum = 0;
                for (var ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
                {
                    for (var nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                    {
                        var n = (ny * width + nx) * 4;
                        var a = px[n + 3];
                        if (a == 0)
                            continue;
                        (r, g, b, sum) = (r + px[n] * a, g + px[n + 1] * a, b + px[n + 2] * a, sum + a);
                    }
                }
                if (sum > 0)
                    (px[o], px[o + 1], px[o + 2]) = ((byte)((r + sum / 2) / sum), (byte)((g + sum / 2) / sum), (byte)((b + sum / 2) / sum));
            }
        }
    }
}
