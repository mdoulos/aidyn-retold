using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>What the UI's text is drawn in: each font's own sheet, as the original's, or a typeface drawn at the screen's resolution.</summary>
public enum TextFont
{
    Original,

    /// <summary>
    /// A typeface in place of the sheets' letters, digits and marks, with widths of its own: Marcellus SC, or a plugin's
    /// (<c>TextFace</c> in the views). The sheets' controller buttons and icons stay. The default, the user's choice (Oct 31, 2026).
    /// </summary>
    HighResolution,
}

/// <summary>
/// The fonts the core lays text out in: each font record as the plugins have it, or with the widths its glyphs take in the
/// typeface the text is drawn in (<see cref="Widths"/>), which the runtime hands over with each tick's settings
/// (<see cref="SessionSettings.TextWidths"/>). So the conversation box, the journal, a backdrop's pages and the credits wrap and
/// page their text as it's drawn.
/// </summary>
public sealed class TextFonts(PluginSet plugins)
{
    private readonly Dictionary<int, TextLayout> _layouts = [];
    private IReadOnlyDictionary<RecordId, IReadOnlyList<int>>? _widths;

    /// <summary>Each font's glyph widths in the typeface its text is drawn in, by its record, or none for its sheet's own.</summary>
    public IReadOnlyDictionary<RecordId, IReadOnlyList<int>>? Widths
    {
        get => _widths;
        set
        {
            if (ReferenceEquals(_widths, value))
                return;
            _widths = value;
            _layouts.Clear();
        }
    }

    /// <summary>The main font.</summary>
    public TextLayout Main => Get(0);

    /// <summary>The slim font the lists and details use (<c>SlimFont</c>).</summary>
    public TextLayout Slim => Get(1);

    /// <summary>Font <paramref name="n"/>, as its text is drawn.</summary>
    public TextLayout Get(int n)
    {
        if (_layouts.TryGetValue(n, out var layout))
            return layout;
        var record = plugins.Get<FontRecord>(RecordId.Decimal(RecordTypes.Font, n));
        return _layouts[n] = new TextLayout(_widths?.GetValueOrDefault(record.Id) is { } widths ? record with { Widths = widths } : record);
    }
}
