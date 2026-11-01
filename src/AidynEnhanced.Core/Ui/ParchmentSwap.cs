namespace AidynEnhanced.Core.Ui;

/// <summary>
/// The game's blank parchment in place of the pause menu tabs' own, the user's choice and the user's art (Oct 31, 2026): laid across each tab's
/// frame, its torn edges on the lines the menu's height puts them, <see cref="Scale"/> times as fine as the tabs so it keeps
/// its detail on a large screen. Nothing painted into the tabs is drawn on it: the user wanted the parchment blank.
/// </summary>
public static class ParchmentSwap
{
    /// <summary>How many pixels across the laid parchment has for each of the tab's.</summary>
    public const int Scale = 4;

    /// <summary>The lines the parchment's torn edges reach in the full-height menu, where the user marked them (Oct 31, 2026).</summary>
    public const int Top = 6, Bottom = 234;

    /// <summary>
    /// Whether a tab's parchment is laid mirrored, left to right: every other tab's, the Party tab's, so the parchment runs on
    /// unbroken from tab to tab, the tabs lying side by side on the scroll, its left edge against its left edge and its right
    /// against its right.
    /// </summary>
    public static bool Mirrored(PauseTab tab) => (int)tab % 2 == 1;

    /// <summary>
    /// The parchment of RGBA bytes laid across a frame <paramref name="width"/> × <paramref name="height"/>, <see cref="Scale"/>
    /// times as fine: its width across the frame's, its outermost torn edges, the first row any column is opaque in and the
    /// last, on lines <paramref name="top"/> and <paramref name="bottom"/>, and <paramref name="mirrored"/> left to right
    /// (<see cref="Mirrored"/>). Its transparent pixels take the colour of the parchment nearest them up or down, so it doesn't
    /// fringe its edges with another colour drawn filtered.
    /// </summary>
    public static byte[] Lay(ReadOnlySpan<byte> parchment, int parchmentWidth, int parchmentHeight, int width, int height, int top, int bottom,
        bool mirrored = false)
    {
        if (parchment.Length != parchmentWidth * parchmentHeight * 4)
            throw new ArgumentException("The parchment isn't its width × height RGBA bytes.", nameof(parchment));
        var (first, last) = Edges(parchment, parchmentWidth, parchmentHeight);
        var (w, h) = (width * Scale, height * Scale);
        var laid = new byte[w * h * 4];
        var rowsPerLine = (float)(last + 1 - first) / (bottom - top);
        Span<float> c = stackalloc float[4];
        for (var y = 0; y < h; y++)
        {
            var py = first + ((y + 0.5f) / Scale - top) * rowsPerLine - 0.5f;
            for (var x = 0; x < w; x++)
            {
                Sample(parchment, parchmentWidth, parchmentHeight, ((mirrored ? w - 1 - x : x) + 0.5f) * parchmentWidth / w - 0.5f, py, c);
                var o = (y * w + x) * 4;
                for (var i = 0; i < 4; i++)
                    laid[o + i] = (byte)Math.Clamp((int)MathF.Round(c[i]), 0, 255);
            }
        }
        Bleed(laid, w, h);
        return laid;
    }

    /// <summary>The parchment's outermost torn edges: the first row any column is opaque in and the last.</summary>
    private static (int First, int Last) Edges(ReadOnlySpan<byte> parchment, int width, int height)
    {
        var (first, last) = (height - 1, 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (parchment[(y * width + x) * 4 + 3] < 128)
                    continue;
                first = Math.Min(first, y);
                last = Math.Max(last, y);
            }
        }
        return first <= last ? (first, last) : (0, height - 1);
    }

    /// <summary>The parchment at a point, by its four nearest pixels weighed by their alpha, so the transparent ones' colours don't bleed in.</summary>
    private static void Sample(ReadOnlySpan<byte> rgba, int width, int height, float x, float y, Span<float> c)
    {
        var (x0, y0) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - x0, y - y0);
        c.Clear();
        float alpha = 0;
        for (var j = 0; j < 2; j++)
        {
            for (var i = 0; i < 2; i++)
            {
                var (u, v) = (Math.Clamp(x0 + i, 0, width - 1), Math.Clamp(y0 + j, 0, height - 1));
                var at = (v * width + u) * 4;
                var weight = (i == 0 ? 1 - fx : fx) * (j == 0 ? 1 - fy : fy) * rgba[at + 3];
                for (var k = 0; k < 3; k++)
                    c[k] += rgba[at + k] * weight;
                alpha += weight;
            }
        }
        for (var k = 0; k < 3; k++)
            c[k] = alpha > 0 ? c[k] / alpha : 0;
        c[3] = alpha;
    }

    /// <summary>Gives the transparent pixels the colour of the parchment nearest them up or down the column.</summary>
    private static void Bleed(byte[] rgba, int width, int height)
    {
        for (var x = 0; x < width; x++)
        {
            var from = -1;
            for (var y = 0; y < height && from < 0; y++)
            {
                if (rgba[(y * width + x) * 4 + 3] != 0)
                    from = y;
            }
            if (from < 0)
                continue;
            for (var y = 0; y < height; y++)
            {
                var at = (y * width + x) * 4;
                if (rgba[at + 3] != 0)
                    from = y;
                else
                    rgba.AsSpan((from * width + x) * 4, 3).CopyTo(rgba.AsSpan(at, 3));
            }
        }
    }
}
