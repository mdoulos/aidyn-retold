using AidynRetold.Core.Ui;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A script page's conversation box as the game draws it, under the page's graph: the UI screen as wide as the page's column, cut to
/// the box from its feathering's top to the screen's foot, with none of the screen above it. The mouse works it as it does the game's
/// (the runtime's <c>Game.ConversationMouse</c>): over a choice it points at it (<see cref="DialogueBox.PointAt"/>), and a click is
/// A (<see cref="Pressed"/>), which takes the choice pointed at or moves the script on. The keys work it as a controller does: once
/// it's clicked, Up and Down move between the choices, and Enter or Space is A.
/// </summary>
public partial class DialoguePreview : Control
{
    private readonly UiScreen _screen;
    private readonly ConversationBoxView _box;
    private readonly Func<bool> _wide;
    private readonly Label _empty = new()
    {
        Text = "Play runs the script; its conversation box shows here, and a click on it is A, as in the game.",
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ThemeTypeVariation = "DimLabel",
        MouseFilter = MouseFilterEnum.Ignore,
    };
    // The highest the box has reached since it was last cleared, in the screen's pixels up from its foot, where the box stands
    // whatever the screen's lines, so the strip grows with a taller page and doesn't jump back with each shorter one.
    private int? _height;

    /// <param name="wide">Whether the window's shape is 16:9, which widens the screen and leaves the box at its centre.</param>
    public DialoguePreview(UiScreen screen, ConversationBoxView box, Func<bool> wide)
    {
        (_screen, _box, _wide) = (screen, box, wide);
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        // Sized here, not by a container, so it can stand taller than the strip that shows its foot.
        screen.CustomMinimumSize = Vector2.Zero;
        screen.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(screen);
        _empty.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_empty);
    }

    /// <summary>A click on the box: A, after pointing at the choice under it.</summary>
    public event Action? Pressed;

    /// <summary>Forgets how high the box reached, as a new play starts.</summary>
    public void Clear() => _height = null;

    /// <summary>What the strip says while no box shows: how to start, that the script waits on the side panel, or that it's ended.</summary>
    public string Hint
    {
        set => _empty.Text = value;
    }

    public override void _Process(double delta)
    {
        if (Size.X <= 0)
            return;
        // The screen as wide as the strip, in the window's shape.
        var width = Size.X;
        var height = MathF.Round(width * (_wide() ? 9 / 16f : 3 / 4f));
        var canvas = (Vector2)_screen.CanvasSize;
        var frameTop = (int)canvas.Y - DialogueBox.FrameHeight;
        // The box's top, its feathering's, or where a page with a line or two would put it while none shows.
        var top = _box.Visible && _box.Box.Layout is { } layout ? layout.Panel.Band.Top - layout.Panel.Top : frameTop + DialogueBox.BaseTop - DialogueBox.GradientHeight;
        _height = Math.Max(_height ?? 0, (int)canvas.Y - top);
        var scale = height / canvas.Y;
        _screen.Size = new Vector2(width, height);
        _screen.Position = new Vector2(0, -MathF.Round((canvas.Y - _height.Value) * scale));
        var strip = MathF.Round(_height.Value * scale);
        if (Math.Abs(CustomMinimumSize.Y - strip) > 0.5f)
            CustomMinimumSize = new Vector2(0, strip);
        _empty.Visible = !_box.Visible;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key && _box.Visible)
        {
            switch (key.Keycode)
            {
                case Key.Up or Key.Down:
                    _box.Box.MoveCursor(key.Keycode == Key.Up ? -1 : 1);
                    _box.QueueRedraw();
                    AcceptEvent();
                    return;
                case Key.Enter or Key.KpEnter or Key.Space when !key.Echo:
                    Pressed?.Invoke();
                    AcceptEvent();
                    return;
            }
        }
        if (@event is not InputEventMouse mouse || !_box.Visible || _screen.ToCanvas(mouse.Position - _screen.Position) is not { } at)
            return;
        // Over a choice, the cursor goes to it; a click is A.
        if (_box.Box.PointAt((int)at.X, (int)at.Y))
            _box.QueueRedraw();
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Pressed?.Invoke();
            AcceptEvent();
        }
    }
}
