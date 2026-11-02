using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;
using Godot;

namespace AidynEnhanced.Views;

/// <summary>
/// A font's layout and its sheet, to print with, the typeface its letters are drawn in instead, if one is (<see cref="TextFace"/>),
/// and how much smaller than its sheet's own size it's drawn (<see cref="Sized"/>). Its measures are at that size, rounded to
/// the frame's pixels.
/// </summary>
public sealed record UiFont(TextLayout Layout, Texture2D? Sheet, FaceSheet? Face = null, float Scale = 1)
{
    public int LineHeight => Scaled(Layout.LineHeight());

    public int Width(string text) => Scaled(Layout.Width(text));

    /// <summary>How tall text is when it starts at <paramref name="x"/> and wraps at <paramref name="right"/>, or 0 if it's empty or has no room.</summary>
    public int Height(string text, int x, int right) => Scaled(Layout.Height(text, x, x + (int)((right - x) / Scale)));

    /// <summary>
    /// The font drawn with its capitals <paramref name="capitals"/> of the frame's pixels tall. Only the typeface is drawn smaller:
    /// the sheets' own letters are pixels, so a font without a face stays its size.
    /// </summary>
    public UiFont Sized(float capitals) => Face is { } face ? this with { Scale = capitals / face.Capitals } : this;

    /// <summary>
    /// How far down the text drawn smaller is moved, so the middle of its capitals is where the font's own size has them: text
    /// drawn where a layout put the full-size text sits in the middle of the same row.
    /// </summary>
    public float Drop => Face is { } face ? (1 - Scale) * Middle : 0;

    /// <summary>How far under where text is drawn its capitals' middle is, at any size.</summary>
    public float Middle => Face is { } face ? face.Baseline - face.Capitals / 2f : Layout.Font.CellHeight / 2f;

    /// <summary>How tall its capitals are drawn, in the frame's pixels.</summary>
    public float Capitals => (Face?.Capitals ?? Layout.Font.CellHeight) * Scale;

    /// <summary>How wide text is on one line, unrounded, for placing it to a fraction of a pixel.</summary>
    public float ExactWidth(string text) => Layout.Width(text) * Scale;

    private int Scaled(int length) => Scale == 1 ? length : (int)MathF.Round(length * Scale);
}

/// <summary>
/// The menus' widget kit, drawn immediately on a <see cref="UiScreen"/>'s canvas in its pixels: text
/// in either font, UI images whole, scaled or cut out, the original's parchment popup, and scrolling
/// lists with a cursor and arrows. Views call it from <c>_Draw</c>.
/// </summary>
public sealed class UiPainter
{
    /// <summary>The colour of the arrows beside a list's title.</summary>
    public static readonly Rgba ArrowColor = new(225, 225, 225, 255);

    private readonly PluginSet _plugins;
    private readonly UiFont?[] _fonts = new UiFont?[2];
    private TextFace? _face;
    private bool _original;

    /// <summary>
    /// Whether the menus look as the original's do, Graphic Quality's Original (<c>Settings.OriginalLook</c>), which the runtime sets,
    /// the user's choice (Nov 2, 2026): the text in the fonts' own size (<see cref="TextSize"/>), the original's feathered black boxes
    /// rather than slips (<see cref="Slips"/>), no sheets of parchment (<see cref="Slip"/>), the tabs' own images at the original's
    /// height (<see cref="Tab"/>), and the original's images, not a mod's (<see cref="UiImages.Get"/>). The runtime draws the text in
    /// the fonts' sheets too (<see cref="TextFace.Current"/>).
    /// </summary>
    public static bool Original { get; set; }

    public UiPainter(PluginSet plugins)
    {
        _plugins = plugins;
        Images = new UiImages(plugins);
    }

    public UiImages Images { get; }

    /// <summary>
    /// How tall the capitals of most of the menus' text are, in the frame's pixels, the user's choice (Nov 2, 2026): the size the
    /// Party tab's level was drawn at, the slim font's 7 at 0.6, which reads better on a fine screen than the fonts' own size, kept
    /// as large as it was for the console's pixels. A heading's are larger, and a tab's caption's smaller.
    /// </summary>
    public const float StandardText = 4.2f, HeadingText = 5.6f, CaptionText = 2.94f;

