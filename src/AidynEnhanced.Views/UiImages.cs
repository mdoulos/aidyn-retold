using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Assets;
using AidynEnhanced.Plugins.Records;
using Godot;

namespace AidynEnhanced.Views;

/// <summary>UI images from the plugins, as textures, loaded once each.</summary>
public sealed class UiImages(PluginSet plugins)
{
    // The pieces of the original's feathered popups (WidgetChoiceDia::SetImages), as the machine code
    // loads them: the band, a 1 × 1 white texel of alpha 136, then the gradient above, below, left and
    // right, and the corners from the top left clockwise.
    private static readonly RecordId Band = RecordId.Hex(RecordTypes.Image, 0x01C4);
    private static readonly RecordId[] Edges = [.. new[] { 0x01C3, 0x01C2, 0x01C0, 0x01C1 }.Select(i => RecordId.Hex(RecordTypes.Image, i))];
    private static readonly RecordId[] Corners = [.. new[] { 0x01BC, 0x01BD, 0x01BE, 0x01BF }.Select(i => RecordId.Hex(RecordTypes.Image, i))];

    private readonly Dictionary<RecordId, Texture2D?> _textures = [];

    /// <summary>A UI image, or null if no plugin has it.</summary>
    public Texture2D? Get(RecordId image)
    {
        if (_textures.TryGetValue(image, out var texture))
            return texture;
        if (plugins.FindAsset(image, "png") is { } png)
            texture = Png.Texture(png);
        else
        {
            GD.PushWarning($"No asset for {image}.");
        }
        return _textures[image] = texture;
    }

    private readonly Dictionary<RecordId, Texture2D?> _plain = [];

