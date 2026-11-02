using AidynEnhanced.Core.Ui;
using AidynEnhanced.Plugins;
using AidynEnhanced.Plugins.Records;
using Godot;

namespace AidynEnhanced.Views;

/// <summary>
/// Draws the pause menu's Party tab (<see cref="PartyMenu"/>) on a <see cref="UiScreen"/>'s canvas, laid out as the user
/// designed it (Oct 31, 2026), in the original's 320 × 240 frame at the screen's centre, on the tab's plain parchment or a mod's
/// blank one. It draws its
/// <see cref="Window"/>: the tab itself, the menu's barter state, the loot window or a shop (see <c>PartyTabView.Barter.cs</c>),
/// or its training state (<c>PartyTabView.Training.cs</c>), each with the header and lists of the <see cref="PartyMenu"/>
/// under it. A further window is drawn by a case in <see cref="_Draw"/> and <see cref="Hover"/>, and takes its presses
/// through <see cref="IPartyWindow"/>.
/// </summary>
public partial class PartyTabView : Control
{
    /// <summary>The original's frame.</summary>
    private const int FrameWidth = 320, FrameHeight = 240;

    // The left column is the member: the portraits and name where the original has them, the level by the name, the bar under
    // the portraits with the aspect's and school's icons after it, and the silhouette's slots in the column under them. The right
    // is the lists, under the tabs and the gold.

    /// <summary>The portraits' frames, as the original paints them into <c>0x00D9</c>. DollMenuPortraits puts each portrait 2 inside its frame.</summary>
    private static readonly Rect2I PortraitPiece = new(18, 22, 134, 46);

    private static readonly Vector2I ShownPortrait = new(4, 4), NameAt = new(51, 6);
    private static readonly Vector2I[] OtherPortraits = [new(49, 17), new(78, 17), new(107, 17)];
    private const int ShownSize = 40, OtherSize = 24;

    /// <summary>The hit points bar under the portraits, the hit points on it.</summary>
    private static readonly Rect2I Bar = new(22, 67, 96, 10);

    /// <summary>The aspect's and school's icons after the bar, from the portraits' frames' corner, and how far apart they are.</summary>
    private static readonly Vector2I IconsAt = new(107, 45);

    private const int IconGap = 4;

    /// <summary>The level's right edge, under the small portraits' right edge, and the gold's, at the list's.</summary>
    private const int LevelRight = 149, GoldRight = 306;

    /// <summary>
    /// The tabs over the list, the user's art (<c>assets/ui</c>): the first one's corner, the square each one's art is drawn in, and
    /// the gap between them. A banner hangs from its rod 2 under the square's top, a closed one to 26 down it and an open one to 28.
    /// </summary>
    private static readonly Vector2I TabsAt = new(160, 14);

    private const int TabSize = 30, TabsGap = 1;

    /// <summary>
    /// A tab's icon, centred across it, this far down it and this big, with its caption under it, at most this far in from either
    /// side, inside the banner's stitching.
    /// </summary>
    private const int TabIconTop = 6, TabIconSize = 13, CaptionInset = 5;

    /// <summary>How big a tab's caption is drawn, as a share of the slim font's size.</summary>
    private const float CaptionScale = 0.42f;

    /// <summary>
    /// How strongly a tab that isn't showing draws its icon and caption, and one the window hasn't got; how strongly one the window
    /// hasn't got draws its banner; and how strongly the open banner shows over a closed one the mouse is on.
    /// </summary>
    private const float ClosedIcon = 0.7f, MissingIcon = 0.3f, MissingTab = 0.5f, HoverOpen = 0.4f;

    /// <summary>The tabs' art, closed and open, and each tab's icon, in <see cref="PartyMenu.Tabs"/>' order.</summary>
    private const string TabClosed = "ui-tab-closed.png", TabOpen = "ui-tab-open.png";

    private static readonly string[] TabIcons = ["ui-inventory.png", "ui-skills.png", "ui-spells.png"];

    /// <summary>The purse before the gold, the user's art, and how big it's drawn, its middle level with the gold's.</summary>
    private const string Purse = "ui-gold.png";

    private const int PurseSize = 16;

    /// <summary>How far down the tabs the gold's middle is, level with the banners' middles.</summary>
    private const float GoldMiddle = 15;

    /// <summary>How big the level, the gold and the hit points are drawn, as a share of the slim font's size.</summary>
    private const float SmallScale = 0.6f;

    /// <summary>
    /// The silhouette's slots' corner, centring them in the column under the bar. Their places are the original's, from its
    /// silhouette's corner (DollEquipmentMenu::SetSlotCoords); the shield's is level with the first scroll's, as its frame painted
    /// into <c>0x00D9</c> is and the user's captures show; at 45 it stood 2 above it.
    /// </summary>
    private static readonly Vector2I SilhouetteAt = new(27, 83);

    /// <summary>
    /// The figure behind the slots, the user's art (<see cref="Figures"/>): its top and how tall it's drawn, centred under the helmet's
    /// slot, which puts its head under that slot and its feet at the boots'. Its width is the art's shape.
    /// </summary>
    private const float FigureTop = 79, FigureHeight = 145;

    private static readonly Vector2I[] Slots =
    [
        new(10, 47), new(10, 66), new(10, 85), new(40, 2), new(40, 24), new(30, 43), new(50, 43), new(40, 62),
        new(40, 120), new(70, 47), new(70, 66), new(70, 85), new(90, 47), new(90, 66), new(90, 85),
    ];

    private const int SlotSize = 16;

    /// <summary>What the list and its arrows are cut to as they slide: the list's bounds across, from under the tabs to the list's foot.</summary>
    private static readonly Rect2I ListClip = new(160, 43, 146, 171);

    /// <summary>The arrow that says which tabs of the pause menu are beside this one, under the gap between the columns.</summary>
    private static readonly Vector2I SliderAt = new(130, 215);

    // WidgetItemDetail: the parchment, and its text's left and right edges and top, as the original places them; what goes over
    // the silhouette is drawn Drop lower.
    private static readonly Vector2I ClassicParchment = new(0, 58);
    private static readonly ScreenRect ClassicDetails = new(23, 70, 149, 218);
    private const int DetailsIndent = 16;

    // WidgetCrafting (CraftingMenu): the title centred on the details' parchment, its list under it. A member's row is its portrait's 25 in the original, so
    // the portraits touch and the next name overlaps the health by 2; the port leaves 3 between them, as the user asked.
    private const int MemberPortrait = 25;

