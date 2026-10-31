using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;
using Godot;

namespace AidynEnhanced.Views;

/// <summary>
/// A typeface the UI's text is drawn in instead of the fonts' sheets (<see cref="TextFont.HighResolution"/>): Marcellus SC, Roman
/// small capitals like the menus' own painted labels, or a plugin's (a <c>.ttf</c> asset of the font record, such as
/// <c>assets/aidyn/font/0.ttf</c>). Each letter, digit and mark a sheet has is drawn by the face into a copy of the sheet's cells,
/// <see cref="Density"/> texels a pixel, with a width of its own; the controller buttons and icons the sheets draw for some
/// characters stay the sheet's.
/// </summary>
/// <remarks>
/// A glyph is as tall as the sheet's capitals, 8 pixels in the main font and 7 in the slim, standing on the sheet's baseline, and
/// takes the face's advance plus <see cref="Tracking"/>, rounded, centred in it, as ReChronicled's HD font found best: the sheets'
/// widths are wide and loosely spaced. The core lays text out by the same widths (<see cref="TextFonts"/>), which the runtime
/// hands over with the settings (<see cref="Widths"/>).
/// </remarks>
public sealed class TextFace
{
    /// <summary>How many texels a pixel of the screen the face's sheets are drawn at: fine enough for a 4K window.</summary>
    public const int Density = 8;

    /// <summary>What a glyph takes beyond the face's advance, in pixels, before it's rounded.</summary>
    public const float Tracking = 0.6f;

    /// <summary>The face the game's text is drawn in, or none for the fonts' own sheets: the Text Font setting's.</summary>
    public static TextFace? Current { get; set; }

    // The marks a sheet's cell is the face's to draw: letters and digits, and these where the sheet's cell for one is that mark.
    // ':' and ';' share a cell, as '%' and '&' do; the first of each is drawn. '"', '$' and the brackets' and symbols' cells hold
    // the sheets' own icons.
    private const string Marks = ".,!-+=%/*'?:();@";

    private static readonly Dictionary<PluginSet, TextFace> Loaded = [];

    private readonly PluginSet _plugins;
    private readonly Dictionary<RecordId, FaceSheet> _sheets = [];
    private IReadOnlyDictionary<RecordId, IReadOnlyList<int>>? _widths;

    private TextFace(PluginSet plugins) => _plugins = plugins;

    /// <summary>The face for the plugins' fonts, made once.</summary>
    public static TextFace For(PluginSet plugins)
    {
        if (!Loaded.TryGetValue(plugins, out var face))
            Loaded[plugins] = face = new TextFace(plugins);
        return face;
    }

    /// <summary>The widths each of the plugins' two fonts' glyphs take in the face, by font record, for the core's layouts.</summary>
    public IReadOnlyDictionary<RecordId, IReadOnlyList<int>> Widths =>
        _widths ??= Enumerable.Range(0, 2).Select(n => _plugins.TryGet<FontRecord>(RecordId.Decimal(RecordTypes.Font, n))).OfType<FontRecord>()
            .ToDictionary(r => r.Id, r => Sheet(r).Record.Widths);

    /// <summary>A font's sheet as the face draws it, made the first time it's asked for.</summary>
    public FaceSheet Sheet(FontRecord font)
    {
        if (!_sheets.TryGetValue(font.Id, out var sheet))
            _sheets[font.Id] = sheet = Draw(font);
        return sheet;
    }