    /// <summary>
    /// An image with what's painted into it covered by plain parchment from elsewhere in it (<see cref="PlainImage"/>),
    /// made the first time it's asked for, or null if no plugin has the image.
    /// </summary>
    public Texture2D? Plain(RecordId image, IReadOnlyList<PlainImage.Patch> patches)
    {
        if (_plain.TryGetValue(image, out var plain))
            return plain;
        if (Get(image)?.GetImage() is { } data)
        {
            data.Convert(Image.Format.Rgba8);
            var (width, height) = (data.GetWidth(), data.GetHeight());
            var bytes = data.GetData();
            PlainImage.Apply(bytes, width, height, patches);
            plain = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes));
        }
        return _plain[image] = plain;
    }

    private readonly Dictionary<(RecordId, bool), Texture2D?> _tall = [];

    /// <summary>
    /// A tab's image made taller for the full-height menu (<see cref="TallImage"/>), its gaps filled from its plain version
    /// (<see cref="Plain"/>, by <paramref name="patches"/>), or that plain version made taller; made the first time it's asked for,
    /// or null if no plugin has the image.
    /// </summary>
    public Texture2D? Tall(RecordId image, IReadOnlyList<PlainImage.Patch> patches, bool plain = false)
    {
        if (_tall.TryGetValue((image, plain), out var tall))
            return tall;
        if (Plain(image, patches)?.GetImage() is { } flat && (plain ? flat : Get(image)?.GetImage()) is { } data)
        {
            flat.Convert(Image.Format.Rgba8);
            data.Convert(Image.Format.Rgba8);
            var (width, height) = (data.GetWidth(), data.GetHeight());
            var bytes = TallImage.Apply(data.GetData(), flat.GetData(), width, height);
            tall = ImageTexture.CreateFromImage(Image.CreateFromData(width, height + TallImage.Above + TallImage.Below, false, Image.Format.Rgba8, bytes));
        }
        return _tall[(image, plain)] = tall;
    }

    /// <summary>
    /// The pause menu's scroll, scene <c>0x0E63</c>, beside whose model a plugin can put a blank parchment of its own in place
    /// of the game's, <c>0x0E63.parchment.png</c>, at any size (<see cref="ParchmentSwap"/>).
    /// </summary>
    private static readonly RecordId Scroll = RecordId.Hex(RecordTypes.Scene, 0x0E63);

    /// <summary>The parchment laid across a tab's frame, by the frame's size and the side, which a plugin set's tabs share.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PluginSet, Dictionary<(int, int, bool), Task<byte[]?>>> Laid = [];

    /// <summary>The laid parchment's textures, by whether they're mirrored.</summary>
    private readonly Dictionary<bool, Texture2D?> _onParchment = [];

    /// <summary>
    /// The game's blank parchment, the user's (<c>assets/ui/parchment.png</c>, carried in this assembly), or a plugin's in its
    /// place, laid across a tab's frame in place of the tab's own (<see cref="ParchmentSwap"/>): its torn edges where the
    /// full-height menu puts them, and mirrored as the tab's place on the scroll says (<see cref="ParchmentSwap.Mirrored"/>).
    /// Null only if the tab has no image to size it by or it can't be read. It's laid on another thread,
    /// begun by <see cref="WarmParchment"/> or the first time it's asked for, and waited for then.
    /// </summary>
    public Texture2D? OnParchment(PauseTab tab)
    {
        var key = ParchmentSwap.Mirrored(tab);
        if (_onParchment.TryGetValue(key, out var texture))
            return texture;
        try
        {
            if (Laying(tab) is { } laying && laying.Result is { } laid)
            {
                var (width, height) = plugins.ImageSize(MenuImages.Tab(tab));
                var data = Image.CreateFromData(width * ParchmentSwap.Scale, height * ParchmentSwap.Scale, false, Image.Format.Rgba8, laid);
                data.GenerateMipmaps();
                // Mipmapped, for a screen drawn at the original's size, where it's drawn at a quarter.
                texture = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(data), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
            }
        }
        catch (AggregateException e)
        {
            GD.PushWarning($"The scroll's parchment couldn't be laid: {e.InnerException?.Message}");
        }
        return _onParchment[key] = texture;
    }

    /// <summary>Begins laying the parchment for a tab on another thread, so it's ready when the menu opens.</summary>
    public void WarmParchment(PauseTab tab) => Laying(tab);

    private Task<byte[]?>? Laying(PauseTab tab)
    {
        var (w, h) = plugins.ImageSize(MenuImages.Tab(tab));
        if (w == 0)
            return null;
        var path = plugins.FindAsset(Scroll, "parchment.png");
        var mirrored = ParchmentSwap.Mirrored(tab);
        var laid = Laid.GetValue(plugins, _ => []);
        if (!laid.TryGetValue((w, h, mirrored), out var task))
        {
            laid[(w, h, mirrored)] = task = Task.Run<byte[]?>(() =>
                PngImage.Decode(path is not null ? File.ReadAllBytes(path) : GameParchment()) is { } p
                    ? ParchmentSwap.Lay(p.Rgba, p.Width, p.Height, w, h, ParchmentSwap.Top, ParchmentSwap.Bottom, mirrored)
                    : null);
        }
        return task;
    }

    /// <summary>The game's own blank parchment's PNG, carried in this assembly.</summary>
    private static byte[] GameParchment()
    {
        using var stream = typeof(UiImages).Assembly.GetManifestResourceStream("parchment.png")
            ?? throw new InvalidOperationException("The views' assembly carries no parchment.png.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private readonly Dictionary<RecordId, Texture2D?> _feathered = [];

    /// <summary>
    /// An image faded out at its edges with round corners, by the feathered boxes' own ramp (<see cref="EdgeFeather"/>),
    /// made the first time it's asked for, or null if no plugin has the image.
    /// </summary>
    public Texture2D? Feathered(RecordId image, int left, int top, int right, int bottom)
    {
        if (_feathered.TryGetValue(image, out var feathered))
            return feathered;
        if (Get(image)?.GetImage() is { } data)
        {
            data.Convert(Image.Format.Rgba8);
            var (width, height) = (data.GetWidth(), data.GetHeight());
            var bytes = data.GetData();
            EdgeFeather.Apply(bytes, width, height, left, top, right, bottom, EdgeRamp());
            feathered = ImageTexture.CreateFromImage(Image.CreateFromData(width, height, false, Image.Format.Rgba8, bytes));
        }
        return _feathered[image] = feathered;
    }

    /// <summary>
    /// The feathered boxes' ramp from the edge in, a value a pixel from 0 to 1: the left gradient's top row, over the
    /// band's alpha, 136, which its last pixels reach.
    /// </summary>
    private float[] EdgeRamp()
    {
        if (Get(Edges[2])?.GetImage() is not { } data)
            return [1];
        data.Convert(Image.Format.Rgba8);
        var full = data.GetPixel(data.GetWidth() - 1, 0).A;
        return [.. Enumerable.Range(0, data.GetWidth()).Select(x => Math.Min(1, data.GetPixel(x, 0).A / full))];
    }

    /// <summary>
    /// A band feathered by each piece at its own size, as a message's box is (<c>IntroMenu.ShadowBox</c>):
    /// the gradients' thickness is their images' height above and below and width beside.
    /// </summary>
    public FeatheredPanel Around(ScreenRect band)
    {
        int Height(RecordId image) => Get(image) is { } t ? t.GetHeight() : 0;
        int Width(RecordId image) => Get(image) is { } t ? t.GetWidth() : 0;
        return new FeatheredPanel(band, Height(Edges[0]), Height(Edges[1]), Width(Edges[2]), Width(Edges[3]));
    }

    /// <summary>Draws a feathered panel in black, as the original's popups are drawn, each piece scaled to fit, at an alpha.</summary>
    public void DrawPanel(CanvasItem canvas, FeatheredPanel panel, float alpha = 1)
    {
        var tint = new Color(0, 0, 0, alpha);
        var b = panel.Band;
        var (left, top, right, bottom) = ((float)b.Left, (float)b.Top, (float)b.Right, (float)b.Bottom);
        Piece(Band, new Rect2(left, top, right - left, bottom - top));
        Piece(Edges[0], new Rect2(left, top - panel.Top, right - left, panel.Top));
        Piece(Edges[1], new Rect2(left, bottom, right - left, panel.Bottom));
        Piece(Edges[2], new Rect2(left - panel.Left, top, panel.Left, bottom - top));
        Piece(Edges[3], new Rect2(right, top, panel.Right, bottom - top));
        Piece(Corners[0], new Rect2(left - panel.Left, top - panel.Top, panel.Left, panel.Top));
        Piece(Corners[1], new Rect2(right, top - panel.Top, panel.Right, panel.Top));
        Piece(Corners[2], new Rect2(right, bottom, panel.Right, panel.Bottom));
        Piece(Corners[3], new Rect2(left - panel.Left, bottom, panel.Left, panel.Bottom));

        void Piece(RecordId image, Rect2 rect)
        {
            if (rect.Size.X > 0 && rect.Size.Y > 0 && Get(image) is { } texture)
                canvas.DrawTextureRect(UiPainter.Filtered(texture, rect.Size, new Rect2(Vector2.Zero, texture.GetSize())), rect, false, tint);
        }
    }
}
