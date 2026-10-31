using AidynEnhanced.Core.Scripts;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;

namespace AidynEnhanced.Core.Ui;

/// <summary>Text a box shows: where it starts, and the bounds it wraps at and is drawn inside.</summary>
public sealed record TextBlock(string Text, int X, int Y, ScreenRect Bounds);

/// <summary>
/// Where a conversation box puts everything on the UI screen. The runtime draws, in order: the
/// panel, the portrait's border, the portrait, the line and the choices.
/// </summary>
/// <param name="Speaker">Whose portrait shows: the line's speaker, or Alaron's once only the choices are up. None if the line has no speaker.</param>
/// <param name="Line">The line, or none once only the choices are up.</param>
/// <param name="Highlight">Which choice the cursor is on.</param>
public sealed record DialogueBoxLayout(
    FeatheredPanel Panel,
    RecordId? Speaker,
    int BorderX,
    int BorderY,
    int PortraitX,
    int PortraitY,
    TextBlock? Line,
    IReadOnlyList<TextBlock> Choices,
    int Highlight)
{
    /// <summary>The top of the panel's band.</summary>
    public int Top => Panel.Band.Top;

    /// <summary>The bottom of the panel's band.</summary>
    public int Bottom => Panel.Band.Bottom;
}

/// <summary>What A did on a page of a script (<see cref="DialogueBox.Accept"/>).</summary>
public enum DialogueAccept
{
    /// <summary>It brought up the choices behind a line that showed alone first. The script hasn't moved.</summary>
    ShowedChoices,
    /// <summary>The script took a choice, so there's a new page, or none.</summary>
    Chose,
    /// <summary>The script moved on from a line.</summary>
    Continued,
    /// <summary>It printed the rest of a line still printing (<see cref="TextReveal"/>). The script hasn't moved.</summary>
    Revealed,
}

/// <summary>
/// The box a conversation shows its pages in, at the bottom of the screen with the speaker's portrait
/// on the left (<c>DiaMenuSub::InitDefault</c>). It's for conversations
/// and the other modes that draw no backdrop; backdrops and the help menu use a full-screen image.
/// </summary>
/// <remarks>
/// <para>
/// The original lays the box out on its 320 × 240 screen, and so does this, in a frame of that size
/// at the bottom centre of the UI screen, so its text wraps as authored whatever the screen's size.
/// The original draws a translucent band right across the screen, with a gradient above it, and
/// the black bars it draws over the screen's edges for overscan cut it off (<c>Graphics::EndList</c>).
/// The port draws a floating panel instead, the band inside those bars with feathering where they
/// were, built from the original's own popup pieces. On a 4:3 screen it looks as the original's does
/// inside its bars; on a wider one it has margins either side.
/// </para>
/// <para>
/// The box grows upward to fit, and a line sits just above its choices. When a page's first choice
/// has the line's speaker, the line shows alone first, and A brings up the choices alone under
/// Alaron's portrait. Then the portrait and its border stay where the line put them, since the
/// original doesn't move them, but the box shrinks to the choices, never below its base. The cursor's choice pulses from
/// tan to a dim brown and back, a step a frame (<c>WidgetScrollMenu::Tick</c>).
/// </para>
/// <para>
/// The port can print a line a few glyphs a tick instead of showing it whole, with a blip for the runtime to sound as glyphs
/// appear (<see cref="TextReveal"/>, the user's choice): <see cref="GlyphsPerTick"/> and <see cref="Voice"/>, which the
/// session sets from the settings each tick, and at their defaults the line shows whole and silent as the original's does.
/// While a line prints, its choices wait, A prints the rest (<see cref="DialogueAccept.Revealed"/>), and
/// <see cref="RevealedGlyphs"/> is how many of the line's glyphs to draw.
/// </para>
/// </remarks>
/// <param name="font">The font the box's text is in: the main font.</param>
/// <param name="borderWidth">The portrait border image's size, which the layout is built around.</param>
/// <param name="player">Alaron's entity, whose portrait shows over choices alone (<c>IDEntInd(Alaron)</c>).</param>
public sealed class DialogueBox(TextLayout font, int borderWidth, int borderHeight, RecordId player)
{
    /// <summary>Where the box's bottom part starts, before it grows to fit.</summary>
    public const int BaseTop = 180;