    /// <summary>
    /// How far down what goes over the silhouette is drawn from where the original has it, C-down's details, the skills' popups and
    /// the barter, training and post-battle windows' own parts, so their parchment clears the bar and covers the silhouette's
    /// column, to the full-height parchment's foot.
    /// </summary>
    private const int Drop = 20;

    /// <summary>The colours of the bar's track, its edge, and the hit points on it, with their shadow.</summary>
    private static readonly Rgba BarTrack = new(68, 42, 34, 60), BarEdge = new(68, 42, 34, 150), BarText = new(250, 242, 226, 255),
        BarShadow = new(40, 20, 14, 170);

    /// <summary>
    /// What's painted into <c>0x00D9</c>, covered by plain parchment from the list's side, which the tab is drawn on, and the veil
    /// over it: the portraits' frames, the silhouette and its slots in nine pieces,
    /// the bar's frame, the labels, the two rules over the list, the knotwork in three, and "Details". Each piece's
    /// source was picked for grain like the parchment round it, and nothing painted or torn is in it or a pixel round it.
    /// </summary>
    public static readonly PlainImage.Patch[] PlainPatches =
    [
        new(new(64, 35, 153, 67), 214, 140), new(new(18, 22, 66, 70), 181, 148),
        new(new(23, 70, 59, 118), 282, 143), new(new(59, 70, 94, 118), 277, 128), new(new(94, 70, 129, 118), 171, 164),
        new(new(23, 118, 59, 166), 282, 134), new(new(59, 118, 94, 166), 248, 132), new(new(94, 118, 129, 166), 167, 160),
        new(new(23, 166, 59, 213), 169, 164), new(new(59, 166, 94, 213), 184, 122), new(new(94, 166, 129, 213), 273, 155),
        new(new(218, 23, 300, 54), 236, 155), new(new(159, 29, 219, 80), 258, 135),
        new(new(153, 83, 237, 90), 189, 139), new(new(237, 83, 320, 90), 172, 155), new(new(168, 93, 311, 101), 169, 157),
        new(new(139, 97, 161, 135), 274, 153), new(new(139, 135, 161, 174), 167, 168), new(new(139, 174, 161, 212), 275, 154),
        new(new(239, 207, 295, 221), 246, 186),
    ];

    private readonly UiPainter _ui;
    private readonly PluginSet _plugins;
    /// <summary>Each member's figure, by entity, looked up once.</summary>
    private readonly Dictionary<RecordId, Figure> _figures = [];
    /// <summary>The tab the mouse is on, or null.</summary>
    private int? _hoveredTab;
    /// <summary>Where the tab's list was last drawn, which the mouse is tested against: a list sliding in is drawn off its place.</summary>
    private ListLayout _listLayout = PartyMenu.Layout(PartyList.Inventory);
    private Vector2I _origin;
    /// <summary>What's being drawn on: the tab, or one of its layers.</summary>
    private CanvasItem _canvas;
    /// <summary>Where the frame's corner is on <see cref="_canvas"/>, which what's drawn scaled goes back to.</summary>
    private Vector2 _base;

    /// <summary>The Party tab's list, cut to <see cref="ListClip"/>, over the tab's image and header.</summary>
    private readonly Control _listClip = new() { Name = "List", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control _listLayer = new() { MouseFilter = MouseFilterEnum.Ignore };
    /// <summary>
    /// The Party tab's skill popup and, over it, C-down's details, each over the list on a layer of its own, whose
    /// modulate is its fade's alpha (<see cref="PopupFade"/>), so the alpha multiplies every colour drawn on it.
    /// </summary>
    private readonly Control _craftingLayer = new() { Name = "Crafting", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Control _detailsLayer = new() { Name = "Details", MouseFilter = MouseFilterEnum.Ignore };
    /// <summary>
    /// A window's own parts over the header, the barter, training and post-battle states' lists, parchment and lines, on a layer
    /// whose modulate is <see cref="WindowAlpha"/>, so the post-battle window fades out and the loot window in with the header
    /// staying (<c>PauseMenu.CloseFade</c>, <c>OpenFade</c>).
    /// </summary>
    private readonly Control _windowLayer = new() { Name = "Window", MouseFilter = MouseFilterEnum.Ignore };
    /// <summary>What's drawn over those: the veil and the messages.</summary>
    private readonly Control _overlay = new() { Name = "Overlay", MouseFilter = MouseFilterEnum.Ignore };

    public PartyTabView(PluginSet plugins, IPartyWindow window)
    {
        _ui = new UiPainter(plugins);
        _plugins = plugins;
        _canvas = this;
        Window = window;
        Name = "Party tab";
        MouseFilter = MouseFilterEnum.Pass;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _windowLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_windowLayer);
        _windowLayer.Draw += DrawWindow;
        _listClip.AddChild(_listLayer);
        AddChild(_listClip);
        _listLayer.Draw += DrawLists;
        foreach (var layer in new[] { _craftingLayer, _detailsLayer, _overlay })
        {
            layer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(layer);
        }
        _craftingLayer.Draw += DrawCrafting;
        _detailsLayer.Draw += DrawDetails;
        _overlay.Draw += DrawOverlay;
        // The veil's image is made now, as the menu first opens, rather than as a tab is first slid from.
        _ui.Images.Plain(MenuImages.PartyTab, PlainPatches);
        _ui.Images.WarmParchment(PauseTab.Party);
    }

    /// <summary>What the tab shows and takes the presses: the tab, the barter state or the training state, which the runtime's menu starts afresh each time it opens.</summary>
    public IPartyWindow Window
    {
        get;
        set
        {
            field = value;
            QueueRedraw();
        }
    }

    /// <summary>The header and the lists the window is built on: the tab's own state, or that of the barter or training state.</summary>
    public PartyMenu Menu => Window.Menu;

    /// <summary>Whether the window is the tab itself, whose list is drawn on its own layer, with its silhouette and skill popup.</summary>
    public bool OwnTab => ReferenceEquals(Window, Menu);

    /// <summary>
    /// Whether C-down is held: pressing it opens the details of the window, which fade out once they're open and it's let
    /// go (<see cref="DetailsPane"/>, <see cref="IPartyWindow.Press"/>).
    /// </summary>
    public bool DetailsOpen => Window.DetailsPane.Held;

    /// <summary>How opaque the window's own parts and lists are, from 0 to 1, as the post-battle windows fade (<see cref="_windowLayer"/>).</summary>
    public float WindowAlpha { get; set; } = 1;

    /// <summary>
    /// How far the widgets are faded into the parchment, from 0 to 1: the pause menu fades a tab
    /// beside the one showing, whose edge shows on a screen wider than the frame.
    /// </summary>
    public float Veil { get; set; }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
        else if (what == NotificationMouseExit)
            _hoveredTab = null;
    }

