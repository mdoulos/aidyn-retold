using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;

namespace AidynRetold.Core.Ui;

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