    private FaceSheet Draw(FontRecord record)
    {
        var font = Load(record);
        var server = TextServerManager.GetPrimaryInterface();
        var rid = font.GetRids()[0];
        var (capitals, baseline) = Capitals(record);
        var size = (int)Math.Round(capitals * Density * 200.0 / InkHeight(server, rid, 200, 'H'));
        var glyphSize = new Vector2I(size, 0);
        var sheet = Image.CreateEmpty(record.Columns * record.CellWidth * Density, record.Rows * record.CellHeight * Density, false, Image.Format.Rgba8);
        var widths = record.Widths.ToArray();
        var drawn = new bool[widths.Length];
        var caches = new Dictionary<long, Image>();

        foreach (var (key, cell) in record.Glyphs)
        {
            if (key.Length != 1 || cell < 0 || cell >= widths.Length || drawn[cell] || !(char.IsAsciiLetterOrDigit(key[0]) || Marks.Contains(key[0]) || key[0] == ' '))
                continue;
            var glyph = server.FontGetGlyphIndex(rid, size, key[0], 0);
            if (glyph == 0 && key[0] != ' ')
                continue;
            var advance = server.FontGetGlyphAdvance(rid, size, glyph).X / Density;
            var width = Math.Clamp((int)Math.Round(advance + Tracking), 1, record.CellWidth);
            widths[cell] = width;
            drawn[cell] = true;
            if (key[0] == ' ')
                continue;
            server.FontRenderGlyph(rid, glyphSize, glyph);
            var texture = server.FontGetGlyphTextureIdx(rid, glyphSize, glyph);
            if (texture < 0)
                continue;
            if (!caches.TryGetValue(texture, out var cache))
            {
                cache = server.FontGetTextureImage(rid, glyphSize, texture);
                cache.Convert(Image.Format.Rgba8);
                caches[texture] = cache;
            }
            // The pen at the cell's left plus half what the width has over the advance, on the baseline; the glyph's box is
            // offset from the pen, and cut to the cell.
            var cellLeft = cell / record.Rows * record.CellWidth * Density;
            var cellTop = cell % record.Rows * record.CellHeight * Density;
            var pen = new Vector2(cellLeft + (width - advance) / 2 * Density, cellTop + baseline * Density);
            var uv = server.FontGetGlyphUVRect(rid, glyphSize, glyph);
            var at = (pen + server.FontGetGlyphOffset(rid, glyphSize, glyph)).Round();
            var source = new Rect2I((Vector2I)uv.Position.Round(), (Vector2I)uv.Size.Round());
            var cellRect = new Rect2I(cellLeft, cellTop, width * Density, record.CellHeight * Density);
            var target = new Rect2I((Vector2I)at, source.Size).Intersection(cellRect);
            if (target.Size.X <= 0 || target.Size.Y <= 0)
                continue;
            source = new Rect2I(source.Position + target.Position - (Vector2I)at, target.Size);
            sheet.BlendRect(cache, source, target.Position);
        }
        sheet.GenerateMipmaps();
        var canvasTexture = new CanvasTexture
        {
            DiffuseTexture = ImageTexture.CreateFromImage(sheet),
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        return new FaceSheet(record with { Widths = widths }, canvasTexture, drawn);
    }

    /// <summary>
    /// How tall the sheet's capitals are and the row under them, its baseline, from its H: 8 and 8 in the main font, 7 and 7 in
    /// the slim. A sheet without one is taken as the main font's.
    /// </summary>
    private (int Capitals, int Baseline) Capitals(FontRecord record)
    {
        if (!record.Glyphs.TryGetValue("H", out var cell) || _plugins.FindAsset(record.Image, "png") is not { } png || Image.LoadFromFile(png) is not { } image)
            return (8, 8);
        image.Convert(Image.Format.Rgba8);
        var (left, top) = (cell / record.Rows * record.CellWidth, cell % record.Rows * record.CellHeight);
        var inked = Enumerable.Range(0, record.CellHeight)
            .Where(y => Enumerable.Range(0, record.CellWidth).Any(x => image.GetPixel(left + x, top + y).A > 0.25f)).ToList();
        return inked.Count == 0 ? (8, 8) : (inked[^1] - inked[0] + 1, inked[^1] + 1);
    }

    /// <summary>How many rows of a character's glyph at a size have ink, to find the size whose capitals are so tall.</summary>
    private static int InkHeight(TextServer server, Rid rid, int size, char c)
    {
        var glyphSize = new Vector2I(size, 0);
        var glyph = server.FontGetGlyphIndex(rid, size, c, 0);
        server.FontRenderGlyph(rid, glyphSize, glyph);
        var image = server.FontGetTextureImage(rid, glyphSize, server.FontGetGlyphTextureIdx(rid, glyphSize, glyph));
        image.Convert(Image.Format.Rgba8);
        var uv = server.FontGetGlyphUVRect(rid, glyphSize, glyph);
        var rows = 0;
        for (var y = (int)uv.Position.Y; y < (int)uv.End.Y; y++)
        {
            for (var x = (int)uv.Position.X; x < (int)uv.End.X; x++)
            {
                if (image.GetPixel(x, y).A > 0.5f)
                {
                    rows++;
                    break;
                }
            }
        }
        return Math.Max(rows, 1);
    }

    /// <summary>The face for a font: a plugin's <c>.ttf</c> asset of its record, or the game's own, Marcellus SC (SIL Open Font License).</summary>
    private FontFile Load(FontRecord record)
    {
        byte[] data;
        if (_plugins.FindAsset(record.Id, "ttf") is { } path)
            data = File.ReadAllBytes(path);
        else
        {
            using var stream = typeof(TextFace).Assembly.GetManifestResourceStream("MarcellusSC-Regular.ttf")
                ?? throw new InvalidOperationException("The views are built without their font.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            data = memory.ToArray();
        }
        return new FontFile
        {
            Data = data,
            Antialiasing = TextServer.FontAntialiasing.Gray,
            Hinting = TextServer.Hinting.None,
            SubpixelPositioning = TextServer.SubpixelPositioning.Disabled,
            Oversampling = 1,
            GenerateMipmaps = false,
        };
    }
}

/// <summary>A font's sheet as a <see cref="TextFace"/> draws it: the record with the face's widths, and the cells the face drew.</summary>
/// <param name="Texture">The cells, <see cref="TextFace.Density"/> texels a pixel, filtered smoothly.</param>
public sealed record FaceSheet(FontRecord Record, Texture2D Texture, IReadOnlyList<bool> Drawn)
{
    /// <summary>Whether the face draws a cell of the sheet, a glyph's by where it's taken from; its icons are the sheet's.</summary>
    public bool Draws(int sourceX, int sourceY)
    {
        var cell = sourceX / Record.CellWidth * Record.Rows + sourceY / Record.CellHeight;
        return cell >= 0 && cell < Drawn.Count && Drawn[cell];
    }
}
