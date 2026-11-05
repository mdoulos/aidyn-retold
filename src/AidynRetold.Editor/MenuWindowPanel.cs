using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A preview of one of the Party tab's windows (<see cref="IPartyWindow"/>) in the runtime's view, against the editor's
/// game: the tab itself, the loot window and shops, and the training window. It holds what they share: the UI screen
/// and the options for how it is shown, a bar of buttons that press the window's controls and redraw the view, the Details
/// checkbox, the buttons <c>--press</c> names, and the line saying the window has closed. A panel makes the window, adds
/// its buttons and calls <see cref="Finish"/>.
/// </summary>
public abstract partial class MenuWindowPanel : VBoxContainer
{
    private readonly UiScreen _screen = new()
    {
        CustomMinimumSize = new Vector2(UiScreen.Width, UiScreen.Height), Backdrop = new Color(0.22f, 0.25f, 0.3f), ForwardsMouse = true,
    };
    private readonly HFlowContainer _bar = new();
    private readonly Label _state = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Dictionary<string, Action> _presses = [];
    private readonly bool _bare;
    private UiScreenOptions? _options;

    /// <param name="bare">
    /// The screen alone, as wide as the panel, with no line over it, no bar of buttons and no options: the mouse and the keys work it,
    /// as the game's do. The buttons and the Details box are still made, for <c>--press</c>, but never shown.
    /// </param>
    protected MenuWindowPanel(PluginSet plugins, IPartyWindow window, bool bare = false)
    {
        _bare = bare;
        AddThemeConstantOverride("separation", 6);
        View = new PartyTabView(plugins, window);
        _screen.Canvas.AddChild(View);
        // A click on a row the view puts the cursor on is A, a click on a portrait C-left, as the game takes them.
        _screen.Clicked += Press;
        _screen.FocusMode = FocusModeEnum.All;
        _screen.GuiInput += KeyInput;
    }