    /// <summary>The font the box's text is laid out in, as it's drawn: the session hands the game's boxes theirs (<see cref="TextFonts"/>).</summary>
    public TextLayout Font { get; set; } = font;

    /// <summary>The original's screen, which the box is laid out in.</summary>
    public const int FrameWidth = 320, FrameHeight = 240;

    /// <summary>How much of the frame's edges the original covers with black bars: 16 pixels either side and 12 at the bottom (1.0 and 1.1).</summary>
    public const int OverscanSide = 16, OverscanBottom = 12;

    /// <summary>How tall the gradient above the band is: its image, 16 tall, squeezed (<c>DiaMenuSub</c>).</summary>
    public const int GradientHeight = 10;

    /// <summary>The line's colour.</summary>
    public static readonly Rgba LineColor = new(200, 200, 200, 255);

    /// <summary>The choices' colour, which the cursor's choice pulses from.</summary>
    public static readonly Rgba ChoiceColor = new(200, 180, 100, 255);

    /// <summary>What the cursor's choice pulses to.</summary>
    public static readonly Rgba HighlightColor = new(50, 45, 25, 63);

    /// <summary>How many frames the pulse takes each way.</summary>
    public const int PulseFrames = 24;

    private const int Margin = 26, BorderX = 20, PortraitInset = 6, ChoiceIndent = 10, ChoiceSpacing = 4;

    private ScriptPage? _page;
    private int _screenWidth = FrameWidth, _screenHeight = FrameHeight;
    private bool _choicesAlone;
    private int _borderY = BaseTop;
    private readonly MenuPulse _pulse = new(Cadence.FrameTicks, PulseFrames);
    private int _totalGlyphs, _revealed, _blips, _ticksSinceBlip = TextReveal.TicksBetweenBlips, _pause, _blipSpacing = TextReveal.TicksBetweenBlips;
    private float _revealAccumulated;
    private readonly List<char> _glyphChars = [];

    /// <summary>
    /// The portrait border's size (image 0x00DF, 52 × 52), which the layout is built
    /// around: the core lays the box out by it without reading the image.
    /// </summary>
    public const int BorderSize = 52;

    /// <summary>The box the game's conversations and cut-scenes show their pages in: in the main font, laid out around the portrait border.</summary>
    public static DialogueBox For(PluginSet plugins) =>
        new(new TextLayout(plugins.Get<FontRecord>(RecordId.Decimal(RecordTypes.Font, 0))), BorderSize, BorderSize, GameState.Alaron)
        {
            SpeakerCadence = speaker => speaker is { } id ? plugins.TryGet<DbRecord>(id)?.VoiceCadence : null,
        };

    /// <summary>What the box shows now, or none before the first page.</summary>
    public DialogueBoxLayout? Layout { get; private set; }

    /// <summary>The choice the cursor is on.</summary>
    public int Highlight { get; private set; }

    /// <summary>How many glyphs a tick a line prints (<see cref="TextReveal.GlyphsPerTick"/>): 0, the default, shows it whole at once.</summary>
    public float GlyphsPerTick { get; set; }

    /// <summary>Whether a blip is counted as glyphs print (<see cref="TakeBlips"/>). Off by default, as the original's box is silent.</summary>
    public bool Voice { get; set; }

    /// <summary>A speaker's cadence (<see cref="TextReveal.BlipSpacing"/>), looked up as a line starts; none, the default, is the normal one.</summary>
    public Func<RecordId?, BlipCadence?>? SpeakerCadence { get; init; }

    /// <summary>How many of the line's glyphs, in the order <see cref="TextLayout.Print"/> places them, are shown so far.</summary>
    public int RevealedGlyphs => _revealed;

