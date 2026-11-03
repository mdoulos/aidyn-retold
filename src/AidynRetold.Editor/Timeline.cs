using System.Globalization;
using Godot;

namespace AidynRetold.Editor;

/// <summary>A moment in a clip the timeline marks: its tick, what kind it is and what happens.</summary>
public sealed record TimelineMark(int Tick, TimelineMarkKind Kind, string What);

/// <summary>What a mark on the timeline is, which picks its colour and row.</summary>
public enum TimelineMarkKind
{
    /// <summary>A footstep's sound plays.</summary>
    Footstep,
    /// <summary>One of the body's own sounds plays: a swing, a grunt.</summary>
    Sound,
    /// <summary>A particle emitter starts.</summary>
    Emitter,
    /// <summary>The blow lands, and the target starts to react.</summary>
    Strike,
}

/// <summary>
/// A clip's ticks across the view's width, under it: the playhead, a tick mark every ten and a number every fifty, and the clip's
/// marks (<see cref="TimelineMark"/>) over the track, coloured by kind, each with its tooltip. Clicking or dragging on it scrubs,
/// as the model view's slider did.
/// </summary>
public partial class Timeline : Control
{
    private static readonly Color Track = new(0.2f, 0.21f, 0.25f), Ticks = new(0.45f, 0.47f, 0.53f), Head = new(1f, 0.9f, 0.4f);

    private IReadOnlyList<TimelineMark> _marks = [];
    private int _length = 1, _tick;
    private bool _dragging;

    public Timeline()
    {
        CustomMinimumSize = new Vector2(0, 44);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = " ";
    }

    /// <summary>The playhead was dragged or clicked to a tick.</summary>
    public event Action<int>? Scrubbed;

    /// <summary>The clip's length in ticks.</summary>
    public int Length
    {
        get => _length;
        set
        {
            _length = Math.Max(value, 1);
            QueueRedraw();
        }
    }

    /// <summary>Where the playhead is, from 0.</summary>
    public int Tick
    {
        get => _tick;
        set
        {
            if (_tick == value)
                return;
            _tick = value;
            QueueRedraw();
        }
    }

    /// <summary>Whether clicking scrubs: not while an actor is driven, as its state machine sets the clock.</summary>
    public bool Editable { get; set; } = true;

    public IReadOnlyList<TimelineMark> Marks
    {
        get => _marks;
        set
        {
            _marks = value;
            QueueRedraw();
        }
    }

    private const float Margin = 10, TrackTop = 22, TrackHeight = 10;

    private float X(int tick) => Margin + (Size.X - 2 * Margin) * tick / _length;

    private int TickAt(float x) => Math.Clamp((int)Math.Round((x - Margin) / Math.Max(Size.X - 2 * Margin, 1) * _length), 0, _length);

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        DrawRect(new Rect2(Margin, TrackTop, Size.X - 2 * Margin, TrackHeight), Track);
        for (var t = 0; t <= _length; t += 10)
        {
            var x = X(t);
            DrawLine(new Vector2(x, TrackTop + TrackHeight), new Vector2(x, TrackTop + TrackHeight + (t % 50 == 0 ? 6 : 3)), Ticks);
            if (t % 50 == 0)
                DrawString(font, new Vector2(x + 2, Size.Y - 1), t.ToString(CultureInfo.InvariantCulture), fontSize: 11, modulate: Ticks);
        }
        // The length at the end, for a clip too short to reach the first fifty.
        var end = _length.ToString(CultureInfo.InvariantCulture);
        var endWidth = font.GetStringSize(end, fontSize: 11).X;
        if (_length % 50 != 0)
            DrawString(font, new Vector2(Size.X - Margin - endWidth, Size.Y - 1), end, fontSize: 11, modulate: Ticks);
        foreach (var mark in _marks)
        {
            var x = X(mark.Tick);
            var color = ColorOf(mark.Kind);
            var top = 2 + (int)mark.Kind % 2 * 9;
            DrawColoredPolygon([new Vector2(x - 5, top), new Vector2(x + 5, top), new Vector2(x, top + 9)], color);
            DrawLine(new Vector2(x, top + 9), new Vector2(x, TrackTop + TrackHeight), color with { A = 0.6f });
        }
        var head = X(_tick);
        DrawLine(new Vector2(head, 0), new Vector2(head, TrackTop + TrackHeight + 2), Head, 2);
    }

    public static Color ColorOf(TimelineMarkKind kind) => kind switch
    {
        TimelineMarkKind.Footstep => new Color(0.45f, 0.75f, 1f),
        TimelineMarkKind.Sound => new Color(0.7f, 0.6f, 1f),
        TimelineMarkKind.Emitter => EditorStyle.Changed,
        _ => EditorStyle.Problem,
    };

    public override string _GetTooltip(Vector2 atPosition)
    {
        var near = _marks.Where(m => Math.Abs(X(m.Tick) - atPosition.X) < 7).ToList();
        return near.Count > 0
            ? string.Join("\n", near.Select(m => $"tick {m.Tick}: {m.What}"))
            : $"tick {TickAt(atPosition.X)} of {_length}";
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed && Editable;
                if (_dragging)
                    Scrub(button.Position.X);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging:
                Scrub(motion.Position.X);
                AcceptEvent();
                break;
        }
    }

    private void Scrub(float x)
    {
        Tick = TickAt(x);
        Scrubbed?.Invoke(_tick);
    }
}
