using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A short message at the foot of the window, such as what a save did, with Undo where it can be undone: it stays three seconds,
/// fades, and gives way to the next. It's a control of the editor's window, over the page, and takes no clicks but its own.
/// </summary>
public partial class Toast : Control
{
    private static Toast? _shown;
    private readonly PanelContainer _panel = new() { ThemeTypeVariation = "Toast", SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    private readonly double _seconds;
    private bool _closing;

    private Toast(string text, Action? undo, double seconds)
    {
        _seconds = seconds;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        column.OffsetBottom = -28;
        AddChild(column);
        var row = EditorStyle.Row(14);
        row.AddChild(new Label { Text = text, VerticalAlignment = VerticalAlignment.Center });
        if (undo is not null)
        {
            var button = new Button { Text = "Undo", ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None };
            button.AddThemeColorOverride("font_color", EditorTheme.Current.Accent);
            button.AddThemeColorOverride("font_hover_color", EditorTheme.Current.Accent);
            button.Pressed += () =>
            {
                Close(now: true);
                undo();
            };
            row.AddChild(button);
        }
        _panel.AddChild(row);
        column.AddChild(_panel);
    }

    /// <summary>Shows <paramref name="text"/> over <paramref name="near"/>'s window, in place of a toast still showing.</summary>
    public static void Show(Node near, string text, Action? undo = null, double seconds = 3)
    {
        if (_shown is not null && IsInstanceValid(_shown))
            _shown.Close(now: true);
        _shown = new Toast(text, undo, seconds);
        near.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _shown);
    }

    public override void _Ready()
    {
        _panel.Modulate = Colors.Transparent;
        CreateTween().TweenProperty(_panel, "modulate", Colors.White, 0.15);
        GetTree().CreateTimer(_seconds).Timeout += () => Close(now: false);
    }

    private void Close(bool now)
    {
        if (_closing || !IsInstanceValid(this))
            return;
        _closing = true;
        if (now || !IsInsideTree())
        {
            QueueFree();
            return;
        }
        var fade = CreateTween();
        fade.TweenProperty(_panel, "modulate", Colors.Transparent, 0.25);
        fade.Finished += QueueFree;
    }
}