    /// <summary>
    /// Whether this view steps its window each tick: the editor's panels', which have no menu to step it; in the game the
    /// core's menu steps the window of the tab showing (<see cref="Core.Ui.PauseMenu.Step"/>).
    /// </summary>
    public bool StepsWindow { get; set; } = true;

    public override void _PhysicsProcess(double delta)
    {
        if (StepsWindow)
            Window.Step();
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouse mouse)
            _hoveredTab = TabAt(mouse.Position) is { } tab && HasTab(PartyMenu.Tabs[tab]) ? tab : null;
        // A click on a tab presses the stick toward it, so it slides and sounds as the stick does, and waits as the stick waits.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } onTab && _hoveredTab is { } clicked)
        {
            var step = PartyMenu.StepToTab(PartyMenu.TabOf(Menu.List), clicked);
            if (step != 0)
                UiScreen.Of(this)?.Click(step > 0 ? MenuAction.Right : MenuAction.Left);
            AcceptEvent();
            return;
        }
        // A click on a row chooses it, as A does; anywhere else it chooses nothing.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            if (Hover(click.Position))
            {
                QueueRedraw();
                UiScreen.Of(this)?.Click(MenuAction.Accept);
            }
            AcceptEvent();
            return;
        }
        // Hovering a row puts the cursor on it, and the wheel moves it.
        if (@event is InputEventMouseMotion motion && Hover(motion.Position))
        {
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel)
            Window.MoveCursor(wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1);
        else
            return;
        QueueRedraw();
        AcceptEvent();
    }

    /// <summary>
    /// Puts the window's cursor on the row under a point, as hovering does: the pile's or the party's list in the barter
    /// state, the trainer's in training, and the tab's list or its crafting popup's. True if there was a row, and no popup
    /// took the presses.
    /// </summary>
    private bool Hover(Vector2 point)
    {
        var at = point - (Vector2)_origin;
        // What's drawn over the silhouette is drawn lower in the tabbed layout.
        var left = at - new Vector2(0, Drop);
        switch (Window)
        {
            case BarterMenu { HasPopup: false } barter:
                if (BarterMenu.PileLayout.RowAt(barter.Pile, left.X, left.Y) is { } pile)
                    barter.Point(true, pile);
                else if (PartyMenu.Layout(PartyList.Inventory).RowAt(Menu.Inventory, at.X, at.Y) is { } party)
                    barter.Point(false, party);
                else
                    return false;
                return true;
            case TrainingMenu { HasPopup: false } training:
                if ((training.OnSpells ? TrainingMenu.Layout.RowAt(training.Spells, left.X, left.Y) : TrainingMenu.Layout.RowAt(training.Skills, left.X, left.Y)) is not { } trainer)
                    return false;
                training.Point(trainer);
                return true;
            case PartyMenu { Message: null } menu:
                return PointTabRow(menu, at);
            case PostBattleMenu { HasPopup: false } postBattle:
                return PointTabRow(postBattle.Party, at);
            default:
                return false;
        }
    }

    /// <summary>The tab whose banner is under a point on the view, or null.</summary>
    private int? TabAt(Vector2 point)
    {
        if (Menu.Shown is null)
            return null;
        var at = point - (Vector2)_origin;
        for (var i = 0; i < PartyMenu.Tabs.Count; i++)
        {
            var rect = TabRect(i);
            if (at.X >= rect.Position.X + 1 && at.X < rect.End.X - 1 && at.Y >= rect.Position.Y + 2 && at.Y < rect.End.Y - 3)
                return i;
        }
        return null;
    }

    /// <summary>Where a tab's art is drawn in the frame.</summary>
    private static Rect2 TabRect(int tab) => new(TabsAt.X + tab * (TabSize + TabsGap), TabsAt.Y, TabSize, TabSize);

    /// <summary>
    /// Puts the cursor on the tab's list row under a point in the frame, the crafting popup's while it's open, or the stats and
    /// skills page's in either section. True if there was a row.
    /// </summary>
    private bool PointTabRow(PartyMenu menu, Vector2 at)
    {
        if (menu.OnPage && menu.Crafting is null)
        {
            if (menu.PageRowAt(at.X, at.Y) is not var (section, line))
                return false;
            menu.PointPage(section, line);
            return true;
        }
        var row = menu.Crafting is { } crafting
            ? crafting.Layout.RowAt(crafting.Rows, at.X, at.Y - Drop)
            : menu.List switch
            {
                PartyList.Inventory => _listLayout.RowAt(menu.Inventory, at.X, at.Y),
                PartyList.Stats => _listLayout.RowAt(menu.Stats, at.X, at.Y),
                PartyList.Skills => _listLayout.RowAt(menu.Skills, at.X, at.Y),
                _ => _listLayout.RowAt(menu.Spells, at.X, at.Y),
            };
        if (row is not { } r)
            return false;
        menu.Point(r);
        return true;
    }

    /// <summary>
    /// The tab's image and what's on it, but for the Party tab's own list, which <see cref="DrawLists"/> draws over it,
    /// and what <see cref="DrawOverlay"/> draws over that.
    /// </summary>
    public override void _Draw()
    {
        _listLayer.QueueRedraw();
        _craftingLayer.Modulate = Faded(OwnTab ? Menu.Crafting?.Fade.Alpha ?? 0 : 0);
        _detailsLayer.Modulate = Faded(Window.DetailsPane.Alpha);
        _craftingLayer.QueueRedraw();
        _detailsLayer.QueueRedraw();
        _overlay.QueueRedraw();
        if (Menu.Shown is null)
            return;
        _origin = new Vector2I(((int)Size.X - FrameWidth) / 2, ((int)Size.Y - FrameHeight) / 2);
        _listClip.Position = _origin + ListClip.Position;
        _listClip.Size = ListClip.Size;
        _listLayer.Position = -ListClip.Position;
        _listLayer.Size = new Vector2(FrameWidth, FrameHeight);
        Begin(this, _origin);
        _windowLayer.Modulate = new Color(1, 1, 1, WindowAlpha);
        _listClip.Modulate = new Color(1, 1, 1, OwnTab ? 1 : WindowAlpha);
        _windowLayer.QueueRedraw();
        if (Window is PartyMenu)
        {
            DrawTab();
            return;
        }
        // The barter, training and post-battle states: the header here, and the window's own parts on their layer (DrawWindow).
        _ui.Tab(_canvas, PauseTab.Party, PlainPatches, plain: true);
        Portraits(PortraitPiece.Position);
        Health();
    }

    /// <summary>The barter, training or post-battle state's own parts, over the header, as their fade has them.</summary>
    private void DrawWindow()
    {
        if (Menu.Shown is null || OwnTab)
            return;
        Begin(_windowLayer, _origin);
        // Each window has its own tabs, which fade with it.
        Tabs();
        switch (Window)
        {
            case BarterMenu barter:
                DrawBarter(barter);
                break;
            case TrainingMenu training:
                DrawTraining(training);
                break;
            case PostBattleMenu postBattle:
                DrawPostBattle(postBattle);
                break;
            default:
                throw new NotSupportedException($"The Party tab has no drawing for a {Window.GetType().Name}.");
        }
    }

    /// <summary>
    /// The Party tab's list, or as the stick changes it, the list it left sliding out and the new one sliding in after it,
    /// both the same way, a list's width over <see cref="PartyMenu.ListFrames"/> frames (<c>PartyMenu.ListsSlide</c>,
    /// <c>0x80031620</c>): to the right, the new list from x 20 and the old to 300; to the left, from 300 and to 20.
    /// </summary>
    private void DrawLists()
    {
        if (Menu.Shown is null || !OwnTab && Window is not PostBattleMenu)
            return;
        // The layer is the frame's size, at its corner.
        Begin(_listLayer, Vector2.Zero);
        if (Menu.Slide is not { } slide)
        {
            List(0, _ui.Slim);
            return;
        }
        var left = PartyMenu.ListBounds.Left;
        var by = slide.Direction * PartyMenu.ListDistance;
        List(MenuTween.Value(left, left + by, Menu.ListChange) - left, _ui.Slim, which: slide.From);
        List(MenuTween.Value(left - by, left, Menu.ListChange) - left, _ui.Slim);
    }

    /// <summary>A layer's modulate for a fade's alpha.</summary>
    private static Color Faded(int alpha) => new(1, 1, 1, alpha / 255f);

    /// <summary>The Party tab's skill popup, as it fades.</summary>
    private void DrawCrafting()
    {
        if (Menu.Shown is null || !OwnTab || Menu.Crafting is not { } crafting)
            return;
        Begin(_craftingLayer, _origin + new Vector2I(0, Drop));
        Crafting(crafting);
    }

    /// <summary>
    /// C-down's details on the parchment over the silhouette, as they fade: over the barter state's pile, the trainer's
    /// list, or the tab and its skill popup (<c>WidgetItemDetail</c>).
    /// </summary>
    private void DrawDetails()
    {
        if (Menu.Shown is null || !Window.DetailsPane.Showing)
            return;
        var details = Window.Details();
        if (details is null)
            return;
        Begin(_detailsLayer, _origin + new Vector2I(0, Drop));
        _ui.Parchment(_canvas, ClassicParchment.X, ClassicParchment.Y);
        Details(details, ClassicDetails, _ui.Slim);
    }

    /// <summary>
    /// Over the popup and the details: the veil, and the messages and popups at the screen's centre. The veil is the tab's own image with its painted layout covered by plain parchment (<see cref="PlainPatches"/>),
    /// over everything on the tab, so the widgets and the layout fade into the parchment alike. Drawing the image itself
    /// left what's painted into it as strong as ever.
    /// </summary>
    private void DrawOverlay()
    {
        if (Menu.Shown is null)
            return;
        Begin(_overlay, _origin);
        if (Veil > 0)
            _ui.Tab(_canvas, PauseTab.Party, PlainPatches, new Rgba(255, 255, 255, (byte)(Veil * 255)), plain: true);
        _overlay.DrawSetTransform(Vector2.Zero);
        Popups();
    }

    /// <summary>The plain parchment, the header, the silhouette's slots in their column, the tabs and the arrow under the gap between the columns.</summary>
    private void DrawTab()
    {
        _ui.Tab(_canvas, PauseTab.Party, PlainPatches, plain: true);
        Portraits(PortraitPiece.Position);
        Health();
        Silhouette(SilhouetteAt);
        Tabs();
        // Combat's pause turns the arrow to point left only.
        _ui.Image(_canvas, Menu.Combat is not null ? MenuImages.SliderLeft : MenuImages.SliderBoth, SliderAt.X, SliderAt.Y, color: PartyMenu.TextColor);
    }

    /// <summary>
    /// The tabs over the list, banners on rods, the user's art: the inventory's satchel (a shop's Sell), the stats and skills' sword
    /// and quill, and the spells' book, each with a small caption under it. The tab showing is the open banner, lighter and hanging
    /// lower, and the others closed, their icons faded, and those the window hasn't fainter still. As the lists slide, the banner
    /// left closes as the new one opens, by the slide's eased share; the mouse on a closed one opens it a little. Beside them, at
    /// the list's right edge, the party's gold after a purse.
    /// </summary>
    private void Tabs()
    {
        var shown = PartyMenu.TabOf(Menu.List);
        var from = Menu.Slide is { } slide ? PartyMenu.TabOf(slide.From) : shown;
        var change = Menu.ListChange;
        for (var i = 0; i < PartyMenu.Tabs.Count; i++)
        {
            var rect = TabRect(i);
            var has = HasTab(PartyMenu.Tabs[i]);
            // How far it's the tab showing: 1 for it, 0 for the others, and between for the two the lists are sliding between.
            var open = i == shown ? (i == from ? 1 : change) : i == from ? 1 - change : 0;
            Art(TabClosed, rect, has ? 1 : MissingTab);
            if (Math.Max(open, i == _hoveredTab ? HoverOpen : 0) is var lift and > 0)
                Art(TabOpen, rect, lift);
            var strength = has ? Mathf.Lerp(ClosedIcon, 1, open) : MissingIcon;
            Art(TabIcons[i], new Rect2(rect.Position.X + (TabSize - TabIconSize) / 2f, rect.Position.Y + TabIconTop, TabIconSize, TabIconSize), strength);
            Caption(TabLabel(PartyMenu.Tabs[i]), rect, PartyMenu.TextColor with { A = (byte)(255 * strength) });
        }

        // The gold, after a purse.
        var gold = Num(Menu.Party.Gold);
        var goldX = GoldRight - SmallWidth(gold);
        var middle = TabsAt.Y + GoldMiddle;
        Small(gold, goldX, middle - (_ui.Slim.LineHeight - 2) * SmallScale / 2, PartyMenu.TextColor);
        Art(Purse, new Rect2(goldX - PurseSize - 1, middle - PurseSize / 2f, PurseSize, PurseSize));
    }

    /// <summary>A tab's caption, centred under its icon, squeezed across to stay <see cref="CaptionInset"/> in from its sides.</summary>
    private void Caption(string text, Rect2 tab, Rgba color)
    {
        var width = _ui.Slim.Width(text) * CaptionScale;
        var squeeze = Math.Min(1, (TabSize - 2 * CaptionInset) / width);
        var at = new Vector2(tab.Position.X + (TabSize - width * squeeze) / 2, tab.Position.Y + TabIconTop + TabIconSize);
        _canvas.DrawSetTransform(_base + at, 0, new Vector2(CaptionScale * squeeze, CaptionScale));
        _ui.Text(_canvas, _ui.Slim, text, 0, 0, color);
        _canvas.DrawSetTransform(_base);
    }

    /// <summary>A piece of the game's own UI art (<see cref="UiImages.Art"/>) stretched over a rectangle in the frame, at a strength.</summary>
    private void Art(string name, Rect2 rect, float strength = 1)
    {
        if (UiImages.Art(name) is { } texture)
            _canvas.DrawTextureRect(texture, rect, false, _ui.Tint(new Rgba(255, 255, 255, (byte)(255 * strength))));
    }

    /// <summary>
    /// A tab's caption: the stats and skills page's, Skills for its icon, as the whole name is too long to go under it; the spells';
    /// or the inventory's, which beside a shop is what the party sells.
    /// </summary>
    private string TabLabel(PartyList tab) => tab switch
    {
        PartyList.Stats => "Skills",
        PartyList.Spells => "Spells",
        _ => (Window as BarterMenu)?.PartyTitle == MenuImages.SellTitle ? "Sell" : "Inventory",
    };

    private const string LevelLabel = "Level";

    /// <summary>Whether the window has a tab's list: barter only the inventory, training and the post-battle window only the others.</summary>
    private bool HasTab(PartyList tab) => Window switch
    {
        BarterMenu => tab == PartyList.Inventory,
        TrainingMenu or PostBattleMenu => tab != PartyList.Inventory,
        _ => true,
    };

    /// <summary>Text in the slim font at <see cref="SmallScale"/> of its size, from a point in the frame, its shadow first if it has one.</summary>
    private void Small(string text, float x, float y, Rgba color, Rgba? shadow = null)
    {
        if (shadow is { } dark)
            SmallAt(text, x + 0.4f, y + 0.4f, dark);
        SmallAt(text, x, y, color);
    }

    private void SmallAt(string text, float x, float y, Rgba color)
    {
        _canvas.DrawSetTransform(_base + new Vector2(x, y), 0, new Vector2(SmallScale, SmallScale));
        _ui.Text(_canvas, _ui.Slim, text, 0, 0, color);
        _canvas.DrawSetTransform(_base);
    }

    /// <summary>Starts drawing on a canvas, the frame's corner where <paramref name="at"/> is on it.</summary>
    private void Begin(CanvasItem canvas, Vector2 at)
    {
        _canvas = canvas;
        _base = at;
        canvas.DrawSetTransform(at);
    }

    /// <summary>
    /// A name on one line from (x, y), squeezed across to end at <paramref name="right"/> if it's longer: the typeface is wider
    /// than the original's sheets, and wrapped a long spell's or item's name onto the next row, over its name.
    /// </summary>
    private void OneLine(UiFont font, string text, int x, int y, int right, Rgba color)
    {
        var width = font.Width(text);
        if (x + width <= right || width == 0)
        {
            _ui.Text(_canvas, font, text, x, y, color);
            return;
        }
        _canvas.DrawSetTransform(_base + new Vector2(x, y), 0, new Vector2((float)(right - x) / width, 1));
        _ui.Text(_canvas, font, text, 0, 0, color);
        _canvas.DrawSetTransform(_base);
    }

    /// <summary>How wide text is in the slim font at <see cref="SmallScale"/>.</summary>
    private float SmallWidth(string text) => _ui.Slim.Width(text) * SmallScale;

    /// <summary>
    /// A skill's popup (<c>WidgetCrafting</c>, <c>WidgetMenuPopup</c>) on the details' parchment at its own
    /// place: its title centred above a list in the slim font, of recipes, tasks, attributes, or members
    /// with their portraits, names and health.
    /// </summary>
    private void Crafting(CraftingMenu crafting)
    {
        var font = _ui.Slim;
        _ui.Parchment(_canvas, ClassicParchment.X, ClassicParchment.Y);
        // The original measures the title as it builds the popup, in the main font, and draws it in the slim one.
        var titleWidth = _ui.Main.Width(crafting.Title);
        _ui.Text(_canvas, font, crafting.Title, CraftingMenu.TitleCentre - titleWidth / 2, CraftingMenu.TitleTop, PartyMenu.TextColor);
        var bounds = crafting.Layout.Bounds;
        var x = bounds.Left;
        _ui.List(crafting.Rows, crafting.Layout, PartyMenu.TextColor, Menu.HighlightNow, (row, y, color) =>
        {
            if (row.Member is { } slot && Menu.Party.Slots.ElementAtOrDefault(slot) is { } member)
            {
                // WidgetMenuHealer::ShowHealedMember: the portrait, the name 2 after it, and the health under the name.
                if (Menu.Portrait(member.Entity) is { } portrait)
                    _ui.Image(_canvas, portrait, x, y, new Vector2I(MemberPortrait, MemberPortrait));
                _ui.Text(_canvas, font, row.Text, x + MemberPortrait + 2, y, bounds, color);
                _ui.Text(_canvas, font, row.Health ?? "", x + MemberPortrait, y + font.LineHeight + 5, bounds, color);
            }
            else
            {
                _ui.Text(_canvas, font, row.Text, x, y, bounds, color);
            }
        });
    }

    /// <summary>
    /// The popups over the window, centred on the screen: the barter state's drop question, then the window's own question
    /// and its message, which is off-white over a feathered black box: as wide as its text up to
    /// 150, and as tall as it wraps, plus 5; a crafting popup's result has 16 more across and 5 more down
    /// (<c>WidgetCrafting::TextPopup</c>).
    /// </summary>
    private void Popups()
    {
        var screen = (Vector2I)Size;
        if (Window is BarterMenu { Confirm: { } confirm } barter)
            Confirm(confirm, barter.PopupHighlight);
        _ui.Overlay(_canvas, screen, Window.Overlay);
    }

    /// <summary>
    /// The portraits from the member shown round, their name, and their aspect's and school's icons. As the member
    /// changes (<c>PartyMenu.PortraitsChange</c>, <c>0x8003b3cc</c>), each portrait moves and resizes from its old place
    /// to its new one, through the frames painted into the tab, which stay, drawn in the party's order as the
    /// original's widgets are. The name crossfades, and the icons where they change.
    /// </summary>
    private void Portraits(Vector2I at)
    {
        var shown = Menu.Shown!;
        var order = Menu.PortraitOrder.ToList();
        var previous = Menu.Previous is null ? null : Menu.PreviousOrder.ToList();
        var change = Menu.MemberChange;
        foreach (var slot in order.Order())
        {
            if (Menu.Portrait(Menu.Party.Slots[slot]!.Entity) is not { } portrait)
                continue;
            var (to, toSize) = PortraitPlace(order.IndexOf(slot));
            var was = previous?.IndexOf(slot) ?? -1;
            var (from, fromSize) = was >= 0 ? PortraitPlace(was) : (to, toSize);
            var size = MenuTween.Value(fromSize, toSize, change);
            _ui.Image(_canvas, portrait, at.X + MenuTween.Value(from.X, to.X, change), at.Y + MenuTween.Value(from.Y, to.Y, change),
                new Vector2I(size, size));
        }
        if (Menu.Previous is { } before)
            _ui.Text(_canvas, _ui.Slim, before.Name, at.X + NameAt.X, at.Y + NameAt.Y, FadingOut(PartyMenu.TextColor, change));
        _ui.Text(_canvas, _ui.Slim, shown.Name, at.X + NameAt.X, at.Y + NameAt.Y, FadingIn(PartyMenu.TextColor, change));
        var icons = Icons(shown);
        var old = Menu.Previous is { } member ? Icons(member) : icons;
        var iconsY = IconsAt.Y;
        foreach (var (icon, x) in old.Except(icons))
            _ui.Image(_canvas, icon, at.X + x, at.Y + iconsY, color: FadingOut(White, change));
        foreach (var (icon, x) in icons)
            _ui.Image(_canvas, icon, at.X + x, at.Y + iconsY, color: old.Contains((icon, x)) ? null : FadingIn(White, change));
    }

    /// <summary>Where the portrait so far round the party from the member shown goes, from the frames' corner, and how big it is.</summary>
    private static (Vector2I At, int Size) PortraitPlace(int i) =>
        i == 0 ? (ShownPortrait, ShownSize) : (OtherPortraits[Math.Min(i - 1, OtherPortraits.Length - 1)], OtherSize);

    /// <summary>A member's aspect's and school's icons after the bar, 4 apart, and where they go across.</summary>
    private List<(RecordId Icon, int X)> Icons(Core.Characters.Character member)
    {
        var icons = new List<(RecordId, int)>();
        var x = IconsAt.X;
        if (MenuImages.AspectIcon(member.Aspect) is { } aspect)
        {
            icons.Add((aspect, x));
            x += _ui.Size(aspect).X + IconGap;
        }
        if (MenuImages.SchoolIcon(member.School) is { } school)
            icons.Add((school, x));
        return icons;
    }

    private static readonly Rgba White = new(255, 255, 255, 255);

    /// <summary>A colour as it fades in, its alpha tweened from 0 (<c>Tween.CreateByte</c>).</summary>
    private static Rgba FadingIn(Rgba color, float ease) => color with { A = (byte)MenuTween.Value(0, color.A, ease) };

    /// <summary>A colour as it fades out, its alpha tweened to 0.</summary>
    private static Rgba FadingOut(Rgba color, float ease) => color with { A = (byte)MenuTween.Value(color.A, 0, ease) };

    /// <summary>
    /// The hit points bar under the portraits, cut to the share left, on a track where the frame painted into the tab was, with the
    /// hit points and their maximum on it, and the level by the name, small; the gold is with the tabs. As the member changes, or a
    /// potion, the equipment, a skill or training changes the member shown (<c>PartyMenu.HealthGoldChange</c>, <c>0x8003be5c</c>),
    /// the bar's width tweens to the new share, and the hit points, their maximum and the level crossfade, even where they're the
    /// same.
    /// </summary>
    private void Health()
    {
        var shown = Menu.Shown!;
        var bar = Bar;
        var change = Menu.NumbersChange;
        var track = new Rect2(bar.Position - new Vector2(0.5f, 0.5f), bar.Size + Vector2.One);
        _canvas.DrawRect(track, _ui.Tint(BarTrack));
        _canvas.DrawRect(track, _ui.Tint(BarEdge), filled: false, width: 0.5f);
        var width = Menu.HealthBar(bar.Size.X);
        if (Menu.PreviousNumbers is { } before)
            width = MenuTween.Value(before.Bar(bar.Size.X), width, change);
        _ui.ImageTo(_canvas, MenuImages.HealthBar, bar.Position.X, bar.Position.Y, bar.Size, bar.Position.X + width);
        if (Menu.PreviousNumbers is { } was)
            Numbers(was, FadingOut(White, change).A);
        Numbers(HeaderNumbers.Of(shown), FadingIn(White, change).A);
    }

    /// <summary>The hit points and their maximum centred on the bar, and the level by the name, at an alpha.</summary>
    private void Numbers(HeaderNumbers numbers, byte a)
    {
        var bar = Bar;
        var hp = $"{Num(numbers.Hp)} / {Num(numbers.MaxHp)}";
        var height = (_ui.Slim.LineHeight - 2) * SmallScale;
        Small(hp, bar.Position.X + (bar.Size.X - SmallWidth(hp)) / 2, bar.Position.Y + (bar.Size.Y - height) / 2, BarText with { A = a },
            BarShadow with { A = (byte)(BarShadow.A * a / 255) });
        var level = $"{LevelLabel} {Num(numbers.Level)}";
        // Level with the name's foot.
        var nameFoot = PortraitPiece.Position.Y + NameAt.Y + _ui.Slim.LineHeight - 2;
        Small(level, LevelRight - SmallWidth(level), nameFoot - height, PartyMenu.TextColor with { A = a });
    }

    /// <summary>
    /// The silhouette: the member's figure behind (<see cref="Figures"/>), crossfading to the new member's as the header changes
    /// over where it's another, and over it the slots, what's worn or each empty slot's image. As the member changes, or an item is
    /// put on or taken off, the slots the core says crossfade (<see cref="PartyMenu.SlotCrossfades"/>) fade from what they showed.
    /// </summary>
    private void Silhouette(Vector2I at)
    {
        var middle = at.X + Slots[(int)EquipmentSlot.Helmet].X + SlotSize / 2f;
        var figure = FigureOf(Menu.Shown!);
        if (Menu.Previous is { } previous && FigureOf(previous) is var was && was != figure)
        {
            FigureArt(was, middle, FadingOut(White, Menu.MemberChange).A / 255f);
            FigureArt(figure, middle, FadingIn(White, Menu.MemberChange).A / 255f);
        }
        else
        {
            FigureArt(figure, middle, 1);
        }

        var change = Menu.SlotsChange;
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            var x = at.X + Slots[(int)slot].X;
            var y = at.Y + Slots[(int)slot].Y;
            var size = new Vector2I(SlotSize, SlotSize);
            if (Menu.PreviousSlots is { } before && Menu.SlotCrossfades(slot))
            {
                _ui.Image(_canvas, before[(int)slot] ?? MenuImages.EmptySlots[(int)slot], x, y, size, FadingOut(White, change));
                _ui.Image(_canvas, Menu.SlotImage(slot), x, y, size, FadingIn(White, change));
            }
            else
            {
                _ui.Image(_canvas, Menu.SlotImage(slot), x, y, size);
            }
        }
    }

    /// <summary>A member's figure, by their entity's record.</summary>
    private Figure FigureOf(Core.Characters.Character member)
    {
        if (!_figures.TryGetValue(member.Entity, out var figure))
            _figures[member.Entity] = figure = Figures.Of(_plugins.TryGet<DbRecord>(member.Entity));
        return figure;
    }

    /// <summary>A figure's art, from <see cref="FigureTop"/> and <see cref="FigureHeight"/> tall, centred on <paramref name="middle"/>, at a strength.</summary>
    private void FigureArt(Figure figure, float middle, float strength)
    {
        var name = figure switch
        {
            Figure.Woman => "silhouette-woman.png",
            Figure.LargeMan => "silhouette-large-man.png",
            _ => "silhouette-man.png",
        };
        if (UiImages.Art(name) is not { } texture)
            return;
        var width = FigureHeight * texture.GetWidth() / texture.GetHeight();
        Art(name, new Rect2(middle - width / 2, FigureTop, width, FigureHeight), strength);
    }

    /// <summary>
    /// Where a list's columns go, from its left edge: the original's (<c>SMIItem</c>, <c>SMStatItem</c>, <c>WidgetSkillInfo</c>,
    /// <c>WidgetSpellEntry</c>). Its names reach its bounds' right edge.
    /// </summary>
    private static class Columns
    {
        public const int Quantity = 26, Name = 42, NameNoQuantity = 30, Value = 79, Change = 100, SkillChange = 95, Rank = 36,
            SpellName = 50;
    }

    /// <summary>
    /// A list, by default the one showing, <paramref name="shift"/> across from its place (<see cref="PartyMenu.LayoutOf"/>), under its
    /// tab, with its arrows and its rows in <paramref name="font"/>, the cursor's in <paramref name="cursor"/>, or pulsing if none is
    /// given. The stats and skills are one page (<see cref="StatsPage"/>).
    /// </summary>
    private void List(int shift, UiFont font, Rgba? cursor = null, PartyList? which = null)
    {
        var shown = which ?? Menu.List;
        var layout = PartyMenu.Layout(shown).Shifted(shift);
        var b = layout.Bounds;
        if (shown == Menu.List)
            _listLayout = layout;
        var highlight = cursor ?? Menu.HighlightNow;
        switch (shown)
        {
            case PartyList.Inventory:
                Items(Menu.Inventory, layout, font, highlight);
                break;
            case PartyList.Stats or PartyList.Skills:
                StatsPage(layout, font, highlight);
                break;
            default:
                SpellRows(Menu.Spells, layout, font, highlight);
                break;
        }
    }

    /// <summary>
    /// The stats and skills page, as far as it's scrolled (<see cref="PartyMenu.PageScroll"/>): the stats' rows, the
    /// experience lines and the skills' rows, each drawn whole or not at all, with the arrows while rows are hidden above or below.
    /// The cursor is on a stat or a skill.
    /// </summary>
    private void StatsPage(ListLayout layout, UiFont font, Rgba highlight)
    {
        var top = layout.Bounds.Top;
        var color = PartyMenu.TextColor;
        var stats = Menu.Stats;
        for (var i = 0; i < stats.Rows.Count; i++)
        {
            if (PartyMenu.PageShows(Menu.StatTop(i), PartyMenu.StatPitch))
                StatRowAt(stats.Rows[i], layout, top + Menu.StatTop(i), font, Menu.List == PartyList.Stats && i == stats.Cursor ? highlight : color);
        }
        ExperienceLinesAt(layout, top + Menu.ExperienceAt - Menu.PageScroll, font, y => PartyMenu.PageShows(y - top, PartyMenu.ExperiencePitch));
        var skills = Menu.Skills;
        for (var i = 0; i < skills.Rows.Count; i++)
        {
            if (PartyMenu.PageShows(Menu.SkillTop(i), PartyMenu.SkillPitch))
                SkillRowAt(skills.Rows[i], layout, top + Menu.SkillTop(i), font, Menu.List == PartyList.Skills && i == skills.Cursor ? highlight : color);
        }
        _ui.Arrows(_canvas, Menu.PageMoreAbove, Menu.PageMoreBelow, layout);
    }

    /// <summary>A stat's row (<c>SMStatItem</c>): its name, its base value, and what gear and spells add when it isn't 0.</summary>
    private void StatRowAt(StatRow row, ListLayout layout, int top, UiFont font, Rgba color)
    {
        var b = layout.Bounds;
        var x = b.Left;
        OneLine(font, row.Name, x, top, b.Right, color);
        _ui.Text(_canvas, font, Num(row.Value), x + Columns.Value, top, b, PartyMenu.TextColor);
        if (row.Change != 0)
            _ui.Text(_canvas, font, Change(row.Change), x + Columns.Change, top, b, row.Change < 0 ? MenuDetailsBuilder.Loss : MenuDetailsBuilder.Gain);
    }

    /// <summary>
    /// The experience lines under the stats, from <paramref name="top"/> a line apart, the values lined up 4 after the first label,
    /// each drawn where <paramref name="shows"/> says its top shows.
    /// </summary>
    private void ExperienceLinesAt(ListLayout layout, int top, UiFont font, Func<int, bool> shows)
    {
        if (Menu.Experience is not { } exp)
            return;
        var b = layout.Bounds;
        var valueX = b.Left + font.Width(exp.RemainingLabel) + 4;
        foreach (var (label, value) in new[] { (exp.RemainingLabel, exp.Remaining), (exp.TotalLabel, exp.Total), (exp.NextLevelLabel, exp.NextLevel) })
        {
            if (shows(top))
            {
                _ui.Text(_canvas, font, label, b.Left, top, b, PartyMenu.TextColor);
                _ui.Text(_canvas, font, value, valueX, top, b, PartyMenu.TextColor);
            }
            top += font.LineHeight;
        }
    }

    /// <summary>
    /// A skills list's rows (<c>WidgetSkillInfo</c>), 13 apart, with their arrows: the icon, the name 12 after
    /// it, the level, and the difference gear makes when it isn't 0.
    /// </summary>
    private void SkillRows(MenuList<SkillRow> list, ListLayout layout, UiFont font, Rgba highlight)
    {
        _ui.List(list, layout, PartyMenu.TextColor, highlight, (row, top, c) => SkillRowAt(row, layout, top, font, c));
        _ui.Arrows(_canvas, list, layout);
    }

    private void SkillRowAt(SkillRow row, ListLayout layout, int top, UiFont font, Rgba color)
    {
        var b = layout.Bounds;
        var x = b.Left;
        if (row.Icon is { } icon)
            _ui.Image(_canvas, icon, x, top);
        OneLine(font, row.Name, x + 12, top, b.Right, color);
        _ui.Text(_canvas, font, Num(row.Value), x + Columns.Value, top, b, PartyMenu.TextColor);
        if (row.Change != 0)
            _ui.Text(_canvas, font, Change(row.Change), x + Columns.SkillChange, top, b, row.Change < 0 ? MenuDetailsBuilder.Loss : MenuDetailsBuilder.Gain);
    }

    /// <summary>
    /// A spells list's rows (<c>WidgetSpellEntry</c>), 13 apart, with their arrows: the aspect's, school's and
    /// spell's icons, the rank and the name.
    /// </summary>
    private void SpellRows(MenuList<SpellRow> list, ListLayout layout, UiFont font, Rgba highlight)
    {
        var b = layout.Bounds;
        var x = b.Left;
        var color = PartyMenu.TextColor;
        _ui.List(list, layout, color, highlight, (row, top, c) =>
        {
            foreach (var (icon, dx) in new[] { (row.Aspect, 0), (row.School, 12), (row.Symbol, 24) })
            {
                if (icon is { } i)
                    _ui.Image(_canvas, i, x + dx, top);
            }
            _ui.Text(_canvas, font, Num(row.Rank), x + Columns.Rank, top, b, color);
            OneLine(font, row.Name, x + Columns.SpellName, top, b.Right, c);
        });
        _ui.Arrows(_canvas, list, layout);
    }

    /// <summary>
    /// An inventory's or a pile's rows (<c>SMIItem</c>), 13 apart, with the arrows 15 in from each side and 13
    /// above: the wearer's portrait and the item's icon at 12 square, then the count and the name.
    /// </summary>
    private void Items(MenuList<InventoryRow> list, ListLayout layout, UiFont font, Rgba cursor)
    {
        var b = layout.Bounds;
        var x = b.Left;
        _ui.List(list, layout, PartyMenu.TextColor, cursor, (row, top, c) =>
        {
            if (row.Wearer is { } wearer && Menu.Party.Slots[wearer] is { } who && Menu.Portrait(who.Entity) is { } portrait)
                _ui.Image(_canvas, portrait, x, top, new Vector2I(12, 12));
            _ui.Image(_canvas, row.Item.Icon, x + 13, top, new Vector2I(12, 12));
            if (row.Quantity != 0)
                _ui.Text(_canvas, font, Num(row.Quantity), x + Columns.Quantity, top, b, PartyMenu.TextColor);
            OneLine(font, row.Item.Name, x + (row.Quantity != 0 ? Columns.Name : Columns.NameNoQuantity), top, b.Right, c);
        });
        _ui.Arrows(_canvas, list, layout);
    }

    /// <summary>
    /// C-down's details (<c>WidgetItemDetail</c>) in an area: a heading, the lines, and the footer, pinned where
    /// the original pins it.
    /// </summary>
    private void Details(MenuDetails details, ScreenRect area, UiFont font)
    {
        var y = area.Top;
        if (details.Icon is { } icon)
        {
            // The icon, and the name level with its foot.
            var size = _ui.Size(icon);
            _ui.Image(_canvas, icon, area.Left, y);
            y += size.Y + 2;
            if (details.Title is { } title)
                _ui.Text(_canvas, font, title, area.Left + size.X + 2, y - font.LineHeight, PartyMenu.TextColor);
            y += 4;
        }
        foreach (var line in details.Lines)
            y += Line(line, y);

        var footerHeight = details.Footer.Count * font.LineHeight;
        var footerTop = details.FooterTop ?? (details.FooterBottom is { } bottom ? bottom - footerHeight : y);
        foreach (var line in details.Footer)
            footerTop += Line(line, footerTop);

        int Line(DetailLine line, int top)
        {
            var height = font.LineHeight;
            var x = area.Left + (line.Indent ? DetailsIndent : 0);
            foreach (var piece in line.Left)
            {
                switch (piece)
                {
                    case DetailText text when line.Wrap:
                        height = _ui.Text(_canvas, font, text.Text, x, top, new ScreenRect(area.Left, 0, area.Right, (int)Size.Y), text.Color);
                        break;
                    case DetailText text:
                        _ui.Text(_canvas, font, text.Text, x, top, text.Color);
                        x += font.Width(text.Text);
                        break;
                    case DetailImage image:
                        var size = image.Size is { } s ? new Vector2I(s, s) : _ui.Size(image.Image);
                        _ui.Image(_canvas, image.Image, x, top, size);
                        x += size.X + 2;
                        break;
                }
            }
            var right = area.Right;
            foreach (var piece in line.Right.Reverse())
            {
                switch (piece)
                {
                    case DetailText text:
                        right -= font.Width(text.Text);
                        _ui.Text(_canvas, font, text.Text, right, top, text.Color);
                        break;
                    case DetailImage image:
                        // An aspect's icon sits 2 higher, and its line is as tall as it is, plus 1.
                        var size = _ui.Size(image.Image);
                        right -= size.X;
                        _ui.Image(_canvas, image.Image, right, top - 2);
                        height = size.Y + 1;
                        break;
                }
            }
            return height;
        }
    }

    private static string Num(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Change(int change) => change < 0 ? $"({Num(change)})" : $"(+{Num(change)})";
}
