using AidynRetold.Core;
using AidynRetold.Core.Scripts;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Views;

/// <summary>
/// Draws a conversation's box (<see cref="DialogueBox"/>) on a <see cref="UiScreen"/>'s canvas, in
/// the game's main font, as <c>DiaMenuSub</c>'s widgets do.
/// </summary>
public partial class ConversationBoxView : Control
{
    // Engine code's literals: the portrait's border and the portrait of a speaker without one.
    private static readonly RecordId BorderImage = RecordId.Hex(RecordTypes.Image, 0x00DF);
    private static readonly RecordId FallbackPortrait = RecordId.Hex(RecordTypes.Image, 0x0125);

    private readonly PluginSet _plugins;
    private readonly UiPainter _painter;
    private readonly UiImages _images;
    private readonly Texture2D? _border;

    public ConversationBoxView(PluginSet plugins)
    {
        _plugins = plugins;
        _painter = new UiPainter(plugins) { TextSize = UiPainter.LargeText };
        _images = _painter.Images;
        Name = "Conversation";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _border = _images.Get(BorderImage);
        var border = _border?.GetSize() ?? new Vector2(52, 52);
        Box = new DialogueBox(_painter.Main.Layout, (int)border.X, (int)border.Y, GameState.Alaron);
    }

    /// <summary>
    /// The box drawn: its own by default, as the editor's script page uses it, and in the game trek's or the cut-scene's
    /// (<see cref="Core.Trek.TrekLoop.Box"/>, <see cref="Core.Scripts.CutScene.Box"/>), which the core lays out and whose
    /// cursor the presses move; this sizes it to the UI screen and draws it.
    /// </summary>
    public DialogueBox Box
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
                return;
            field = value;
            if (Size.X > 0 && Size.Y > 0)
                value.Resize((int)Size.X, (int)Size.Y);
            QueueRedraw();
        }
    }

    /// <summary>Whether this view steps the box's pulse each tick: its own box's; the game's boxes are stepped by trek and the cut-scene.</summary>
    public bool StepsBox { get; set; } = true;

    /// <summary>Shows a page.</summary>
    public void Show(ScriptPage page)
    {
        Box.Show(page);
        Visible = true;
        QueueRedraw();
    }

    /// <summary>A on the page: <see cref="DialogueBox.Accept"/>, redrawing once it has brought up the choices behind a line that showed alone first.</summary>
    public DialogueAccept Accept(ScriptRunner runner, Action<Action>? press = null)
    {
        var accepted = Box.Accept(runner, press);
        if (accepted == DialogueAccept.ShowedChoices)
            QueueRedraw();
        return accepted;
    }

    /// <summary>Puts the cursor on a choice.</summary>
    public void Point(int choice)
    {
        Box.MoveCursor(choice - Box.Highlight);
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        // The UI screen's canvas is as wide as the window's shape, so the box follows it.
        if (what == NotificationResized)
        {
            Box.Resize((int)Size.X, (int)Size.Y);
            QueueRedraw();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Its own box is laid out in the font as it's drawn, as the session hands the game's theirs.
        if (StepsBox)
        {
            Box.Font = _painter.Main.Layout;
            Box.Step();
        }
        // Once a line is complete it's drawn once more if the tick that completed it printed glyphs: the view saw it incomplete before
        // that tick and complete after, so the last glyph or two of every line went undrawn until Nov 6, 2026.
        if (Visible && (Box.Layout is { Choices.Count: > 0 } || !Box.LineComplete || Box.RevealedGlyphs != _drawnGlyphs))
            QueueRedraw();
    }

    public override void _Draw()
    {
        if (Box.Layout is not { } layout)
            return;
        _images.DrawPanel(this, layout.Panel);
        if (_border is not null)
            DrawTexture(_border, new Vector2(layout.BorderX, layout.BorderY));
        if (_images.Get(PortraitOf(layout.Speaker)) is { } portrait)
            DrawTexture(portrait, new Vector2(layout.PortraitX, layout.PortraitY));

        // A line still printing shows its first glyphs, and its choices wait for the rest (TextReveal).
        if (layout.Line is { } line)
            Print(line, DialogueBox.LineColor, Box.RevealedGlyphs);
        _drawnGlyphs = Box.RevealedGlyphs;
        if (!Box.LineComplete)
            return;
        for (var i = 0; i < layout.Choices.Count; i++)
            Print(layout.Choices[i], i == layout.Highlight ? Box.HighlightNow : DialogueBox.ChoiceColor);
    }

    /// <summary>How many of the line's glyphs the last draw printed, so the draw after the one that completes a line still comes.</summary>
    private int _drawnGlyphs;

    private void Print(TextBlock block, Rgba color, int? glyphs = null)
    {
        // The core lays the line out at the face's own size; the glyphs are drawn at the Large size, shrunk toward the block's corner
        // and moved down the font's drop, as UiPainter.Text draws text smaller (Nov 6, 2026), so the lines end a little short of the box.
        var font = _painter.Main;
        var (scale, drop) = (font.Scale, font.Drop);
        foreach (var glyph in font.Layout.Print(block.Text, block.X, block.Y, block.Bounds, color).Glyphs.Take(glyphs ?? int.MaxValue))
        {
            var rect = new Rect2(block.X + (glyph.X - block.X) * scale, block.Y + drop + (glyph.Y - block.Y) * scale, glyph.Width * scale, glyph.Height * scale);
            UiPainter.Glyph(this, font, glyph, rect, null, UiPainter.Color8(glyph.Color));
        }
    }

    /// <summary>A speaker's portrait, or the code's fallback for one without.</summary>
    private RecordId PortraitOf(RecordId? speaker) =>
        speaker is { } id && _plugins.TryGet<DbRecord>(id)?.Portrait is { } portrait ? portrait : FallbackPortrait;
}
