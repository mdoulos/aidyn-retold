using Godot;

namespace AidynRetold.Editor;

/// <summary>A dialog button's part: the way out, the way forward, or a way forward that takes something away.</summary>
public enum DialogButtonKind
{
    Cancel,
    /// <summary>Another way forward, outlined as the way out is, which Escape and the backdrop don't take.</summary>
    Secondary,
    Primary,
    Danger,
}

/// <summary>A dialog's button: its label, its part, what it does, and whether the dialog closes after.</summary>
public sealed record DialogButton(string Label, DialogButtonKind Kind, Action? Pressed = null, bool Closes = true);

/// <summary>
/// The editor's dialog, over everything: a dimmed backdrop, and a box of a fixed width, its title in a band of its own with a line
/// under it, its message in paragraphs, and its buttons at its foot's right, the way out first and the way forward last, filled,
/// red where it takes something away. Focus starts on the way out; the cross at the title's right, Escape and a click on the
/// backdrop take it, where the dialog allows. It's a control of the editor's window, so it takes the editor's theme, dark or light.
/// </summary>
public partial class EditorDialog : Control
{
    private readonly VBoxContainer _body = new();
    private readonly List<Button> _buttons = [];
    private readonly bool _dismissable;
    private Action? _cancel;
    private Control? _focus;

    /// <param name="dismissable">Whether the cross, Escape and the backdrop cancel: not for a dialog the editor can't go on without.</param>
    /// <param name="under">What stands under the buttons, at the foot's right: a quieter way on, such as Just looking around.</param>
    public EditorDialog(string title, IEnumerable<Control> body, IReadOnlyList<DialogButton> buttons, float width = 460, bool dismissable = true,
        Control? under = null)
    {
        _dismissable = dismissable;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var backdrop = new ColorRect { Color = EditorTheme.Current.Backdrop, MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true } && _dismissable)
                Cancel();
        };
        AddChild(backdrop);

        var centre = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        centre.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(centre);
        var box = new PanelContainer { ThemeTypeVariation = "DialogBox", CustomMinimumSize = new Vector2(width, 0) };
        centre.AddChild(box);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        box.AddChild(column);

        var band = new PanelContainer { ThemeTypeVariation = "DialogTitle" };
        var titleRow = EditorStyle.Row(8);
        titleRow.AddChild(new Label { Text = title, ThemeTypeVariation = "DialogTitleLabel", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (dismissable)
        {
            var close = new Button
            {
                Icon = EditorTheme.CloseIcon(EditorTheme.Current.Dim), ThemeTypeVariation = "FlatButton", FocusMode = FocusModeEnum.None,
                TooltipText = "Close (Escape)", SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            close.Pressed += Cancel;
            titleRow.AddChild(close);
        }
        band.AddChild(titleRow);
        column.AddChild(band);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _body.AddThemeConstantOverride("separation", 10);
        foreach (var control in body)
            _body.AddChild(control);
        margin.AddChild(_body);
        column.AddChild(margin);

        var foot = new MarginContainer();
        foot.AddThemeConstantOverride("margin_left", 14);
        foot.AddThemeConstantOverride("margin_right", 14);
        foot.AddThemeConstantOverride("margin_top", 10);
        foot.AddThemeConstantOverride("margin_bottom", 14);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        actions.AddThemeConstantOverride("separation", 8);
        foreach (var spec in buttons)
        {
            var button = new Button
            {
                Text = spec.Label, CustomMinimumSize = new Vector2(84, 30),
                ThemeTypeVariation = spec.Kind switch { DialogButtonKind.Primary => "PrimaryButton", DialogButtonKind.Danger => "DangerButton", _ => "" },
            };
            button.Pressed += () =>
            {
                spec.Pressed?.Invoke();
                if (spec.Closes)
                    Close();
            };
            if (spec.Kind == DialogButtonKind.Cancel && _cancel is null)
            {
                _cancel = () =>
                {
                    spec.Pressed?.Invoke();
                    if (spec.Closes)
                        Close();
                };
                _focus ??= button;
            }
            actions.AddChild(button);
            _buttons.Add(button);
        }
        if (under is null)
        {
            foot.AddChild(actions);
        }
        else
        {
            var stack = new VBoxContainer();
            stack.AddThemeConstantOverride("separation", 8);
            stack.AddChild(actions);
            under.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            stack.AddChild(under);
            foot.AddChild(stack);
        }
        column.AddChild(foot);
    }

    /// <summary>The buttons, in order, for a dialog that changes them as it goes, as the import does.</summary>
    public IReadOnlyList<Button> Buttons => _buttons;

    /// <summary>What has the focus when the dialog opens: its way out, unless something in its body asks for it.</summary>
    public Control? FocusOnOpen
    {
        get => _focus;
        set => _focus = value;
    }

    /// <summary>Shows the dialog over everything in <paramref name="near"/>'s window.</summary>
    public EditorDialog Open(Node near)
    {
        // Deferred, as the window may be setting up its children, as it is when the editor opens on the import.
        near.GetTree().Root.CallDeferred(Node.MethodName.AddChild, this);
        return this;
    }

    public override void _Ready() => (_focus ?? _buttons.LastOrDefault())?.CallDeferred(Control.MethodName.GrabFocus);

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (_dismissable)
                Cancel();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The dialog was closed without a button: by its cross, Escape or the backdrop, where it has no Cancel to press.</summary>
    public event Action? Dismissed;

    private void Cancel()
    {
        if (_cancel is not null)
        {
            _cancel();
            return;
        }
        Dismissed?.Invoke();
        Close();
    }

    public void Close() => QueueFree();

    /// <summary>A message's paragraphs, split at blank lines, wrapping at the box's width.</summary>
    public static IEnumerable<Control> Paragraphs(string message, float width = 432) =>
        message.Split("\n\n").Select(p => (Control)new Label { Text = p, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(width, 0) });

    /// <summary>A question with a way forward and a way out: <paramref name="onConfirm"/> runs on the way forward.</summary>
    public static EditorDialog Confirm(Node near, string title, string message, string confirm, Action onConfirm, bool danger = false,
        Control? extra = null, float width = 460) =>
        new EditorDialog(title, extra is null ? Paragraphs(message, width - 28) : [.. Paragraphs(message, width - 28), extra],
            [new DialogButton("Cancel", DialogButtonKind.Cancel), new DialogButton(confirm, danger ? DialogButtonKind.Danger : DialogButtonKind.Primary, onConfirm)],
            width).Open(near);

    /// <summary>A message, with OK.</summary>
    public static EditorDialog Tell(Node near, string title, string message) =>
        new EditorDialog(title, Paragraphs(message), [new DialogButton("OK", DialogButtonKind.Primary)]).Open(near);

    /// <summary>
    /// A question with a line to answer in: <paramref name="onConfirm"/> gets the answer on the way forward, or on Enter in the line,
    /// which has the focus.
    /// </summary>
    public static EditorDialog Ask(Node near, string title, string message, string answer, string placeholder, string confirm, Action<string> onConfirm)
    {
        var line = new LineEdit { Text = answer, PlaceholderText = placeholder, CustomMinimumSize = new Vector2(432, 0) };
        EditorDialog? dialog = null;
        dialog = new EditorDialog(title, [.. Paragraphs(message), line],
            [new DialogButton("Cancel", DialogButtonKind.Cancel), new DialogButton(confirm, DialogButtonKind.Primary, () => onConfirm(line.Text.Trim()))]);
        line.TextSubmitted += text =>
        {
            onConfirm(text.Trim());
            dialog.Close();
        };
        dialog.FocusOnOpen = line;
        return dialog.Open(near);
    }
}
