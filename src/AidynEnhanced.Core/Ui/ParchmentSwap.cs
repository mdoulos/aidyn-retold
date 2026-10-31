namespace AidynEnhanced.Core.Ui;

/// <summary>
/// The pause menu's tabs on a new parchment, a mod's: the parchment laid across the frame, its torn edges on the lines the
/// menu's height puts them, <see cref="Scale"/> times as fine as the tabs so it keeps its detail on a large screen, and what's
/// painted into each tab carried onto it, so the new parchment takes the place of the tab's own.
/// </summary>
/// <remarks>
/// <para>
/// What's painted is told from the parchment by the tab's plain version (<see cref="PlainImage"/>): only where the two differ
/// can there be paint. It's carried as a factor a colour at a time, which the new parchment is multiplied by: 1 where nothing's
/// painted, under 1 where the paint is darker and over where it's lighter, so the painted layout takes on the new parchment's
/// tone. The original's parchment is heavily stained, and inside the patches the plain version's parchment is a copy from
/// elsewhere with stains of its own, so the plain difference alone carries old stains onto the new parchment. Paint is taken
/// in two parts that leave most of them out:
/// </para>
/// <list type="bullet">
/// <item>The washes, the Party tab's silhouette chief among them: the difference from the plain version smoothed over
/// <see cref="WashBlur"/>, where it darkens by <see cref="WashFrom"/> to <see cref="WashTo"/> (in log), as strongly as stains
/// rarely do. The World tab's sea, washed more faintly than its stains, is left out, and its coasts kept.</item>
/// <item>The detail, the frames, lettering, rules, knotwork, coasts and the calendar: where the tab stands out from its own
/// blur by <see cref="Detail"/>, a pixel round it, measured against the parchment round it (the membrane over the tab
/// from the pixels outside the detail, as <see cref="PlainImage"/>'s patches meet the image) and against the plain version
/// with the wash taken out, and kept only where both agree, the smaller of the two: a stain's edge or the ring round a wash
/// shows against one and not the other. A factor nearer 1 than <see cref="Faint"/> is none, fading in to <see cref="Clear"/>.</item>
/// </list>
/// <para>
/// Both fade out over <see cref="EdgeFade"/> pixels by the tab's own torn edge, so paint that reaches it, the World tab's
/// coasts, fades into the new parchment rather than stopping at a tear that's no longer there. The paint keeps the tab's
/// pixels; only the parchment is finer.
/// </para>
/// </remarks>
public static class ParchmentSwap
{
    /// <summary>How many pixels across the laid parchment has for each of the tab's.</summary>
    public const int Scale = 4;

    /// <summary>
    /// The lines the parchment's torn edges reach: the full-height menu's, where the user marked them (Oct 31, 2026), and the
    /// original's height, the System tab's.
    /// </summary>
    public const int FullTop = 6, FullBottom = 234, OriginalTop = 13, OriginalBottom = 222;

    private const float WashBlur = 1, WashFrom = 0.14f, WashTo = 0.24f;
    private const float DetailBlur = 1.5f, Detail = 10;
    private const float Faint = 0.04f, Clear = 0.10f;
    private const int EdgeFade = 4;

    /// <summary>The membrane's over-relaxation, as <see cref="PlainImage"/>'s: enough to settle detail 30 pixels across.</summary>
    private const int Passes = 200;

    private const float Relaxation = 1.9f;

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