    /// <summary>
    /// How tall <see cref="Main"/>'s and <see cref="Slim"/>'s capitals are drawn, in the frame's pixels: <see cref="StandardText"/>
    /// in the menus, so the two fonts' text is one size, or null for the fonts' own size, which the screens the menus' size hasn't
    /// reached yet keep.
    /// </summary>
    public float? TextSize
    {
        get => Original ? null : field;
        set
        {
            field = value;
            Array.Clear(_fonts);
        }
    }

    /// <summary>The main font, and the slim one the lists and details use (<c>SlimFont</c>), as the game's text is drawn now, at <see cref="TextSize"/>.</summary>
    public UiFont Main => Font(0);

    public UiFont Slim => Font(1);

    /// <summary>Font <paramref name="n"/> in its sheet, or in the typeface the Text Font setting draws text in (<see cref="TextFace.Current"/>).</summary>
    private UiFont Font(int n)
    {
        if (TextFace.Current != _face || Original != _original)
        {
            (_face, _original) = (TextFace.Current, Original);
            Array.Clear(_fonts);
        }
        if (_fonts[n] is { } font)
            return font;
        var record = _plugins.Get<FontRecord>(RecordId.Decimal(RecordTypes.Font, n));
        var face = _face?.Sheet(record);
        font = new UiFont(new TextLayout(face?.Record ?? record), Images.Get(record.Image), face);
        return _fonts[n] = TextSize is { } size ? font.Sized(size) : font;
    }

    /// <summary>
    /// Prints text from (x, y), wrapping at <paramref name="bounds"/>' right edge and drawing only
    /// what's inside them, and returns how tall it is (<c>WidgetClipText</c>). A font drawn smaller is laid out at its own size
    /// within the bounds made larger to match, and drawn shrunk toward (x, y) and moved down its <see cref="UiFont.Drop"/>.
    /// </summary>
    /// <param name="clip">A box the glyphs are cut to besides, which doesn't move the wrap, as a widget group's is.</param>
    public int Text(CanvasItem canvas, UiFont font, string text, int x, int y, ScreenRect bounds, Rgba color, Rect2I? clip = null) =>
        Text(canvas, font, text, x, y, 0, bounds, color, clip);

    /// <summary>
    /// Prints one line from a point placed to a fraction of a pixel, unbounded but by the screen: letters set one at a time, as a name
    /// being entered is, where whole pixels spaced them unevenly once the text was drawn smaller.
    /// </summary>
    public int Text(CanvasItem canvas, UiFont font, string text, float x, int y, Rgba color)
    {
        var whole = (int)MathF.Floor(x);
        return Text(canvas, font, text, whole, y, x - whole, new ScreenRect(-4096, -4096, 4096, 4096), color, null);
    }

    /// <param name="nudge">How far right of <paramref name="x"/>, less than a pixel, the text is drawn.</param>
    private int Text(CanvasItem canvas, UiFont font, string text, int x, int y, float nudge, ScreenRect bounds, Rgba color, Rect2I? clip)
    {
        var (scale, drop) = (font.Scale, font.Drop);
        if (scale != 1)
        {
            // The foot isn't moved up by the drop, so text moved down into a row laid out at the font's size isn't cut off by it.
            int Across(int edge) => x + (int)Math.Clamp((edge - x) / scale, -8192, 8192);
            int Down(int edge, float by) => y + (int)Math.Clamp((edge - y - by) / scale, -8192, 8192);
            bounds = new ScreenRect(Across(bounds.Left), Down(bounds.Top, drop), Across(bounds.Right), Down(bounds.Bottom, 0));
        }
        var run = font.Layout.Print(text, x, y, bounds, color);
        foreach (var glyph in run.Glyphs)
        {
            var rect = new Rect2(x + nudge + (glyph.X - x) * scale, y + drop + (glyph.Y - y) * scale, glyph.Width * scale, glyph.Height * scale);
            Glyph(canvas, font, glyph, rect, clip, Tint(glyph.Color));
        }
        return scale == 1 ? run.Height : (int)MathF.Round(run.Height * scale);
    }