    /// <summary>How far through the line the printing is, 0 to 1; 1 with the line shown whole.</summary>
    public float Progress => _totalGlyphs == 0 ? 1 : (float)_revealed / _totalGlyphs;

    /// <summary>The mood the line is said in (<see cref="TextReveal.MoodOf"/>), which its printing and its blips follow.</summary>
    public TextMood Mood { get; private set; }

    /// <summary>How the line prints and sounds under its <see cref="Mood"/>.</summary>
    public MoodPreset Preset => TextReveal.Presets[Mood];

    /// <summary>Whether the whole line is up, so its choices show and A moves on; always, with the line shown whole.</summary>
    public bool LineComplete => _revealed >= _totalGlyphs;

    /// <summary>
    /// The blips counted since the last call, and clears them: one for a glyph printed while <see cref="Voice"/> is on, at most one
    /// every <see cref="TextReveal.BlipSpacing"/> ticks for the speaker's cadence. The runtime sounds them at the speaker's pitch.
    /// </summary>
    public int TakeBlips()
    {
        var blips = _blips;
        _blips = 0;
        return blips;
    }

    /// <summary>Whether A moves the box on by itself, bringing up the choices, rather than the script.</summary>
    public bool ShowsLineBeforeChoices => _page is { Choices.Count: > 0 } page && !_choicesAlone && page.Choices[0].Speaker == page.Line.Speaker;

    /// <summary>Sets the UI screen's size, in its pixels, and lays the page out again.</summary>
    public void Resize(int width, int height)
    {
        (_screenWidth, _screenHeight) = (width, height);
        if (_page is not null)
            Lay();
    }

    /// <summary>Where the original's screen is on the UI screen: at the bottom, in the middle.</summary>
    public (int X, int Y) Frame => ((_screenWidth - FrameWidth) / 2, _screenHeight - FrameHeight);

    /// <summary>
    /// Shows a new page, with the cursor on its first choice, and starts printing its line (<see cref="GlyphsPerTick"/>). The same
    /// line shown again while it's still printing, as a conversation's refresh after a tick does, goes on where it was; shown
    /// again once it had finished, as talking to the same wanderer again does, it prints from the start.
    /// </summary>
    public DialogueBoxLayout Show(ScriptPage page)
    {
        var sameLine = _page is not null && !_choicesAlone && page.Line == _page.Line && !LineComplete;
        _page = page;
        _choicesAlone = false;
        Highlight = 0;
        _pulse.Restart();
        var layout = Lay();
        if (!sameLine)
            StartReveal(layout);
        return layout;
    }

    /// <summary>Prints the rest of the line, as A does on a line still printing.</summary>
    public void CompleteLine() => _revealed = _totalGlyphs;

    private void StartReveal(DialogueBoxLayout layout)
    {
        _glyphChars.Clear();
        if (layout.Line is { } line)
        {
            // The glyphs in Print's order, with their characters, so the printing can hold at punctuation. Print leaves out what's
            // clipped, so a line too tall for the box completes a little early, which the view's Take hides.
            foreach (var c in line.Text)
            {
                if (c != '\n' && c != ' ' && Font.Glyph(c) is not null)
                    _glyphChars.Add(c);
            }
            _totalGlyphs = Math.Min(_glyphChars.Count, Font.Print(line.Text, line.X, line.Y, line.Bounds, LineColor).Glyphs.Count);
        }
        else
        {
            _totalGlyphs = 0;
        }
        Mood = TextReveal.MoodOf(_page!.Line.Text, _page.Line.Mood);
        _blipSpacing = TextReveal.BlipSpacing(SpeakerCadence?.Invoke(_page.Line.Speaker));
        _revealed = GlyphsPerTick > 0 ? 0 : _totalGlyphs;
        _revealAccumulated = 0;
        _pause = 0;
        _ticksSinceBlip = _blipSpacing;
    }