    /// <summary>
    /// The keys work the window as a controller does, once the screen is clicked: the arrows are the stick, Enter and Space A,
    /// Escape and Backspace B, Page Up and Page Down C-left and C-right, and Tab C-down while it's held.
    /// </summary>
    private void KeyInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true })
            _screen.GrabFocus();
        if (@event is not InputEventKey key)
            return;
        var action = key.Keycode switch
        {
            Key.Up => MenuAction.Up,
            Key.Down => MenuAction.Down,
            Key.Left => MenuAction.Left,
            Key.Right => MenuAction.Right,
            Key.Enter or Key.KpEnter or Key.Space => MenuAction.Accept,
            Key.Escape or Key.Backspace => MenuAction.Back,
            Key.Pageup => MenuAction.MemberLeft,
            Key.Pagedown => MenuAction.MemberRight,
            Key.Tab => key.Pressed ? MenuAction.Details : MenuAction.DetailsRelease,
            _ => MenuAction.None,
        };
        if (action == MenuAction.None || (!key.Pressed && action != MenuAction.DetailsRelease) || (key.Echo && action is MenuAction.Accept or MenuAction.Back or MenuAction.Details))
            return;
        if (action == MenuAction.Back)
            Back();
        else
            Press(action);
        if (DetailsBox is { } box && action is MenuAction.Details or MenuAction.DetailsRelease)
            box.SetPressedNoSignal(View.DetailsOpen);
        _screen.AcceptEvent();
    }

    /// <summary>B: what the window does with it, which for one that closes says so (<see cref="Leave"/>).</summary>
    protected virtual void Back() => Press(MenuAction.Back);

    /// <summary>The runtime's view of the window.</summary>
    protected PartyTabView View { get; }

    /// <summary>The Details checkbox, once <see cref="AddDetails"/> has made it.</summary>
    protected CheckBox? DetailsBox { get; private set; }

    /// <summary>Says what the window is, at the top.</summary>
    protected void Describe(string text)
    {
        if (!_bare)
            AddChild(new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart });
    }

    /// <summary>A button in the bar that does something to the window and redraws the view.</summary>
    protected Button AddButton(string text, string tooltip, Action act)
    {
        var button = new Button { Text = text, TooltipText = tooltip };
        button.Pressed += () =>
        {
            act();
            View.QueueRedraw();
        };
        _bar.AddChild(button);
        return button;
    }

    /// <summary>
    /// The checkbox that holds C-down. With <paramref name="closesMessage"/>, checking it closes a message first, as C-down
    /// does, instead of showing the details.
    /// </summary>
    /// <summary>A button on the window, as the game's pause menu sends it (<see cref="IPartyWindow.Press"/>), drawn again.</summary>
    protected void Press(MenuAction action)
    {
        View.Window.Press(action);
        View.QueueRedraw();
    }

    protected void AddDetails(string tooltip)
    {
        var box = DetailsBox = new CheckBox { Text = "Details (C-down)", TooltipText = tooltip };
        box.Toggled += on =>
        {
            // C-down closes a message first, as in the game: then the details aren't held, and the box shows it.
            Press(on ? MenuAction.Details : MenuAction.DetailsRelease);
            box.SetPressedNoSignal(View.DetailsOpen);
        };
        _bar.AddChild(box);
    }

    /// <summary>Presses C-down if it is up, or lets it go (<c>--press details</c>).</summary>
    protected void ToggleDetails() => DetailsBox!.ButtonPressed = !DetailsBox.ButtonPressed;

    /// <summary>The names <c>--press</c> takes, in the order the message lists them, and what each does.</summary>
    protected void Presses(params (string Name, Action Act)[] presses)
    {
        foreach (var (name, act) in presses)
            _presses[name] = act;
    }

    /// <summary>Lays out the bar, the screen's options and the screen, and the line for when the window closes if there is one.</summary>
    /// <param name="shape">What the window's shape changes, for the options' tooltip.</param>
    protected void Finish(string shape, bool closes)
    {
        if (_bare)
        {
            // The screen as wide as the panel, in the original's shape, its pixels sharp.
            _screen.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _screen.Filter = UiFilter.Pixels;
            _screen.MouseDefaultCursorShape = CursorShape.PointingHand;
            AddChild(_screen);
            Resized += FitScreen;
            FitScreen();
        }
        else
        {
            _bar.AddThemeConstantOverride("h_separation", 6);
            AddChild(_bar);
            AddChild(_options = new UiScreenOptions(_screen, shape));
            AddChild(_screen);
        }
        if (closes)
            AddChild(_state);
    }

    /// <summary>The bare screen's height from its width: 4:3, as the original's.</summary>
    private void FitScreen()
    {
        var height = MathF.Round(Math.Max(Size.X, UiScreen.Width) * 3 / 4f);
        if (Math.Abs(_screen.CustomMinimumSize.Y - height) > 0.5f)
            _screen.CustomMinimumSize = new Vector2(0, height);
    }

    /// <summary>
    /// The original's window is gone once it closes; here it stays, to look at, with nothing more to do: says so, and
    /// disables the buttons.
    /// </summary>
    protected void Leave(string text)
    {
        _state.Text = text;
        foreach (var button in _bar.GetChildren().OfType<BaseButton>())
            button.Disabled = true;
    }

    /// <summary>Sets how the screen is shown (<c>--zoom</c>, <c>--ui-filter</c>, <c>--aspect</c>, <c>--ui-lines</c>).</summary>
    public void SetScreen(int? zoom, UiFilter? filter, bool? wide, int? lines) => _options?.Set(zoom, filter, wide, lines);

    /// <summary>Presses the controls <see cref="Presses"/> names, in turn (<c>--press</c>), for screenshots.</summary>
    public void Press(IEnumerable<string> buttons)
    {
        foreach (var button in buttons)
        {
            if (_presses.TryGetValue(button.Trim().ToLowerInvariant(), out var act))
                act();
            else
                GD.PrintErr($"--press: \"{button}\" isn't {Names()}.");
        }
        View.QueueRedraw();
    }

    // "a, b, up or down".
    private string Names()
    {
        var names = _presses.Keys.ToList();
        return names.Count < 2 ? string.Concat(names) : $"{string.Join(", ", names[..^1])} or {names[^1]}";
    }
}