    /// <summary>
    /// What's painted into a tab of RGBA bytes, as the factor each of its pixels multiplies the parchment by, three a pixel:
    /// red, green and blue. <paramref name="plain"/> is the tab with nothing painted in; the tab itself gives all 1s.
    /// </summary>
    public static float[] Paint(ReadOnlySpan<byte> image, ReadOnlySpan<byte> plain, int width, int height)
    {
        var n = width * height;
        if (image.Length != n * 4 || plain.Length != image.Length)
            throw new ArgumentException("The tab's images aren't both width × height RGBA bytes.");
        // Where there can be paint, and the difference from the plain version there.
        var painted = new bool[n];
        var difference = new float[n * 3];
        for (var i = 0; i < n; i++)
        {
            if (image[i * 4 + 3] != 255 || plain[i * 4 + 3] != 255
                || (image[i * 4] == plain[i * 4] && image[i * 4 + 1] == plain[i * 4 + 1] && image[i * 4 + 2] == plain[i * 4 + 2]))
                continue;
            painted[i] = true;
            for (var c = 0; c < 3; c++)
                difference[i * 3 + c] = Log(image[i * 4 + c]) - Log(plain[i * 4 + c]);
        }

        // The washes.
        var wash = Blur(difference, painted, width, height, 3, WashBlur);
        var factor = new float[n * 3];
        for (var i = 0; i < n; i++)
        {
            if (!painted[i])
                continue;
            var t = Step(WashFrom, WashTo, -(wash[i * 3] + wash[i * 3 + 1] + wash[i * 3 + 2]) / 3);
            for (var c = 0; c < 3; c++)
                factor[i * 3 + c] = wash[i * 3 + c] * t;
        }

        // The detail: where the tab stands out from its own blur, brightness and colour apart, a pixel round it.
        var luma = new float[n];
        var chroma = new float[n * 3];
        for (var i = 0; i < n; i++)
        {
            luma[i] = (image[i * 4] + image[i * 4 + 1] + image[i * 4 + 2]) / 3f;
            for (var c = 0; c < 3; c++)
                chroma[i * 3 + c] = image[i * 4 + c] - luma[i];
        }
        var lumaBlur = Blur(luma, null, width, height, 1, DetailBlur);
        var chromaBlur = Blur(chroma, null, width, height, 3, DetailBlur);
        var detail = new bool[n];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                float off = 0;
                for (var c = 0; c < 3; c++)
                    off += (chroma[i * 3 + c] - chromaBlur[i * 3 + c]) * (chroma[i * 3 + c] - chromaBlur[i * 3 + c]);
                if (Math.Abs(luma[i] - lumaBlur[i]) + MathF.Sqrt(off) <= Detail)
                    continue;
                for (var v = Math.Max(y - 1, 0); v <= Math.Min(y + 1, height - 1); v++)
                {
                    for (var u = Math.Max(x - 1, 0); u <= Math.Min(x + 1, width - 1); u++)
                        detail[v * width + u] |= painted[v * width + u];
                }
            }
        }
        var round = Membrane(image, plain, detail, width, height);
        Span<float> shown = stackalloc float[3];
        for (var i = 0; i < n; i++)
        {
            if (!detail[i])
                continue;
            float strongest = 0;
            for (var c = 0; c < 3; c++)
            {
                var local = Log(image[i * 4 + c]) - MathF.Log(round[i * 3 + c] + 0.5f);
                var unwashed = difference[i * 3 + c] - factor[i * 3 + c];
                shown[c] = MathF.Sign(local) == MathF.Sign(unwashed) ? MathF.Sign(local) * Math.Min(Math.Abs(local), Math.Abs(unwashed)) : 0;
                strongest = Math.Max(strongest, Math.Abs(shown[c]));
            }
            var t = Step(Faint, Clear, strongest);
            for (var c = 0; c < 3; c++)
                factor[i * 3 + c] += shown[c] * t;
        }

        // Fading out by the torn edge, and from logs to factors.
        var distance = EdgeDistance(image, plain, width, height);
        for (var i = 0; i < n; i++)
        {
            for (var c = 0; c < 3; c++)
                factor[i * 3 + c] = MathF.Exp(factor[i * 3 + c] * distance[i] / EdgeFade);
        }
        return factor;
    }

    /// <summary>
    /// The laid parchment (<see cref="Lay"/>) with a tab's paint (<see cref="Paint"/>) on it: each of the tab's pixels
    /// multiplies the <see cref="Scale"/> × <see cref="Scale"/> of the parchment's over it. The tab is <paramref name="width"/>
    /// × <paramref name="height"/>.
    /// </summary>
    public static byte[] Apply(ReadOnlySpan<byte> laid, ReadOnlySpan<float> paint, int width, int height)
    {
        var w = width * Scale;
        if (laid.Length != w * height * Scale * 4 || paint.Length != width * height * 3)
            throw new ArgumentException("The parchment isn't laid for the tab's size.");
        var result = laid.ToArray();
        for (var y = 0; y < height * Scale; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var p = ((y / Scale) * width + x / Scale) * 3;
                var o = (y * w + x) * 4;
                for (var c = 0; c < 3; c++)
                    result[o + c] = (byte)Math.Clamp((int)MathF.Round(laid[o + c] * paint[p + c]), 0, 255);
            }
        }
        return result;
    }

    private static float Log(byte value) => MathF.Log(value + 0.5f);

    private static float Step(float from, float to, float x)
    {
        var t = Math.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// A Gaussian blur of <paramref name="channels"/> values a pixel, edges clamped; with <paramref name="over"/>, of those
    /// pixels alone, each weighed by how much of the kernel they fill, and 0 elsewhere.
    /// </summary>
    private static float[] Blur(float[] values, bool[]? over, int width, int height, int channels, float sigma)
    {
        var radius = (int)MathF.Ceiling(3 * sigma);
        var kernel = new float[2 * radius + 1];
        for (var k = -radius; k <= radius; k++)
            kernel[k + radius] = MathF.Exp(-k * k / (2 * sigma * sigma));
        var n = width * height;
        // The weights ride along as one more channel.
        var stride = channels + 1;
        var a = new float[n * stride];
        for (var i = 0; i < n; i++)
        {
            var weight = over is null || over[i] ? 1f : 0f;
            for (var c = 0; c < channels; c++)
                a[i * stride + c] = values[i * channels + c] * weight;
            a[i * stride + channels] = weight;
        }
        var b = new float[n * stride];
        for (var pass = 0; pass < 2; pass++)
        {
            var (from, to) = pass == 0 ? (a, b) : (b, a);
            Array.Clear(to);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var o = (y * width + x) * stride;
                    for (var k = -radius; k <= radius; k++)
                    {
                        var (u, v) = pass == 0 ? (Math.Clamp(x + k, 0, width - 1), y) : (x, Math.Clamp(y + k, 0, height - 1));
                        var s = (v * width + u) * stride;
                        for (var c = 0; c < stride; c++)
                            to[o + c] += from[s + c] * kernel[k + radius];
                    }
                }
            }
        }
        var result = new float[n * channels];
        for (var i = 0; i < n; i++)
        {
            if (over is not null && !over[i])
                continue;
            var weight = a[i * stride + channels];
            for (var c = 0; c < channels; c++)
                result[i * channels + c] = weight > 0 ? a[i * stride + c] / weight : 0;
        }
        return result;
    }

    /// <summary>
    /// The tab's colour with the <paramref name="unknown"/> pixels solved as the membrane over the rest, by over-relaxation
    /// from the plain version's colour, three floats a pixel.
    /// </summary>
    private static float[] Membrane(ReadOnlySpan<byte> image, ReadOnlySpan<byte> plain, bool[] unknown, int width, int height)
    {
        var n = width * height;
        var values = new float[n * 3];
        for (var i = 0; i < n; i++)
        {
            for (var c = 0; c < 3; c++)
                values[i * 3 + c] = unknown[i] ? plain[i * 4 + c] : image[i * 4 + c];
        }
        for (var pass = 0; pass < Passes; pass++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (!unknown[i])
                        continue;
                    for (var c = 0; c < 3; c++)
                    {
                        float sum = 0;
                        var count = 0;
                        if (x > 0) { sum += values[(i - 1) * 3 + c]; count++; }
                        if (x < width - 1) { sum += values[(i + 1) * 3 + c]; count++; }
                        if (y > 0) { sum += values[(i - width) * 3 + c]; count++; }
                        if (y < height - 1) { sum += values[(i + width) * 3 + c]; count++; }
                        values[i * 3 + c] += Relaxation * (sum / count - values[i * 3 + c]);
                    }
                }
            }
        }
        return values;
    }

    /// <summary>How far each of the tab's pixels is from one that isn't opaque, its torn edge, in steps across and down, up to <see cref="EdgeFade"/>.</summary>
    private static int[] EdgeDistance(ReadOnlySpan<byte> image, ReadOnlySpan<byte> plain, int width, int height)
    {
        var distance = new int[width * height];
        for (var i = 0; i < distance.Length; i++)
            distance[i] = image[i * 4 + 3] == 255 && plain[i * 4 + 3] == 255 ? EdgeFade : 0;
        for (var pass = 0; pass < EdgeFade; pass++)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = y * width + x;
                    if (distance[i] == 0)
                        continue;
                    // The frame's own edge isn't a torn one: the tabs run on into each other there.
                    var near = Math.Min(Math.Min(x > 0 ? distance[i - 1] : EdgeFade, x < width - 1 ? distance[i + 1] : EdgeFade),
                        Math.Min(y > 0 ? distance[i - width] : EdgeFade, y < height - 1 ? distance[i + width] : EdgeFade));
                    distance[i] = Math.Min(distance[i], near + 1);
                }
            }
        }
        return distance;
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
