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

    private readonly Dictionary<RecordId, Texture2D?> _textures = [], _originals = [];

    /// <summary>
    /// The game's own art in place of the original's images, the user's (Nov 2, 2026), carried in this assembly: the aspects' and the
    /// schools' icons, wherever they're drawn, laid out at the original's size as a larger replacement is; and each potion's and
    /// flask's picture (<see cref="MenuImages.PotionPictures"/>), <c>potion-sleep-gas.png</c> for <c>aidyn_enhanced:image/potion_sleep_gas</c>,
    /// laid out at the flask's size it stands in for.
    /// </summary>
    private static readonly Dictionary<RecordId, string> OwnArt = new Dictionary<RecordId, string>
    {
        [MenuImages.AspectIcon(Aspect.Lunar)!.Value] = "icon-lunar.png",
        [MenuImages.AspectIcon(Aspect.Solar)!.Value] = "icon-solar.png",
        [MenuImages.SchoolIcon(MagicSchool.Chaos)!.Value] = "icon-chaos.png",
        [MenuImages.SchoolIcon(MagicSchool.Elemental)!.Value] = "icon-elemental.png",
        [MenuImages.SchoolIcon(MagicSchool.Naming)!.Value] = "icon-naming.png",
        [MenuImages.SchoolIcon(MagicSchool.Necromancy)!.Value] = "icon-necromancy.png",
        [MenuImages.SchoolIcon(MagicSchool.Star)!.Value] = "icon-star.png",
    }.Concat(MenuImages.PotionPictures.Values.Select(p => KeyValuePair.Create(p, p.Key.Replace('_', '-') + ".png"))).ToDictionary();

    /// <summary>
    /// A UI image, or null if no plugin has it: the game's own art where it has its own (<see cref="OwnArt"/>), else the plugins'. A
    /// replacement larger than the original, as a redrawn pack's at 8 times or the game's own, is laid out at the original's size
    /// (<see cref="PluginSet.ImageSize"/>) and drawn into it, mipmapped and filtered, so it's sharper rather than larger; one at the
    /// original's size is drawn pixel for pixel as the original is. One of the port's own pictures draws the image it stands in for
    /// (<see cref="MenuImages.Shared"/>) in the original look.
    /// </summary>
    public Texture2D? Get(RecordId image)
    {
        // The original look draws the original's image, the first plugin's, whatever a mod or the game's own art puts over it.
        var original = UiPainter.Original;
        var textures = original ? _originals : _textures;
        if (textures.TryGetValue(image, out var texture))
            return texture;
        var shared = MenuImages.Shared(image);
        if (!original && OwnArt.TryGetValue(image, out var own) && ArtImage(own).Image?.Duplicate() is Image art
            && plugins.ImageSize(shared ?? image) is var (ownWidth, ownHeight) && ownWidth > 0)
            return textures[image] = Sharper(art, new Vector2I(ownWidth, ownHeight));
        if (shared is { } stoodFor)
            return textures[image] = Get(stoodFor);
        if ((original ? plugins.FindOriginalAsset(image, "png") : plugins.FindAsset(image, "png")) is { } png)
        {
            var (width, height) = plugins.ImageSize(image);
            texture = Png.Load(png) is not { } data ? null
                : width > 0 && (data.GetWidth() != width || data.GetHeight() != height) ? Sharper(data, new Vector2I(width, height))
                : ImageTexture.CreateFromImage(data);
        }
        else
        {
            GD.PushWarning($"No asset for {image}.");
        }
        return textures[image] = texture;
    }

    /// <summary>A larger picture drawn at a smaller size: mipmapped and filtered, the texture saying it's that size.</summary>
    private static Texture2D Sharper(Image data, Vector2I size)
    {
        var texture = ImageTexture.CreateFromImage(Mipmapped(data));
        texture.SetSizeOverride(size);
        return new CanvasTexture { DiffuseTexture = texture, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
    }

    /// <summary>
    /// An image with mipmaps made by coverage (<see cref="CoverageMipmaps"/>), so the colour its clear pixels hold never reaches its
    /// edges at the smaller sizes, where Godot's own left dark smudges on the icons' edges (Nov 2, 2026).
    /// </summary>
    private static Image Mipmapped(Image data)
    {
        data.Convert(Image.Format.Rgba8);
        data.ClearMipmaps();
        var (width, height) = (data.GetWidth(), data.GetHeight());
        return Image.CreateFromData(width, height, true, Image.Format.Rgba8, CoverageMipmaps.Chain(data.GetData(), width, height));
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
    private static byte[] GameParchment() => Carried("parchment.png");

    /// <summary>A file carried in this assembly, by its name in <c>assets/ui</c>.</summary>
    private static byte[] Carried(string name)
    {
        using var stream = typeof(UiImages).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The views' assembly carries no {name}.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private static readonly Dictionary<string, Texture2D?> CarriedArt = [];

    /// <summary>
    /// A piece of the game's own UI art, carried in this assembly by its name in <c>assets/ui</c> (<c>ui-gold.png</c>), loaded once.
    /// It's drawn far smaller than it's painted, so it's mipmapped and filtered, as the parchment is. Null if it can't be read.
    /// </summary>
    public static Texture2D? Art(string name)
    {
        if (CarriedArt.TryGetValue(name, out var texture))
            return texture;
        if (ArtImage(name).Image?.Duplicate() is Image image)
        {
            // Mipmapped by coverage, so the smaller sizes don't pale its edges toward the clear pixels' white or darken them toward
            // their black or grey.
            texture = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(Mipmapped(image)), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        }
        return CarriedArt[name] = texture;
    }

    private static readonly Dictionary<string, (Image? Image, Rect2 Paper)> CarriedImages = [];

    /// <summary>
    /// A piece of the game's own UI art as it's painted, RGBA, and its paper: the box its pixels more than half opaque fill, which a
    /// sheet of parchment is laid by. Read once; no image and an empty box if it can't be read.
    /// </summary>
    public static (Image? Image, Rect2 Paper) ArtImage(string name)
    {
        if (CarriedImages.TryGetValue(name, out var art))
            return art;
        var image = new Image();
        if (image.LoadPngFromBuffer(Carried(name)) != Error.Ok)
        {
            GD.PushWarning($"The views' {name} couldn't be read.");
            return CarriedImages[name] = (null, default);
        }
        image.Convert(Image.Format.Rgba8);
        var (w, h) = (image.GetWidth(), image.GetHeight());
        var bytes = image.GetData();
        var (left, top, right, bottom) = (w, h, -1, -1);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (bytes[(y * w + x) * 4 + 3] <= 128)
                    continue;
                (left, right) = (Math.Min(left, x), Math.Max(right, x));
                (top, bottom) = (Math.Min(top, y), Math.Max(bottom, y));
            }
        }
        var paper = right < 0 ? new Rect2(0, 0, w, h) : new Rect2(left, top, right + 1 - left, bottom + 1 - top);
        return CarriedImages[name] = (image, paper);
    }

    private readonly Dictionary<(RecordId, string), Texture2D?> _cut = [];

    /// <summary>
    /// An image cut to the outline of a piece of the game's own art, the art's paper stretched over the image, the user's choice
    /// (Nov 2, 2026): a portrait in the shape of a sheet of parchment, its edges following the sheet's torn ones. An original's
    /// small portrait is made larger first, pixel for pixel, so the outline isn't lost to its pixels. Mipmapped and filtered, made the
    /// first time it's asked for; null if no plugin has the image or the art can't be read.
    /// </summary>
    public Texture2D? CutTo(RecordId image, string art)
    {
        if (_cut.TryGetValue((image, art), out var cut))
            return cut;
        if (plugins.FindAsset(image, "png") is { } png && Png.Load(png) is { } data && ArtImage(art) is ({ } mask, var paper))
        {
            data.Convert(Image.Format.Rgba8);
            if (data.GetWidth() < CutSize)
            {
                var k = (CutSize + data.GetWidth() - 1) / data.GetWidth();
                data.Resize(data.GetWidth() * k, data.GetHeight() * k, Image.Interpolation.Nearest);
            }
            var (w, h) = (data.GetWidth(), data.GetHeight());
            var bytes = data.GetData();
            var outline = mask.GetData();
            var (mw, mh) = (mask.GetWidth(), mask.GetHeight());
            for (var y = 0; y < h; y++)
            {
                var my = paper.Position.Y + (y + 0.5f) / h * paper.Size.Y - 0.5f;
                for (var x = 0; x < w; x++)
                {
                    var mx = paper.Position.X + (x + 0.5f) / w * paper.Size.X - 0.5f;
                    var o = (y * w + x) * 4 + 3;
                    bytes[o] = (byte)MathF.Round(bytes[o] * AlphaAt(outline, mw, mh, mx, my) / 255f);
                }
            }
            var result = Image.CreateFromData(w, h, true, Image.Format.Rgba8, CoverageMipmaps.Chain(bytes, w, h));
            cut = new CanvasTexture { DiffuseTexture = ImageTexture.CreateFromImage(result), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        }
        return _cut[(image, art)] = cut;
    }

    /// <summary>How wide an image is made, at least, before it's cut to an outline.</summary>
    private const int CutSize = 160;

    /// <summary>An RGBA image's alpha at a point between its pixels' centres, blended between the four round it; 0 off the image.</summary>
    private static float AlphaAt(byte[] rgba, int width, int height, float x, float y)
    {
        var (x0, y0) = ((int)MathF.Floor(x), (int)MathF.Floor(y));
        var (fx, fy) = (x - x0, y - y0);
        float At(int px, int py) => px < 0 || py < 0 || px >= width || py >= height ? 0 : rgba[(py * width + px) * 4 + 3];
        return (At(x0, y0) * (1 - fx) + At(x0 + 1, y0) * fx) * (1 - fy) + (At(x0, y0 + 1) * (1 - fx) + At(x0 + 1, y0 + 1) * fx) * fy;
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