    /// <summary>
    /// Draws a placed glyph, cut to a clip if there's one: from the typeface's sheet where it draws the glyph's cell, its texels
    /// <see cref="TextFace.Density"/> a pixel, and else from the font's own sheet, pixel for pixel.
    /// </summary>
    public static void Glyph(CanvasItem canvas, UiFont font, PlacedGlyph glyph, Rect2I? clip, Color color) =>
        Glyph(canvas, font, glyph, new Rect2(glyph.X, glyph.Y, glyph.Width, glyph.Height), clip, color);

    /// <summary>A placed glyph drawn over a rectangle, as a font drawn smaller draws it.</summary>
    private static void Glyph(CanvasItem canvas, UiFont font, PlacedGlyph glyph, Rect2 rect, Rect2I? clip, Color color)
    {
        var source = new Rect2(glyph.SourceX, glyph.SourceY, glyph.Width, glyph.Height);
        if (font.Face is { } face && face.Draws(glyph.SourceX, glyph.SourceY))
            DrawCut(canvas, face.Texture, rect, new Rect2(source.Position * TextFace.Density, source.Size * TextFace.Density), clip, color, sampled: false);
        else if (font.Sheet is { } sheet)
            DrawCut(canvas, sheet, rect, source, clip, color, sampled: false);
    }

    /// <summary>
    /// A message box at the centre of a screen of the size: a feathered black box
    /// <paramref name="width"/> + 8 across and <paramref name="height"/> down, with the text 4 in, in the
    /// main font.
    /// </summary>
    public void MessageBox(CanvasItem canvas, Vector2I screen, string text, int width, int height, Rgba color)
    {
        var x0 = (screen.X - width) / 2;
        var y0 = (screen.Y - height) / 2;
        Box(canvas, new ScreenRect(x0, y0, x0 + width + 8, y0 + height), question: false);
        Text(canvas, Main, text, x0 + 4, y0 + 4, new ScreenRect(x0 + 4, y0, x0 + 4 + width, y0 + height + 4), Slips ? Ink : color);
    }

    /// <summary>
    /// A message fitted to its text: as wide as the text up to
    /// <paramref name="wrap"/>, wrapping there, and as tall as it wraps plus 5, with
    /// <paramref name="extraWidth"/> and <paramref name="extraHeight"/> more.
    /// </summary>
    public void Message(CanvasItem canvas, Vector2I screen, string text, int wrap, Rgba color, int extraWidth = 0, int extraHeight = 0)
    {
        var width = Math.Min(Main.Width(text), wrap);
        MessageBox(canvas, screen, text, width + extraWidth, Main.Height(text, 0, width) + 5 + extraHeight, color);
    }

    /// <summary>
    /// A menu's message: fitted to its text and off-white, or in a box of its own size and white (<see cref="MenuMessage.Box"/>);
    /// a crafting popup's result has 16 more across and 5 more down (<c>WidgetCrafting::TextPopup</c>).
    /// </summary>
    public void Message(CanvasItem canvas, Vector2I screen, MenuMessage message)
    {
        if (message.Box is { } box)
            MessageBox(canvas, screen, message.Text, box.Width, box.Height, MenuMessage.White);
        else
            Message(canvas, screen, message.Text, message.Wrap, MenuMessage.Color, message.FromCrafting ? 16 : 0, message.FromCrafting ? 5 : 0);
    }

    /// <summary>A menu's question over choices, with the cursor's option in <paramref name="cursor"/>.</summary>
    public void Choice(CanvasItem canvas, Vector2I screen, MenuChoice choice, Rgba cursor) =>
        Choice(canvas, screen, choice.Text, choice.Width, choice.Gap, choice.Color, choice.ChoiceColor, choice.Choices, cursor);

    /// <summary>The popups a menu has open that are drawn over the screen: its question, then its message over that.</summary>
    public void Overlay(CanvasItem canvas, Vector2I screen, MenuOverlay overlay)
    {
        if (overlay.Choice is { } choice)
            Choice(canvas, screen, choice, overlay.Highlight);
        if (overlay.Message is { } message)
            Message(canvas, screen, message);
    }