    /// <summary>How many ticks the printing holds after the glyph at <paramref name="i"/>: the mood's pause at a sentence's end or a comma.</summary>
    private int PauseAfter(int i)
    {
        var c = _glyphChars[i];
        if (c is '.' or '!' or '?')
        {
            // An ellipsis or a "?!" holds once, after its last mark.
            var next = i + 1 < _glyphChars.Count ? _glyphChars[i + 1] : ' ';
            return next is '.' or '!' or '?' ? 0 : Preset.StopPause;
        }
        return c is ',' or ';' or ':' ? Preset.CommaPause : 0;
    }

    /// <summary>Brings up the choices under a line that showed alone first.</summary>
    public DialogueBoxLayout ShowChoices()
    {
        if (!ShowsLineBeforeChoices)
            throw new InvalidOperationException("The page has no choices waiting behind its line.");
        _choicesAlone = true;
        return Lay();
    }

    /// <summary>
    /// Whether A takes one of the page's choices rather than moving the script on: the page has choices and the script
    /// isn't waiting for A, which a random line or an animation with no actor does with only its line up.
    /// </summary>
    public static bool PicksChoice(ScriptRunner runner) => runner.Page is { Choices.Count: > 0 } && runner.Wait != ScriptWait.Player;

    /// <summary>
    /// A on the page the script shows: brings up the choices behind a line that showed alone first, else takes the
    /// cursor's choice (<see cref="PicksChoice"/>) or moves the script on.
    /// </summary>
    /// <param name="runner">The script, showing a page.</param>
    /// <param name="press">What moves the script on, as a mode takes the press: a conversation's or a cut-scene's, which ask the speaker to talk. None runs it as it is.</param>
    public DialogueAccept Accept(ScriptRunner runner, Action<Action>? press = null)
    {
        if (!LineComplete)
        {
            CompleteLine();
            return DialogueAccept.Revealed;
        }
        if (!ShowsLineBeforeChoices)
            return Advance(runner, Highlight, press);
        (press ?? Run)(() => ShowChoices());
        return DialogueAccept.ShowedChoices;
    }

    /// <summary>
    /// A on a page that has no line to show alone first, as a backdrop's or the help menu's: takes the choice
    /// <paramref name="choice"/> if the page has choices to take (<see cref="PicksChoice"/>), else moves the script on.
    /// </summary>
    /// <param name="choice">Which choice to take: where the cursor is, or for the help menu's B, the last.</param>
    /// <param name="press">As in <see cref="Accept"/>.</param>
    public static DialogueAccept Advance(ScriptRunner runner, int choice, Action<Action>? press = null)
    {
        press ??= Run;
        if (!PicksChoice(runner))
        {
            press(runner.Continue);
            return DialogueAccept.Continued;
        }
        press(() => runner.Choose(choice));
        return DialogueAccept.Chose;
    }

    private static void Run(Action advance) => advance();

    /// <summary>
    /// Puts the cursor on the choice under a point of the screen, as the mouse over the box does (the port's): true if there was one.
    /// </summary>
    public bool PointAt(int x, int y)
    {
        if (Layout is not { Choices.Count: > 0 } layout)
            return false;
        // A choice's row runs from its top to the next's, the last's to the menu's bottom; its bounds are the menu's, which all share.
        var choices = layout.Choices;
        for (var i = 0; i < choices.Count; i++)
        {
            var bounds = choices[i].Bounds;
            var bottom = i + 1 < choices.Count ? choices[i + 1].Y : bounds.Bottom;
            if (x < bounds.Left - ChoiceIndent || x >= bounds.Right || y < choices[i].Y || y >= bottom)
                continue;
            Highlight = i;
            Layout = layout with { Highlight = i };
            return true;
        }
        return false;
    }

    /// <summary>Moves the cursor, which stops at the first and last choice.</summary>
    public void MoveCursor(int by)
    {
        if (Layout is { Choices.Count: > 0 } layout)
        {
            Highlight = Math.Clamp(Highlight + by, 0, layout.Choices.Count - 1);
            Layout = layout with { Highlight = Highlight };
        }
    }

