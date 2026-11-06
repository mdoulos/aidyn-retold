using Godot;

namespace AidynRetold.Game;

/// <summary>
/// The mouse's buttons over the world in trek and a battle, as World of Warcraft's (ADR 0036), with the mouse on Camera
/// (<see cref="CameraRig.MouseLook"/>): the cursor stays free to click on things, and the camera turns while the mouse is dragged
/// with the left or right button held.
/// </summary>
/// <remarks>
/// A press is held back until it's let go. If the mouse moved less than <see cref="DragDistance"/> meanwhile, it was a click: the
/// press goes on to what reads presses as it would have (<see cref="_UnhandledInput"/>), so a battle's left click still attacks and
/// its right click still cancels, a moment later. Past that distance it's a drag: the cursor is hidden and captured, and the mouse
/// turns the camera (<see cref="CameraRig.Dragging"/>) until both buttons are up, when the cursor comes back where the press was.
/// Both buttons held at once run him the way the camera looks (<see cref="MouseRuns"/>), and are never a click.
/// </remarks>
public partial class Game
{
    /// <summary>How far, in pixels, the mouse moves with a button held before the press is a drag rather than a click.</summary>
    private const float DragDistance = 4;

    /// <summary>The first button pressed over the world, held back until the buttons are let go; null while none is.</summary>
    private InputEventMouseButton? _mouseHeld;

    /// <summary>How far the mouse has moved since <see cref="_mouseHeld"/> went down.</summary>
    private float _mouseTravel;

    /// <summary>Whether the other button went down too while <see cref="_mouseHeld"/> was held, which is never a click.</summary>
    private bool _mouseBoth;

    /// <summary>Whether both buttons are held, which runs him the way the camera looks.</summary>
    private bool MouseRuns => _camera is { MouseLook: true } && _mouseHeld is not null
        && Input.IsMouseButtonPressed(MouseButton.Left) && Input.IsMouseButtonPressed(MouseButton.Right);

    public override void _Input(InputEvent e)
    {
        NoteDevice(e);
        if (_camera is not { MouseLook: true } camera)
            return;
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Right } button:
                GetViewport().SetInputAsHandled();
                MouseDrag(camera, button);
                break;
            case InputEventMouseMotion motion when _mouseHeld is not null && !camera.Dragging:
                _mouseTravel += motion.Relative.Length();
                if (_mouseTravel >= DragDistance)
                {
                    camera.Dragging = true;
                    Input.MouseMode = Input.MouseModeEnum.Captured;
                }
                break;
        }
    }

    /// <summary>
    /// Whether the player is using a controller (<see cref="Core.Ui.IPauseMenuHost.Controller"/>): their last input came from one, a
    /// button or a stick pushed past half way, rather than a key, a mouse button or the mouse moved. A controller plugged in counts
    /// until anything is pressed.
    /// </summary>
    private bool _controller = Input.GetConnectedJoypads().Count > 0;

    private void NoteDevice(InputEvent e)
    {
        bool? controller = e switch
        {
            InputEventJoypadButton { Pressed: true } => true,
            InputEventJoypadMotion motion when Math.Abs(motion.AxisValue) > 0.5f => true,
            InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true } => false,
            InputEventMouseMotion motion when motion.Relative.LengthSquared() > 4 => false,
            _ => null,
        };
        if (controller is { } c)
            _controller = c;
        // An action pressed for the press harness counts as a key's. A key counts only if it's one of the situation's controls, the
        // user's choice (Nov 6, 2026): Ctrl alone, or any key the menu has no use for, leaves the mouse in use and its hover standing.
        bool? pointing = e switch
        {
            InputEventMouseButton { Pressed: true } => true,
            InputEventMouseMotion motion when motion.Relative.LengthSquared() > 4 => true,
            InputEventKey { Pressed: true } key => _stick.Translate(key) != Core.Ui.MenuAction.None ? false : null,
            InputEventJoypadButton { Pressed: true } or InputEventAction { Pressed: true } => false,
            InputEventJoypadMotion motion when Math.Abs(motion.AxisValue) > 0.5f => false,
            _ => null,
        };
        if (pointing is { } p)
            _pointing = p;
    }

    /// <summary>Whether the player is using the mouse (<see cref="Core.Ui.IPauseMenuHost.Pointing"/>): their last input was the mouse.</summary>
    private bool _pointing;

    bool Core.Ui.IPauseMenuHost.Controller => _controller;

    bool Core.Ui.IPauseMenuHost.Pointing => _pointing;

    /// <summary>A button down or up over the world: held back, ending a drag, or a click let through.</summary>
    private void MouseDrag(CameraRig camera, InputEventMouseButton button)
    {
        if (button.Pressed)
        {
            if (_mouseHeld is null)
                (_mouseHeld, _mouseTravel, _mouseBoth) = (button, 0, false);
            else
                _mouseBoth = true;
            return;
        }
        // Godot has let go of the button by now, so this asks after the other.
        if (Input.IsMouseButtonPressed(MouseButton.Left) || Input.IsMouseButtonPressed(MouseButton.Right))
            return;
        var click = _mouseHeld is { } held && !camera.Dragging && !_mouseBoth && held.ButtonIndex == button.ButtonIndex ? held : null;
        EndMouseDrag(restore: true);
        _mouseHeld = null;
        if (click is null)
            return;
        _UnhandledInput(click);
        _UnhandledInput(button);
    }

    /// <summary>Ends a drag: the cursor is shown again, where the press was if <paramref name="restore"/>.</summary>
    private void EndMouseDrag(bool restore)
    {
        if (_camera is not { Dragging: true } camera)
            return;
        camera.Dragging = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (restore && _mouseHeld is { } held)
            GetViewport().WarpMouse(held.Position);
    }
}
