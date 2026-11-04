using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// What the browser shows while the plugins load, which takes a few seconds (editor.md#plugins): a card at the page's centre saying
/// what's loading, with a bar that moves while it does.
/// </summary>
public partial class LoadingPage : CenterContainer
{
    public LoadingPage()
    {
        var card = new PanelContainer { ThemeTypeVariation = "Toast", CustomMinimumSize = new Vector2(380, 0) };
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(new Label { Text = "Loading the records", ThemeTypeVariation = "HeadingLabel" });
        column.AddChild(new Label
        {
            Text = "The original game, the ACRE plugin and the mods, every record and asset in them. It takes a few seconds.",
            ThemeTypeVariation = "DimLabel", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(348, 0),
        });
        column.AddChild(new ProgressBar { Indeterminate = true, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 6) });
        card.AddChild(column);
        AddChild(card);
    }
}
