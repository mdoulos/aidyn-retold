using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Game;

/// <summary>
/// The parchment behind the pause menu's tabs, between the scroll's rollers, the user's choice (Oct 31, 2026): each tab's
/// plain parchment, as its veil is, cut only at the rollers' middles and the rows their ends reach. The tabs over it are cut
/// to the original's clip as well, which lags the rollers as they part and leads them as they close, so without it the world
/// showed between the rollers as the scroll began to open and finished closing. With it there's parchment between them
/// whenever they stand across the screen, widening as they part, and each tab's painted layout and widgets come in over it
/// as the original's clip lets them. As the Party scroll comes up from below, its rollers seen end on, it fades in as they
/// stand up, from <see cref="UprightFrom"/> to <see cref="UprightTo"/> (<see cref="PauseScrollView.Arrange"/>).
/// </summary>
public partial class ScrollParchmentView : Control
{
    /// <summary>
    /// How upright the rollers stand when the parchment begins to fade in, and when it's in: frames 21 and 28 of the Party
    /// opening, the second as they come to stand across the screen.
    /// </summary>
    public const float UprightFrom = 0.35f, UprightTo = 0.7f;

    private readonly UiPainter _ui;

    public ScrollParchmentView(PluginSet plugins)
    {
        _ui = new UiPainter(plugins);
        Name = "Parchment";
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The tabs drawn, each with where its control's corner is on this one, as the tabs' own controls are placed.</summary>
    public IReadOnlyList<(PauseTab Tab, Vector2 Corner)> Tabs { get; set; } = [];

    /// <summary>The size of the tabs' controls, the screen's, which centres their frames.</summary>
    public Vector2I Screen { get; set; }

    public override void _Draw()
    {
        // Each frame centred on its tab's control, as the tabs centre theirs.
        var frame = new Vector2I((Screen.X - UiScreen.Width) / 2, (Screen.Y - UiScreen.Height) / 2);
        foreach (var (tab, corner) in Tabs)
        {
            DrawSetTransform(corner + frame);
            _ui.Tab(this, tab, Patches(tab), plain: true);
        }
        DrawSetTransform(Vector2.Zero);
    }

    private static IReadOnlyList<PlainImage.Patch> Patches(PauseTab tab) => tab switch
    {
        PauseTab.Party => PartyTabView.PlainPatches,
        PauseTab.World => WorldTabView.PlainPatches,
        _ => [],
    };
}