    /// <summary>
    /// A question over choices (<c>WidgetChoiceDia::Update</c>): the question wrapped to
    /// <paramref name="width"/> and its options under it, <paramref name="gap"/> apart, all centred on the
    /// screen on a feathered panel as wide as the popup, with the cursor's option in <paramref name="cursor"/>.
    /// </summary>
    public void Choice(CanvasItem canvas, Vector2I screen, string text, int width, int gap, Rgba textColor, Rgba optionColor,
        MenuList<string> options, Rgba cursor)
    {
        var font = Main;
        var (left, top, right, choicesTop) = ChoiceLayout(screen, text, width, gap, options.Rows.Count);
        var rows = options.Rows;
        var rowHeight = font.LineHeight;
        var bottom = choicesTop + rows.Count * rowHeight;
        Box(canvas, new ScreenRect(left, top, right, bottom), question: true);
        Text(canvas, font, text, left, top, new ScreenRect(left, top, right, choicesTop), Slips ? Ink : textColor);
        for (var i = 0; i < rows.Count; i++)
        {
            var color = i == options.Cursor ? Slips ? RedInk : cursor : Slips ? OptionInk : optionColor;
            Text(canvas, font, rows[i], left - 1, choicesTop + i * rowHeight, new ScreenRect(left - 1, choicesTop, right + 1, bottom), color);
        }
    }

    /// <summary>
    /// Whether messages and questions are loose slips of paper, the user's art and choice (Nov 2, 2026; the menus' own), with their
    /// text in ink, a question's choice in red ink, and a wax seal on a question, which has to be answered; else the original's
    /// feathered black boxes with their own colours.
    /// </summary>
    public bool Slips
    {
        get => field && !Original;
        set;
    }

    /// <summary>The ink a slip's text is in, the menus' rows' colour, and a choice's on it, the red of the art's flourish and seal.</summary>
    public static readonly Rgba Ink = new(68, 42, 34, 255), RedInk = new(150, 34, 28, 255);

    /// <summary>The ink a question's choices that aren't the cursor's are in on a slip, fainter.</summary>
    public static readonly Rgba OptionInk = Ink with { A = 170 };

    /// <summary>A message's or question's box: a slip round the band (<see cref="Slips"/>), sealed for a question, or a feathered black box.</summary>
    public void Box(CanvasItem canvas, ScreenRect band, bool question)
    {
        if (!Slips)
        {
            Images.DrawPanel(canvas, Images.Around(band));
            return;
        }
        var paper = new Rect2(band.Left - SlipSide, band.Top - SlipTop, band.Right - band.Left + 2 * SlipSide, band.Bottom - band.Top + SlipTop + SlipFoot);
        Slip(canvas, paper);
        if (question && UiImages.Art(Seal) is { } seal)
            canvas.DrawTextureRect(seal, new Rect2(paper.End.X - SealSize * 0.7f, paper.Position.Y - SealSize * 0.3f, SealSize, SealSize), false, White);
    }

    /// <summary>
    /// How far a slip's paper reaches past its text's band at the sides, the top and the foot, clear of its torn edges, the foot further
    /// for the text drawn smaller sitting low in its rows; and how big a question's seal is, over the slip's top right corner.
    /// </summary>
    private const float SlipSide = 12, SlipTop = 7, SlipFoot = 10, SealSize = 18;

    private const string Seal = "ui-wax-seal.png";

    /// <summary>
    /// The sheets of parchment, the user's art (Nov 2, 2026), each drawn for its use, its colour and border to match the menu's
    /// parchment: a wide one, a small square, a large square and a tall one. Each paper in the menus is laid from the one its use
    /// names, or else the one nearest its shape (<see cref="SheetFor"/>).
    /// </summary>
    private static readonly string[] Sheets = [WideSheet, SquareSheet, LargeSheet, TallSheet];

    /// <summary>The wide sheet, 1800 × 800: the Party tab's header and the gold's, and the wide papers, the names on slips and the System list's rows.</summary>
    public const string WideSheet = "parchment-1800x800.png";

    /// <summary>
    /// The small square sheet, 500 × 500: the Party tab's tabs, and the portraits' frames, which a portrait is cut to the shape of
    /// (<see cref="UiImages.CutTo"/>).
    /// </summary>
    public const string SquareSheet = "parchment-500x500.png";

    /// <summary>The large square sheet, 1800 × 1800: the silhouette's, under the equipment.</summary>
    public const string LargeSheet = "parchment-1800x1800.png";

    /// <summary>The tall sheet, 1800 × 2150: the Party tab's page, which the lists are on.</summary>
    public const string TallSheet = "parchment-1800x2150.png";

