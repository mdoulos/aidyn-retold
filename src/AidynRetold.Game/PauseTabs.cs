using AidynRetold.Core.Ui;
using Godot;

namespace AidynRetold.Game;

// The tabs' interface is in AidynRetold.Views, beside the Party tab, which the editor shows too.

public partial class SystemTabView : IPauseTab<SystemMenu>
{
    SystemMenu IPauseTab<SystemMenu>.Model { set => Menu = value; }

    void IPauseTab.Activate(bool showing)
    {
        ProcessMode = showing ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        MouseFilter = showing ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
    }
}

/// <remarks>
/// The World tab steps from the menu's own frame (<see cref="WorldMenu.Step"/>), so showing only lets the mouse reach its message's
/// Close button.
/// </remarks>
public partial class WorldTabView : IPauseTab<WorldMenu>
{
    WorldMenu IPauseTab<WorldMenu>.Model { set => Menu = value; }

    void IPauseTab.Activate(bool showing) => MouseFilter = showing ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;

    /// <summary>The message's Close button closes it, as A does; the rest of the tab is the stick's.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click
            && _ui.OnMessageClose(new Vector2I((int)Size.X, (int)Size.Y), Menu.Overlay, click.Position))
        {
            UiScreen.Of(this)?.Click(MenuAction.Accept);
            AcceptEvent();
        }
    }
}