    /// <summary>
    /// Advances the cursor's pulse by one 60 Hz tick, and prints the line's next glyphs (<see cref="GlyphsPerTick"/>, at the mood's
    /// rate), holding after punctuation as the mood has it.
    /// </summary>
    public void Step()
    {
        _pulse.Step();
        _ticksSinceBlip++;
        if (LineComplete)
            return;
        if (_pause > 0)
        {
            _pause--;
            return;
        }
        // A rate such as 0.7 a tick sums a hair short of a whole glyph on its tenth tick, so a whole is taken a little early.
        _revealAccumulated += GlyphsPerTick * Preset.Rate;
        while (_revealAccumulated >= 1 - 1e-3f && _revealed < _totalGlyphs)
        {
            _revealAccumulated -= 1;
            _revealed++;
            if (Voice && _ticksSinceBlip >= _blipSpacing)
            {
                _blips++;
                _ticksSinceBlip = 0;
            }
            _pause = PauseAfter(_revealed - 1);
            if (_pause > 0)
            {
                _revealAccumulated = 0;
                break;
            }
        }
    }

    /// <summary>
    /// The cursor's choice's colour now. Each channel moves by a whole step a frame, the difference
    /// over <see cref="PulseFrames"/> rounded toward zero.
    /// </summary>
    public Rgba HighlightNow => _pulse.Now(ChoiceColor, HighlightColor);

    private DialogueBoxLayout Lay()
    {
        var page = _page!;
        var bottom = BaseTop + borderHeight;
        var textLeft = borderWidth + Margin;
        var textRight = FrameWidth - Margin;
        var top = bottom;
        var choices = new List<TextBlock>();

        var showChoices = page.Choices.Count > 0 && !ShowsLineBeforeChoices;
        if (showChoices)
        {
            var left = textLeft + ChoiceIndent;
            var menuBottom = BaseTop - 4 + borderHeight;
            var heights = page.Choices.Select(c => Font.Height(c.Text, left, textRight)).ToList();
            top = menuBottom - (heights.Sum() + ChoiceSpacing * (heights.Count - 1));
            var y = top;
            for (var i = 0; i < heights.Count; i++)
            {
                choices.Add(new TextBlock(page.Choices[i].Text, left, y, new ScreenRect(left, top, textRight, menuBottom)));
                y += heights[i] + ChoiceSpacing;
            }
        }

        TextBlock? line = null;
        if (!_choicesAlone)
        {
            // The original works in u16s, so a line too tall for the screen wraps around and sits at the base.
            var textBottom = top - 2;
            var lineTop = textBottom - Font.Height(page.Line.Text, textLeft, textRight) - 4;
            if (lineTop is < 0 or > BaseTop)
                lineTop = BaseTop;
            line = new TextBlock(page.Line.Text, textLeft, lineTop + 4, new ScreenRect(textLeft, lineTop + 4, textRight, textBottom));
            top = lineTop;
            _borderY = lineTop;
        }
        // The box never sits below its base, even with only a choice or two up.
        top = Math.Min(top, BaseTop);

        // The band ends where the original's bars cut it, and the feathering fills where they were.
        var panel = new FeatheredPanel(new ScreenRect(OverscanSide, top, FrameWidth - OverscanSide, Math.Min(bottom, FrameHeight - OverscanBottom)),
            GradientHeight, OverscanBottom, OverscanSide, OverscanSide);
        var (frameX, frameY) = Frame;
        return Layout = new DialogueBoxLayout(panel.Offset(frameX, frameY), _choicesAlone ? player : page.Line.Speaker,
            BorderX + frameX, _borderY + frameY, BorderX + PortraitInset + frameX, _borderY + PortraitInset + frameY,
            line is null ? null : Shift(line, frameX, frameY), [.. choices.Select(c => Shift(c, frameX, frameY))], Highlight);
    }

    private static TextBlock Shift(TextBlock block, int x, int y) => block with
    {
        X = block.X + x,
        Y = block.Y + y,
        Bounds = new ScreenRect(block.Bounds.Left + x, block.Bounds.Top + y, block.Bounds.Right + x, block.Bounds.Bottom + y),
    };
}