    /// <summary>The sheet whose paper is nearest a rectangle's shape, by the ratio of their sides.</summary>
    public static string SheetFor(Vector2 size)
    {
        var shape = size.X / Math.Max(size.Y, 1e-3f);
        return Sheets.MinBy(sheet => UiImages.ArtImage(sheet).Paper is { Size: var p } && p.Y > 0 ? MathF.Abs(MathF.Log(p.X / p.Y / shape)) : float.MaxValue)!;
    }

    /// <summary>Where a sheet is drawn so its paper covers a rectangle: the whole picture, stretched alike.</summary>
    public static Rect2 SheetRect(string sheet, Rect2 paper)
    {
        var (image, at) = UiImages.ArtImage(sheet);
        if (image is null || at.Size.X <= 0)
            return paper;
        var scale = paper.Size / at.Size;
        return new Rect2(paper.Position - at.Position * scale, new Vector2(image.GetWidth(), image.GetHeight()) * scale);
    }

    /// <summary>
    /// The shadow under a sheet of paper, soft as it lies on the paper under it: the sheet again, dark and faint, moved down and right a
    /// little three times, each fainter.
    /// </summary>
    private static readonly (Vector2 By, float Alpha)[] Shadow = [(new(0.5f, 0.7f), 0.16f), (new(1f, 1.4f), 0.1f), (new(1.6f, 2.2f), 0.06f)];

    private static readonly Color ShadowColor = new(0.16f, 0.1f, 0.05f);

    /// <summary>
    /// A loose sheet of parchment, the user's art, the one nearest the rectangle's shape or <paramref name="sheet"/>, stretched so its
    /// paper covers the rectangle, with a soft shadow under it, in a colour.
    /// </summary>
    public void Slip(CanvasItem canvas, Rect2 paper, Rgba? color = null, string? sheet = null)
    {
        // The original has none.
        if (Original)
            return;
        sheet ??= SheetFor(paper.Size);
        if (UiImages.Art(sheet) is not { } texture)
            return;
        var drawn = SheetRect(sheet, paper);
        var tint = color is { } c ? Tint(c) : White;
        foreach (var (by, alpha) in Shadow)
            canvas.DrawTextureRect(texture, drawn with { Position = drawn.Position + by }, false, ShadowColor with { A = alpha * tint.A });
        canvas.DrawTextureRect(texture, drawn, false, tint);
    }

    /// <summary>
    /// Draws a frame over a rectangle in nine slices, as a frame for any box: its corners, <paramref name="corner"/> of its pixels
    /// square, drawn <paramref name="drawn"/> square and keeping their shape; its edges between them stretched along; and its middle
    /// stretched over the rest.
    /// </summary>
    public void NineSlice(CanvasItem canvas, Texture2D texture, Rect2 rect, float corner, float drawn, Rgba? color = null)
    {
        var size = texture.GetSize();
        float[] sx = [0, corner, size.X - corner, size.X], sy = [0, corner, size.Y - corner, size.Y];
        float[] dx = [rect.Position.X, rect.Position.X + drawn, rect.End.X - drawn, rect.End.X];
        float[] dy = [rect.Position.Y, rect.Position.Y + drawn, rect.End.Y - drawn, rect.End.Y];
        var tint = color is { } c ? Tint(c) : White;
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                var target = new Rect2(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                if (target.Size.X > 0 && target.Size.Y > 0)
                    canvas.DrawTextureRectRegion(texture, target, new Rect2(sx[i], sy[j], sx[i + 1] - sx[i], sy[j + 1] - sy[j]), tint);
            }
        }
    }

    /// <summary>The choice of a question under a point on the screen it's centred on, as <see cref="Choice(CanvasItem, Vector2I, MenuChoice, Rgba)"/> draws it.</summary>
    public int? ChoiceAt(Vector2I screen, MenuChoice choice, Vector2 at)
    {
        var (left, _, right, choicesTop) = ChoiceLayout(screen, choice.Text, choice.Width, choice.Gap, choice.Choices.Rows.Count);
        var row = (int)Math.Floor((at.Y - choicesTop) / Main.LineHeight);
        return at.X >= left && at.X < right && row >= 0 && row < choice.Choices.Rows.Count ? row : null;
    }

    /// <summary>A question's sides, its top and its choices' top, all centred on the screen.</summary>
    private (int Left, int Top, int Right, int ChoicesTop) ChoiceLayout(Vector2I screen, string text, int width, int gap, int choices)
    {
        var half = width / 2;
        var centre = new Vector2I(screen.X / 2, screen.Y / 2);
        var (left, right) = (centre.X - half, centre.X + half);
        var textHeight = Main.Height(text, left, right);
        var top = centre.Y - (choices * Main.LineHeight + textHeight + gap) / 2;
        return (left, top, right, top + textHeight + gap);
    }

    /// <summary>Prints one line unbounded but by the screen, and cut to <paramref name="clip"/> if there's one.</summary>
    public int Text(CanvasItem canvas, UiFont font, string text, int x, int y, Rgba color, Rect2I? clip = null) =>
        Text(canvas, font, text, x, y, new ScreenRect(-4096, -4096, 4096, 4096), color, clip);

    /// <summary>An image's size, or 0 × 0 if no plugin has it.</summary>
    public Vector2I Size(RecordId image) => Images.Get(image) is { } texture ? (Vector2I)texture.GetSize() : Vector2I.Zero;

    /// <summary>
    /// Draws an image at (x, y), at its own size or scaled to <paramref name="size"/>, as a texture
    /// rectangle times a colour (<c>WidgetBorg8</c>). Scaled images are filtered, as the console's are.
    /// </summary>
    /// <param name="mirror">Draws it flipped left to right.</param>
    /// <param name="clip">A box it's cut to, cropped rather than squeezed; not with <paramref name="mirror"/>.</param>
    public void Image(CanvasItem canvas, RecordId image, int x, int y, Vector2I? size = null, Rgba? color = null, bool mirror = false,
        Rect2I? clip = null)
    {
        if (Images.Get(image) is not { } texture)
            return;
        var drawn = size ?? (Vector2I)texture.GetSize();
        var tint = color is { } c ? Tint(c) : White;
        if (clip is not null && !mirror)
        {
            DrawCut(canvas, texture, new Rect2(x, y, drawn), new Rect2(Vector2.Zero, texture.GetSize()), clip, tint);
            return;
        }
        // A rectangle of negative width draws its texture flipped, in the same place.
        var rect = new Rect2(x, y, mirror ? -drawn.X : drawn.X, drawn.Y);
        var source = new Rect2(Vector2.Zero, texture.GetSize());
        canvas.DrawTextureRectRegion(Filtered(texture, drawn, source), rect, Sampled(source, drawn), tint);
    }

    /// <summary>A whole texture stretched over a rectangle, filtered as the console's are, and cut to a clip if there's one.</summary>
    public void Texture(CanvasItem canvas, Texture2D texture, Rect2 rect, Rect2I? clip = null) =>
        DrawCut(canvas, texture, rect, new Rect2(Vector2.Zero, texture.GetSize()), clip, White);

    /// <summary>
    /// Draws a texture's source rectangle over a rectangle, cut to a clip: what's outside it is cropped off, the
    /// texels left where they were.
    /// </summary>
    /// <param name="sampled">Whether to sample a scaled image as the RDP does (<see cref="Sampled"/>); glyphs are drawn 1:1.</param>
    private static void DrawCut(CanvasItem canvas, Texture2D texture, Rect2 rect, Rect2 source, Rect2I? clip, Color color, bool sampled = true)
    {
        if (clip is { } box)
        {
            var cut = rect.Intersection(new Rect2(box.Position, box.Size));
            if (cut.Size.X <= 0 || cut.Size.Y <= 0)
                return;
            var texels = source.Size / rect.Size;
            source = new Rect2(source.Position + (cut.Position - rect.Position) * texels, cut.Size * texels);
            rect = cut;
        }
        canvas.DrawTextureRectRegion(Filtered(texture, rect.Size, source), rect, sampled ? Sampled(source, rect.Size) : source, color);
    }

    /// <summary>
    /// Draws an image at (x, y) at its own size, with what's painted into it covered by <paramref name="patches"/> of
    /// plain parchment (<see cref="UiImages.Plain"/>), times a colour. A tab beside the one showing is faded by it.
    /// </summary>
    public void Plain(CanvasItem canvas, RecordId image, IReadOnlyList<PlainImage.Patch> patches, int x, int y, Rgba color)
    {
        if (Images.Plain(image, patches) is { } texture)
            canvas.DrawTextureRect(texture, new Rect2(x, y, texture.GetSize()), false, Tint(color));
    }

    /// <summary>
    /// Draws a pause menu tab's image at the frame's top left, times a colour, or its plain version (<see cref="Plain"/>), as a tab
    /// beside the one showing is veiled: made taller to run the screen's height (<see cref="UiImages.Tall"/>), its torn edges past the original's screen and everything painted in where it was. A mod's
    /// blank parchment, when there's one, is drawn instead, with nothing painted in (<see cref="UiImages.OnParchment"/>).
    /// </summary>
    /// <param name="patches">What covers the painted layout in the plain version, which the taller image's gaps are filled from.</param>
    public void Tab(CanvasItem canvas, PauseTab tab, IReadOnlyList<PlainImage.Patch> patches, Rgba? color = null, bool plain = false)
    {
        var image = MenuImages.Tab(tab);
        var tint = color is { } c ? Tint(c) : White;
        if (!Original && Images.OnParchment(tab) is { } laid)
            canvas.DrawTextureRect(laid, new Rect2(Vector2.Zero, laid.GetSize() / ParchmentSwap.Scale), false, tint);
        else if (!Original && Images.Tall(image, patches, plain) is { } tall)
            canvas.DrawTextureRect(tall, new Rect2(0, -TallImage.Above, tall.GetSize()), false, tint);
        else if (plain)
            Plain(canvas, image, patches, 0, 0, color ?? new Rgba(255, 255, 255, 255));
        else
            Image(canvas, image, 0, 0, color: color);
    }

    /// <summary>
    /// Draws one of the HUD's images: at its place in quarter pixels, scaled or stretched, times its colour, and cut to
    /// its clip, inclusive, which crops the image rather than squeezing it (<c>WidgetBorg8::Render</c>).
    /// </summary>
    public void Sprite(CanvasItem canvas, HudSprite sprite)
    {
        if (Images.Get(sprite.Image) is not { } texture)
            return;
        var source = new Rect2(Vector2.Zero, texture.GetSize());
        var size = sprite.Size is var (w, h) ? new Vector2(w, h) : source.Size * sprite.Scale;
        var rect = new Rect2(sprite.X, sprite.Y, size);
        if (sprite.Clip is { } c)
        {
            var cut = rect.Intersection(new Rect2(c.Left, c.Top, c.Right - c.Left + 1, c.Bottom - c.Top + 1));
            if (cut.Size.X <= 0 || cut.Size.Y <= 0)
                return;
            var texels = source.Size / size;
            source = new Rect2((cut.Position - rect.Position) * texels, cut.Size * texels);
            rect = cut;
        }
        canvas.DrawTextureRectRegion(Filtered(texture, rect.Size, source), rect, Sampled(source, rect.Size), Tint(sprite.Color));
    }

    /// <summary>
    /// Where to read a source rectangle from so that scaling it samples as the RDP does: its first
    /// pixel on the first texel's centre and each next one a step of the scale on, where Godot's
    /// first sample is half a step in.
    /// </summary>
    private static Rect2 Sampled(Rect2 source, Vector2 drawn)
    {
        var step = source.Size / drawn;
        return new Rect2(source.Position + (Vector2.One - step) / 2, source.Size);
    }

    // Weakly, so the editor's textures, loaded again with its plugins, let their filtered copies go.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Texture2D, CanvasTexture> Smooth = [];

    /// <summary>
    /// The texture to draw a source rectangle of over a rectangle so big with: as it is where it's drawn at its own size, so a screen
    /// drawn finer than its pixels magnifies it pixel for pixel (<see cref="UiScreen.Oversampling"/>), and filtered linearly where it's
    /// scaled, as the console filters it.
    /// </summary>
    public static Texture2D Filtered(Texture2D texture, Vector2 drawn, Rect2 source)
    {
        // A canvas texture brings its own filter, as a typeface's sheet does.
        if (texture is CanvasTexture || (drawn.Abs() - source.Size).LengthSquared() < 1e-6f)
            return texture;
        return Smooth.GetValue(texture, t => new CanvasTexture { DiffuseTexture = t, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
    }

    /// <summary>
    /// Draws part of an image: <paramref name="source"/>, in the image's pixels, at (x, y) at its own
    /// size or scaled to <paramref name="size"/>. The journal cuts its parchment's faded edges from the book's image with it.
    /// </summary>
    public void Cut(CanvasItem canvas, RecordId image, Rect2I source, int x, int y, Vector2I? size = null, Rgba? color = null, bool mirror = false)
    {
        if (Images.Get(image) is not { } texture)
            return;
        var drawn = size ?? source.Size;
        canvas.DrawTextureRectRegion(Filtered(texture, drawn, source), new Rect2(x, y, mirror ? -drawn.X : drawn.X, drawn.Y), Sampled(source, drawn),
            color is { } c ? Tint(c) : White);
    }

    /// <summary>
    /// Draws an image cut off at a right edge, as a widget's bounds cut it: the hit points bar is
    /// drawn to the share of hit points left.
    /// </summary>
    public void ImageTo(CanvasItem canvas, RecordId image, int x, int y, Vector2I size, int right, Rgba? color = null)
    {
        if (Images.Get(image) is not { } texture || right <= x)
            return;
        var shown = Math.Min(right - x, size.X);
        var source = new Rect2(0, 0, texture.GetSize().X * shown / size.X, texture.GetSize().Y);
        canvas.DrawTextureRectRegion(Filtered(texture, new Vector2(shown, size.Y), source), new Rect2(x, y, shown, size.Y), source,
            color is { } c ? Tint(c) : White);
    }

    /// <summary>The parchment popup at (x, y), with its shadow 5 to the right and 2 down (<c>WidgetMenuPopup</c>).</summary>
    public void Parchment(CanvasItem canvas, int x, int y)
    {
        Image(canvas, MenuImages.ParchmentShadow, x + 5, y + 2);
        Image(canvas, MenuImages.Parchment, x, y);
    }

    /// <summary>
    /// Draws a list's rows in view, from (x, y), a row every <paramref name="rowHeight"/>, with the
    /// cursor's row in <paramref name="cursor"/> and the rest in <paramref name="color"/>
    /// (<c>WidgetFastScrollMenu::Update</c>).
    /// </summary>
    /// <param name="draw">Draws a row at its top, in its colour.</param>
    public void List<TRow>(MenuList<TRow> list, ListLayout layout, Rgba color, Rgba cursor, Action<TRow, int, Rgba> draw)
    {
        var last = Math.Min(list.Rows.Count, list.First + list.Visible);
        for (var i = list.First; i < last; i++)
            draw(list.Rows[i], layout.Bounds.Top + (i - list.First) * layout.RowHeight, i == list.Cursor ? cursor : color);
    }

    /// <summary>
    /// The arrows that show a list has rows above or below: up at <paramref name="up"/> and down at
    /// <paramref name="down"/>, each drawn while rows are hidden its way (<c>WidgetScrollArrows</c>).
    /// </summary>
    public void Arrows<TRow>(CanvasItem canvas, MenuList<TRow> list, ListLayout layout) => Arrows(canvas, list.MoreAbove, list.MoreBelow, layout);

    /// <summary>The arrows at a list's place, for rows hidden above and below.</summary>
    public void Arrows(CanvasItem canvas, bool above, bool below, ListLayout layout)
    {
        if (above)
            Image(canvas, MenuImages.ScrollUp, layout.UpArrow.X, layout.UpArrow.Y, color: ArrowColor);
        if (below)
            Image(canvas, MenuImages.ScrollDown, layout.DownArrow.X, layout.DownArrow.Y, color: ArrowColor);
    }

    public static Color Color8(Rgba c) => Color.Color8(c.R, c.G, c.B, c.A);

    /// <summary>
    /// How opaque what's drawn is, from 0 to 1, times each colour's own alpha: a page of the System tab fading in or
    /// out (<c>Widget.SetAlpha</c> on the page). The message boxes and choices aren't faded by it.
    /// </summary>
    public float Opacity { get; set; } = 1;

    /// <summary>A colour as it's drawn, its alpha times <see cref="Opacity"/>.</summary>
    public Color Tint(Rgba c) => Color8(c) with { A = c.A / 255f * Opacity };

    private Color White => new(1, 1, 1, Opacity);
}
