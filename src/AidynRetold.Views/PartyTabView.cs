using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Views;

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

    /// <summary>The member's name, a heading, larger than the menus' text; the slim font at its own size in the original's layout.</summary>
    private UiFont NameFont => OriginalLook ? _ui.Slim : _ui.Slim.Sized(UiPainter.HeadingText);

    // The original's layout (PartyMenu.OriginalLook), Graphic Quality's Original, the user's choice (Nov 2, 2026): the tab's own image,
    // 0x00D9, with its frames painted in, and what the original draws on its pieces where it draws it.

    /// <summary>Whether the tab is laid out as the original's (<see cref="PartyMenu.OriginalLook"/>).</summary>
    private bool OriginalLook => Menu.OriginalLook;

    /// <summary>The aspect's and school's icons under the large portrait, from the portraits' frames' corner, 2 apart.</summary>
    private static readonly Vector2I OriginalIconsAt = new(4, 48);

    private const int OriginalIconGap = 2, OriginalHungerIcon = 10;

    /// <summary>The hit points, level and gold labels and the bar's frame (<c>WidgetHealthGold</c>), and what's drawn on them.</summary>
    private static readonly Rect2I HealthPiece = new(160, 22, 140, 58);

    private static readonly Vector2I HealthBarAt = new(60, 5), HealthBarSize = new(80, 10);
    private static readonly Vector2I HpAt = new(63, 19), MaxHpAt = new(95, 19), LevelAt = new(63, 33), GoldAt = new(63, 47);

    /// <summary>The silhouette's corner, where its slots' frames are painted (<c>DollEquipmentMenu::SetSlotCoords</c>).</summary>
    private static readonly Vector2I OriginalSilhouette = new(18, 72);

    /// <summary>
    /// What a list and its title are cut to as they slide: the list's bounds across, x 160 to 300, and from under the gold to the
    /// list's foot. The original cuts the rows to the bounds and the title to x 170 to 300.
    /// </summary>
    private static readonly Rect2I OriginalListClip = new(160, 78, 140, 128);

    /// <summary>The arrow under the knotwork.</summary>
    private static readonly Vector2I OriginalSliderAt = new(126, 207);
    private static readonly Vector2I[] OtherPortraits = [new(49, 17), new(78, 17), new(107, 17)];
    private const int ShownSize = 40, OtherSize = 24;

    /// <summary>
    /// The hit points bar beside the aspect's and school's icons and as tall as they are, the user's art and the user's choice (Nov 2,
    /// 2026): its frame, 850 × 200, rounded at its left end and with a finial on its right (ui_bar_frame_asymmetrical), over this
    /// rectangle, its drawn left edge under the large portrait's and its right end at x 121, its ends keeping their shape and its middle
    /// stretched between them (<see cref="ThreeSlice"/>); the brown track and the red fill (ui_bar_brown, ui_bar_red) under
    /// it, sliced alike, the red cut to the share of hit points left across the frame's opening. The hit points are on it.
    /// </summary>
    private static readonly Rect2 BarFrame = BarFrom(PortraitPiece.Position.X + ShownPortrait.X, 121, 66.5f, 11);

    /// <summary>The bar frame's rectangle with its drawn left edge, <see cref="BarArtLeft"/> into its art, at a point across.</summary>
    private static Rect2 BarFrom(float left, float right, float top, float height)
    {
        var x = left - BarArtLeft * height / BarArtHeight;
        return new Rect2(x, top, right - x, height);
    }

    /// <summary>
    /// The bar's art: its size, how far in its frame's drawn left edge is, how wide each end kept whole is, and its opening's sides, top
    /// and bottom, in its pixels.
    /// </summary>
    private const float BarArtWidth = 850, BarArtHeight = 200, BarArtLeft = 65, BarEnd = 140, BarOpeningLeft = 93, BarOpeningRight = 753,
        BarOpeningTop = 63, BarOpeningBottom = 139;

    private const string BarFrameArt = "ui_bar_frame_asymmetrical", BarFillArt = "ui_bar_red", BarTrackArt = "ui_bar_brown";

    /// <summary>How finely the bar's share of hit points is taken, as a width it's measured across.</summary>
    private const int BarPrecision = 1000;

    /// <summary>How tall the hit points' capitals are on the bar.</summary>
    private const float HitPointsText = 3;

    /// <summary>The bar frame's opening, where it's drawn, which the hit points are centred in and the red fills.</summary>
    private static Rect2 BarOpening
    {
        get
        {
            var scale = BarFrame.Size.Y / BarArtHeight;
            var left = BarFrame.Position.X + BarOpeningLeft * scale;
            var right = BarFrame.End.X - (BarArtWidth - BarOpeningRight) * scale;
            return new Rect2(left, BarFrame.Position.Y + BarOpeningTop * scale, right - left, (BarOpeningBottom - BarOpeningTop) * scale);
        }
    }

    /// <summary>The aspect's and school's icons after the bar, from the portraits' frames' corner, and how far apart they are.</summary>
    private static readonly Vector2I IconsAt = new(107, 45);

    /// <summary>
    /// How big the aspect's and school's icons are drawn, a quarter smaller than the original's 10, and how far apart, the user's choice
    /// (Nov 2, 2026); their top, from the portraits' frames' corner, so their middle is the bar's.
    /// </summary>
    private const float IconSize = 7.5f, IconGap = 1, IconsTop = 46.25f;

    /// <summary>The level's right edge, under the small portraits' right edge, and the gold's, at the list's.</summary>
    private const int LevelRight = 149;

    /// <summary>
    /// The right column's page, the user's design (Nov 2, 2026): a loose sheet of the user's parchment (<see cref="UiPainter.Slip"/>) the
    /// list is on, over this rectangle, a page for each section, which one shows standing for the stack without the others being drawn;
    /// and a paper tab sticking up from its top for each section.
    /// </summary>
    private static readonly Rect2 PageRect = new(158, 41.5f, 153, 182.5f);

    /// <summary>
    /// The sheets of parchment the sections lie on, where the user marked their edges (Nov 2, 2026), a gap of 3 or 4 between each and
    /// none overlapping: the portraits with the name, the level, the bar and the aspect's and school's icons on one, the silhouette
    /// with its slots on another under it; the page (<see cref="PageRect"/>) beside them. The gold's is snug round it (<see cref="GoldBox"/>).
    /// </summary>
    private static readonly Rect2 HeaderSheet = new(15.5f, 19, 138, 59.5f), EquipmentSheet = new(15.5f, 81.5f, 138.5f, 142);

    /// <summary>
    /// How far each portrait's square sheet reaches past the portrait, cut to the sheet's outline (<see cref="UiImages.CutTo"/>), so a
    /// rim of the sheet shows round it: 5 pixels of a screen 1440 lines tall, the user's choice.
    /// </summary>
    private const float PortraitBorder = 0.85f;

    /// <summary>
    /// The tabs: the first one's corner, each one's paper, and how far apart they start, a pixel less than they're wide, so they
    /// meet. Each tab's foot runs 6 down behind its page's top, the tab showing's joined to the front page by plain paper.
    /// </summary>
    private static readonly Vector2 TabsAt = new(161, 18);

    private const float TabWidth = 28, TabHeight = 28, TabStep = 27;

    /// <summary>
    /// A tab's icon, centred across it, this far down it and this big, with its caption under it, at most this far in from either
    /// side.
    /// </summary>
    private const int TabIconTop = 4, TabIconSize = 11, CaptionInset = 4;

    /// <summary>
    /// How strongly a tab that isn't showing draws its icon and caption, and one the window hasn't got; and how strongly one the window
    /// hasn't got draws its paper.
    /// </summary>
    private const float ClosedIcon = 0.7f, MissingIcon = 0.3f, MissingTab = 0.5f;

    /// <summary>The colour the tabs behind the front page are drawn in, a shade darker, and one the mouse is on, lighter.</summary>
    private static readonly Rgba BehindTab = new(220, 206, 186, 255), HoveredTab = new(242, 236, 226, 255);

    private static readonly string[] TabIcons = ["ui_inventory", "ui_stats", "ui_skills", "ui_spells"];

    /// <summary>The purse before the gold, the user's art, and how big it's drawn, its middle level with the gold's.</summary>
    private const string Purse = "ui_gold";

    /// <summary>
    /// The gold's box, smaller, the user's choice (Nov 2, 2026): the purse this big, a pixel before the amount, whose capitals are this
    /// tall; the sheet this tall, reaching this far past them either side, its right edge at the page's.
    /// </summary>
    private const float PurseSize = 10, PurseGap = 1, GoldText = 3.4f, GoldHeight = 14, GoldInset = 3.5f;

    /// <summary>How far down the tabs the gold's middle is, level with the tabs' middles.</summary>
    private const float GoldMiddle = 12;


    /// <summary>
    /// The box each of the silhouette's slots sits in, over the figure, the user's art (ui_square_solid), a pixel round the
    /// slot's 16; and how big what's in a box is drawn, and how far in from the slot's corner, inside the box's inner line, which is a
    /// tenth of the way in, the user's choice (Nov 2, 2026).
    /// </summary>
    private const string SlotBox = "ui_square_solid";

    private const int BoxedSize = 12, SlotInset = 2;

    /// <summary>
    /// The silhouette's slots' corner, centring them in the column under the bar (<see cref="PartyMenu.SilhouetteCorner"/>). Their places
    /// are the original's, from its silhouette's corner, but the port's boots (<see cref="PartyMenu.SlotPlace"/>).
    /// </summary>
    private static readonly Vector2I SilhouetteAt = new(PartyMenu.SilhouetteCorner.X, PartyMenu.SilhouetteCorner.Y);

    /// <summary>
    /// The figure behind the slots, the user's art (<see cref="Figures"/>): its top and how tall it's drawn, centred under the helmet's
    /// slot, which puts its head under that slot and its feet at the boots'. Its width is the art's shape.
    /// </summary>
    private const float FigureTop = 82.7f, FigureHeight = 138;

    /// <summary>Where a slot is from the silhouette's corner: the original's, but the port's boots.</summary>
    private Vector2I SlotOffset(EquipmentSlot slot)
    {
        var (x, y) = PartyMenu.SlotPlace(slot, OriginalLook);
        return new Vector2I(x, y);
    }

    private const int SlotSize = PartyMenu.SlotSize;

    /// <summary>
    /// What the list and its arrows are cut to as they slide: across the list's frame's inside, from under the tabs to the list's foot,
    /// which the grid's scroll bar beside it is in too.
    /// </summary>
    private static readonly Rect2I ListClip = new(158, 43, 151, 180);


    // WidgetItemDetail: the parchment, and its text's left and right edges and top, as the original places them; what goes over
    // the silhouette is drawn Drop lower.
    private static readonly Vector2I ClassicParchment = new(0, 58);
    private static readonly ScreenRect ClassicDetails = new(23, 70, 145, 218);
    private const int DetailsIndent = 16;

    /// <summary>
    /// The paper the details, the skills' popups and the windows' own parts are on, over the silhouette: a slip of the user's
    /// (<see cref="UiPainter.Slip"/>) where the original's parchment popup lay (<c>WidgetMenuPopup</c>), as big.
    /// </summary>
    private void Paper()
    {
        if (OriginalLook)
            _ui.Image(_canvas, MenuImages.Parchment, ClassicParchment.X, ClassicParchment.Y);
        else
            _ui.Slip(_canvas, new Rect2(ClassicParchment, _ui.Size(MenuImages.Parchment)));
    }

    // WidgetCrafting (CraftingMenu): the title centred on the details' parchment, its list under it. A member's row is its portrait's 25 in the original, so
    // the portraits touch and the next name overlaps the health by 2; the port leaves 3 between them, as the user asked.
    private const int MemberPortrait = 25;

    /// <summary>
    /// How far down what goes over the silhouette is drawn from where the original has it, C-down's details, the skills' popups and
    /// the barter, training and post-battle windows' own parts, so their parchment clears the bar and covers the silhouette's
    /// column, to the full-height parchment's foot; not at all in the original's layout.
    /// </summary>
    private int Drop => OriginalLook ? 0 : 24;

    /// <summary>The colours of the hit points on the bar, with their shadow.</summary>
    private static readonly Rgba BarText = new(250, 242, 226, 255), BarShadow = new(40, 20, 14, 170);

    /// <summary>
    /// The red flourish under a heading, the user's art (ui_flourish_red, 655 × 115): how wide it's drawn under the details' title,
    /// and how much further down that puts the lines under it.
    /// </summary>
    private const string Flourish = "ui_flourish_red";

    private const float FlourishWidth = 60;

    private const int FlourishRoom = 8;

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
    /// <summary>The silhouette's slot the mouse is on, or null.</summary>
    private EquipmentSlot? _hoveredSlot;
    /// <summary>The inventory's row whose grid cell the mouse is on, or null.</summary>
    private int? _hoveredCell;
    /// <summary>The aspect's or school's icon the mouse is on, by its place among <see cref="Icons"/>, or null.</summary>
    private int? _hoveredIcon;
    /// <summary>The actiontip a right click opened and where in the frame, which it opens beside; the stick's opens by its item.</summary>
    private (ActionTip Tip, Vector2 At)? _tipAt;
    /// <summary>Where the inventory's grid was last drawn, which the mouse is tested against, as <see cref="_listLayout"/> is for a list.</summary>
    private GridLayout _gridLayout = PartyMenu.InventoryGrid;
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
        _ui = new UiPainter(plugins) { TextSize = UiPainter.StandardText, Slips = true };
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
            (_hoveredTab, _hoveredIcon) = (null, null);
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
        {
            _hoveredTab = TabAt(mouse.Position) is { } tab && HasTab(PartyMenu.Tabs[tab]) ? tab : null;
            _hoveredSlot = SlotAt(mouse.Position);
            _hoveredIcon = IconAt(mouse.Position);
            _hoveredCell = null;
        }
        if (TipInput(@event) || DragInput(@event))
        {
            AcceptEvent();
            return;
        }
        // A click on the filter or the order under the grid steps it on.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } footClick && OwnTab && !OriginalLook
            && Menu.List == PartyList.Inventory && Menu.Grid && Menu.Slide is null && !Menu.HasPopup && Menu.Fitting is null
            && FootControlAt(footClick.Position - (Vector2)_origin) is { } choice)
        {
            Menu.ChooseFoot(choice);
            AcceptEvent();
            return;
        }
        // The message's Close button closes it, as A does.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } closing
            && _ui.OnMessageClose((Vector2I)Size, Window.Overlay, closing.Position))
        {
            UiScreen.Of(this)?.Click(MenuAction.Accept);
            AcceptEvent();
            return;
        }
        // A click on a small portrait shows that member, the user's choice (Nov 2, 2026): it aims C-left at them and presses it, so the
        // portraits move and sound as C-left's do, and wait as it waits. The stick's and the C-buttons' changes are as they were.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } portraitClick && Window.ChangesMember
            && MemberAt(portraitClick.Position) is { } member)
        {
            Menu.AimAtMember(member);
            UiScreen.Of(this)?.Click(MenuAction.MemberLeft);
            Menu.AimAtMember(null);
            AcceptEvent();
            return;
        }
        // A click on a slot of the silhouette with something worn in it selects it, and a double click uses it as A does, taking it off:
        // with what's worn not in the inventory, the silhouette is where the mouse reaches it.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } slotClick && _hoveredSlot is { } slot)
        {
            Menu.SelectSlot(slot);
            if (slotClick.DoubleClick)
                UiScreen.Of(this)?.Click(MenuAction.Accept);
            AcceptEvent();
            return;
        }
        // A click on a tab aims the stick at it and presses it toward it, so it slides and sounds as the stick does, and waits as
        // the stick waits.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _hoveredTab is { } clicked)
        {
            var step = PartyMenu.StepToTab(PartyMenu.TabOf(Menu.List), clicked);
            if (step != 0)
            {
                Menu.AimAt(PartyMenu.Tabs[clicked]);
                UiScreen.Of(this)?.Click(step > 0 ? MenuAction.Right : MenuAction.Left);
                Menu.AimAt(null);
            }
            AcceptEvent();
            return;
        }
        // A click on a row or a cell selects it, the user's choice (Nov 2, 2026), and a double click uses it, as A does; anywhere else
        // it does nothing.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            if (Hover(click.Position, select: true))
            {
                QueueRedraw();
                if (click.DoubleClick)
                    UiScreen.Of(this)?.Click(MenuAction.Accept);
            }
            AcceptEvent();
            return;
        }
        // Hovering a list's row puts the cursor on it, and a grid's cell shows its name; the wheel moves the cursor.
        if (@event is InputEventMouseMotion motion && Hover(motion.Position, select: false))
        {
        }
        else if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel)
        {
            var by = wheel.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
            // On the tab the wheel scrolls the grid, the name's slip going to the cell now under the mouse, or moves a list's cursor
            // (PartyMenu.Wheel); elsewhere it moves the cursor.
            if (OwnTab)
            {
                Menu.Wheel(by);
                if (Menu.List == PartyList.Inventory && Menu.Grid)
                    Hover(wheel.Position, select: false);
            }
            else
            {
                Window.MoveCursor(by);
            }
        }
        else
            return;
        QueueRedraw();
        AcceptEvent();
    }

    /// <summary>What a drag picked up: a carried item from the grid, or what's worn in a slot of the silhouette.</summary>
    private readonly record struct DragSource(InventoryRow Row, EquipmentSlot? Slot);

    /// <summary>What the left button was pressed on that could be dragged, and where; and the drag under way, with where the mouse is.</summary>
    private (DragSource From, Vector2 At)? _press, _drag;

    /// <summary>How far the mouse moves with the button down before a press on an item becomes a drag, in the frame's pixels.</summary>
    private const float DragDistance = 2.5f;

    /// <summary>
    /// Dragging items with the mouse in the port's look, the user's choice (Nov 3, 2026): a press on a carried item in the grid or on what's
    /// worn in a slot, which still selects it, becomes a drag once the mouse moves with the button down; let go, a carried item on a slot
    /// it fits is put on in it (<see cref="PartyMenu.DropOnSlot"/>), on a portrait goes to that member (<see cref="PartyMenu.DropOnMember"/>),
    /// and what's worn let go on the grid comes off (<see cref="PartyMenu.DropOffSlot"/>); anywhere else nothing. True if the event was the drag's.
    /// </summary>
    private bool DragInput(InputEvent e)
    {
        if (!OwnTab || OriginalLook)
            return false;
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } press:
                _drag = null;
                _press = Menu.HasPopup || Menu.Slide is not null ? null : DragFrom(press.Position) is { } from ? (from, press.Position) : null;
                return false;
            case InputEventMouseMotion motion when _press is { } pressed && (motion.ButtonMask & MouseButtonMask.Left) != 0:
                if (_drag is null && (motion.Position - pressed.At).Length() < DragDistance)
                    return false;
                _drag = (pressed.From, motion.Position);
                QueueRedraw();
                return true;
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } release:
                var drag = _drag;
                (_press, _drag) = (null, null);
                if (drag is null)
                    return false;
                LetGo(drag.Value.From, release.Position);
                QueueRedraw();
                return true;
        }
        return false;
    }

    /// <summary>What a press at a point on the view would drag: a carried item in the grid, or what's worn in a slot.</summary>
    private DragSource? DragFrom(Vector2 point)
    {
        var at = point - (Vector2)_origin;
        if (SlotAt(point) is { } slot)
            return Menu.Worn(slot) is { } worn ? new DragSource(new InventoryRow(worn, 1, Menu.Member), slot) : null;
        return Menu.List == PartyList.Inventory && Menu.Grid && _gridLayout.RowAt(Menu.Inventory, at.X, at.Y) is { } cell
            && Menu.Inventory.Rows[cell] is { Wearer: null } row ? new DragSource(row, null) : null;
    }

    /// <summary>Where a drag was let go: on a slot or a portrait for a carried item, on the grid for what's worn.</summary>
    private void LetGo(DragSource from, Vector2 point)
    {
        var at = point - (Vector2)_origin;
        if (from.Slot is { } worn)
        {
            var b = _gridLayout.Bounds;
            if (new Rect2(b.Left, b.Top, _gridLayout.Columns * _gridLayout.Cell, _gridLayout.Lines * _gridLayout.Cell).HasPoint(at))
                Menu.DropOffSlot(worn);
            return;
        }
        if (SlotAt(point) is { } slot)
            Menu.DropOnSlot(from.Row, slot);
        else if (MemberAt(point) is { } member)
            Menu.DropOnMember(from.Row, member);
        else if (new Rect2(PortraitPiece.Position + ShownPortrait, ShownSize, ShownSize).HasPoint(at))
            Menu.DropOnMember(from.Row, Menu.Member);
    }

    /// <summary>
    /// A drag under way: the slots a carried item fits outlined in green, or the grid for what's worn, and the item's icon under the mouse.
    /// </summary>
    private void DragArt((DragSource From, Vector2 At) drag)
    {
        var green = _ui.Tint(MenuDetailsBuilder.Gain);
        if (drag.From.Slot is null)
        {
            foreach (var slot in Enum.GetValues<EquipmentSlot>())
            {
                if (PartyMenu.Fits(drag.From.Row.Item, slot))
                    _canvas.DrawRect(SlotRect(slot).Grow(1.5f), green, false, DragOutline);
            }
        }
        else
        {
            var b = _gridLayout.Bounds;
            _canvas.DrawRect(new Rect2(b.Left, b.Top, _gridLayout.Columns * _gridLayout.Cell, _gridLayout.Lines * _gridLayout.Cell), green, false,
                DragOutline);
        }
        var at = drag.At - (Vector2)_origin;
        _ui.Image(_canvas, Menu.ItemIcon(drag.From.Row.Item), new Rect2(at - new Vector2(DragIcon, DragIcon) / 2, DragIcon, DragIcon),
            new Rgba(255, 255, 255, 220));
    }

    /// <summary>How big the dragged item's icon is drawn, and the outlines' thickness.</summary>
    private const float DragIcon = 14, DragOutline = 0.6f;

    /// <summary>
    /// The mouse and the actiontip (<see cref="ActionTip"/>): a click while the details it held open show lets them go; while it's up,
    /// hovering a choice points at it, a click on one chooses it as A does, and a click elsewhere closes it, a right click on an item
    /// opening it there instead; and with none up, a right click on a worn slot or a grid cell selects the item and opens one beside the
    /// mouse, where a right click elsewhere goes on to be B. True if the event was the actiontip's.
    /// </summary>
    private bool TipInput(InputEvent e)
    {
        if (!OwnTab || e is not InputEventMouse mouse)
            return false;
        var at = mouse.Position - (Vector2)_origin;
        if (Menu.DetailsPane.Pinned)
        {
            if (e is InputEventMouseButton { Pressed: true })
                Menu.ReleaseDetails();
            return e is InputEventMouseButton;
        }
        if (Menu.Tip is { } tip)
        {
            var row = TipChoiceAt(tip, at);
            switch (e)
            {
                case InputEventMouseMotion when row is { } hovered:
                    Menu.PointTip(hovered);
                    return true;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } when row is { } chosen:
                    Menu.PointTip(chosen);
                    UiScreen.Of(this)?.Click(MenuAction.Accept);
                    return true;
                case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } click:
                    Menu.CloseTip();
                    if (click.ButtonIndex == MouseButton.Right)
                        OpenTipAt(at);
                    return true;
                default:
                    return e is InputEventMouseButton;
            }
        }
        return e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && OpenTipAt(at);
    }

    /// <summary>
    /// Selects the item under a point in the frame, a worn slot's or a grid cell's, and opens the actiontip on it there, if there's
    /// anything to offer. True if there was an item, so the right click isn't B.
    /// </summary>
    private bool OpenTipAt(Vector2 at)
    {
        if (Menu.HasPopup || Menu.OriginalLook || Menu.Slide is not null)
            return false;
        if (_hoveredSlot is { } slot)
            Menu.SelectSlot(slot);
        else if (Menu.List == PartyList.Inventory && Menu.Grid && _gridLayout.RowAt(Menu.Inventory, at.X, at.Y) is { } cell)
            Menu.Point(cell);
        else
            return false;
        if (Menu.OpenTip())
            _tipAt = (Menu.Tip!, at);
        return true;
    }

    /// <summary>
    /// Puts the window's cursor on the row under a point, as hovering does: the pile's or the party's list in the barter
    /// state, the trainer's in training, and the tab's list or its crafting popup's; on the inventory's grid the cell under it is
    /// only hovered, its name shown, unless <paramref name="select"/>, as a click selects it. True if there was a row, and no popup
    /// took the presses.
    /// </summary>
    private bool Hover(Vector2 point, bool select)
    {
        var at = point - (Vector2)_origin;
        // What's drawn over the silhouette is drawn lower in the tabbed layout.
        var left = at - new Vector2(0, Drop);
        switch (Window)
        {
            case BarterMenu { HasPopup: false } barter:
                if (BarterMenu.PileLayout.RowAt(barter.Pile, left.X, left.Y) is { } pile)
                    barter.Point(true, pile);
                else if (Menu.LayoutOf(PartyList.Inventory).RowAt(Menu.Inventory, at.X, at.Y) is { } party)
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
                return PointTabRow(menu, at, select);
            case PostBattleMenu { HasPopup: false } postBattle:
                return PointTabRow(postBattle.Party, at, select);
            default:
                return false;
        }
    }

    /// <summary>The tab whose banner is under a point on the view, or null.</summary>
    private int? TabAt(Vector2 point)
    {
        if (Menu.Shown is null || OriginalLook)
            return null;
        var at = point - (Vector2)_origin;
        for (var i = 0; i < PartyMenu.Tabs.Count; i++)
        {
            var rect = TabRect(i);
            if (at.X >= rect.Position.X + 0.5f && at.X < rect.End.X - 0.5f && at.Y >= rect.Position.Y + 1 && at.Y < PageRect.Position.Y)
                return i;
        }
        return null;
    }

    /// <summary>The party slot of the member whose small portrait is under a point on the view, or null; the large one's is shown.</summary>
    private int? MemberAt(Vector2 point)
    {
        if (Menu.Shown is null)
            return null;
        var at = point - (Vector2)_origin - (Vector2)PortraitPiece.Position;
        var order = Menu.PortraitOrder;
        for (var i = 1; i < order.Count; i++)
        {
            var (place, size) = PortraitPlace(i);
            if (new Rect2(place, new Vector2(size, size)).HasPoint(at))
                return order[i];
        }
        return null;
    }

    /// <summary>Where a tab's paper is in the frame.</summary>
    private static Rect2 TabRect(int tab) => new(TabsAt.X + tab * TabStep, TabsAt.Y, TabWidth, TabHeight);

    /// <summary>
    /// The silhouette's slot under a point on the view, on the tab itself, or null: in the original's look only one the member shown wears
    /// something in; in the port's any, as an empty one is selected too.
    /// </summary>
    private EquipmentSlot? SlotAt(Vector2 point)
    {
        if (!OwnTab || Menu.Shown is null)
            return null;
        var at = point - (Vector2)_origin;
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            if (SlotRect(slot).HasPoint(at) && (Menu.Worn(slot) is not null || !OriginalLook))
                return slot;
        }
        return null;
    }

    /// <summary>The aspect's or school's icon under a point on the view, by its place among <see cref="Icons"/>, or null.</summary>
    private int? IconAt(Vector2 point)
    {
        if (Menu.Shown is not { } shown)
            return null;
        var at = point - (Vector2)_origin;
        var icons = Icons(shown);
        var index = icons.FindIndex(i => IconRect(i).HasPoint(at));
        return index >= 0 ? index : null;
    }

    /// <summary>Where an icon of <see cref="Icons"/> is in the frame.</summary>
    private Rect2 IconRect((RecordId Icon, float X, string Name) icon) =>
        new(PortraitPiece.Position + new Vector2(icon.X, IconsY), OriginalLook ? _ui.Size(icon.Icon) : new Vector2(IconSize, IconSize));

    /// <summary>How far down the portraits' frames the aspect's and school's icons are.</summary>
    private float IconsY => OriginalLook ? OriginalIconsAt.Y : IconsTop;

    /// <summary>An aspect's or school's icon at a place in the frame: the original's own size in its look, else <see cref="IconSize"/>.</summary>
    private void Icon(RecordId icon, float x, float y, Rgba? color)
    {
        // Hunger's icon has no original's size, so it's drawn at the original's icons', 10 square.
        if (OriginalLook && (icon == HungerDisplay.HungryIcon || icon == HungerDisplay.StarvingIcon))
            _ui.Image(_canvas, icon, new Rect2(x, y, OriginalHungerIcon, OriginalHungerIcon), color);
        else if (OriginalLook)
            _ui.Image(_canvas, icon, (int)x, (int)y, color: color);
        else
            _ui.Image(_canvas, icon, new Rect2(x, y, IconSize, IconSize), color);
    }

    /// <summary>Where a slot of the silhouette is in the frame.</summary>
    private Rect2 SlotRect(EquipmentSlot slot) =>
        new(SilhouetteCorner.X + SlotOffset(slot).X, SilhouetteCorner.Y + SlotOffset(slot).Y, SlotSize, SlotSize);

    /// <summary>The silhouette's slots' corner, the port's or the original's.</summary>
    private Vector2I SilhouetteCorner => OriginalLook ? OriginalSilhouette : SilhouetteAt;

    /// <summary>Puts the cursor on the tab's list row under a point in the frame, or the crafting popup's while it's open. True if there was a row.</summary>
    private bool PointTabRow(PartyMenu menu, Vector2 at, bool select)
    {
        if (menu.Crafting is null && menu.List == PartyList.Inventory && menu.Grid && !select)
        {
            _hoveredCell = _gridLayout.RowAt(menu.Inventory, at.X, at.Y);
            return _hoveredCell is not null;
        }
        var row = menu.Crafting is { } crafting
            ? crafting.Layout.RowAt(crafting.Rows, at.X, at.Y - Drop)
            : menu.List switch
            {
                PartyList.Inventory when menu.Grid => _gridLayout.RowAt(menu.Inventory, at.X, at.Y),
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
        var clip = OriginalLook ? OriginalListClip : ListClip;
        _listClip.Position = _origin + clip.Position;
        _listClip.Size = clip.Size;
        _listLayer.Position = -clip.Position;
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
        _ui.Tab(_canvas, PauseTab.Party, PlainPatches, plain: !OriginalLook);
        _ui.Slip(_canvas, HeaderSheet, sheet: UiPainter.WideSheet);
        Portraits(PortraitPiece.Position);
        Health();
    }

    /// <summary>The barter, training or post-battle state's own parts, over the header, as their fade has them.</summary>
    private void DrawWindow()
    {
        if (Menu.Shown is null || OwnTab)
            return;
        Begin(_windowLayer, _origin);
        // Each window has its own tabs, which fade with it; the original's has none.
        if (!OriginalLook)
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
        var left = Menu.LayoutOf(Menu.List).Bounds.Left;
        var by = slide.Direction * Menu.SlideDistance;
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
        Paper();
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
        // The original shows no names on slips. The prompts are over the details' paper, and go with the veil as the tab slides away.
        if (!OriginalLook)
            Tooltip();
        if (OwnTab && Veil == 0)
            PromptStrip();
        if (OwnTab && Menu.Tip is { } tip)
            ActionTipBox(tip);
        if (_drag is { } drag)
            DragArt(drag);
        _overlay.DrawSetTransform(Vector2.Zero);
        Popups();
    }

    /// <summary>The plain parchment, the header, the silhouette's slots in their column, the tabs and the arrow under the gap between the columns.</summary>
    private void DrawTab()
    {
        if (OriginalLook)
        {
            // The tab's image, its frames painted in, the header on it, the silhouette, and the arrow under the knotwork.
            _ui.Tab(_canvas, PauseTab.Party, PlainPatches);
            Portraits(PortraitPiece.Position);
            Health();
            Silhouette(OriginalSilhouette);
            _ui.Image(_canvas, Menu.Combat is not null ? MenuImages.SliderLeft : MenuImages.SliderBoth, OriginalSliderAt.X, OriginalSliderAt.Y,
                color: PartyMenu.TextColor);
            return;
        }
        _ui.Tab(_canvas, PauseTab.Party, PlainPatches, plain: true);
        _ui.Slip(_canvas, EquipmentSheet, sheet: UiPainter.LargeSheet);
        _ui.Slip(_canvas, HeaderSheet, sheet: UiPainter.WideSheet);
        Portraits(PortraitPiece.Position);
        Health();
        Silhouette(SilhouetteAt);
        CardStats();
        Tabs();
    }

    /// <summary>
    /// The buttons' prompts along the tab's foot, under the sheets, the user's choice (Nov 3, 2026): each button's name in red ink and what
    /// it does after it, centred across the sheets, a gap between each (<see cref="PartyMenu.Prompts"/>).
    /// </summary>
    private void PromptStrip()
    {
        var prompts = Menu.Prompts;
        if (prompts.Count == 0)
            return;
        var font = _ui.Slim.Sized(PromptText);
        var widths = prompts.Select(p => font.ExactWidth(p.Keys) + PromptKeyGap + font.ExactWidth(p.Label)).ToList();
        var x = (HeaderSheet.Position.X + PageRect.End.X) / 2 - (widths.Sum() + PromptGap * (prompts.Count - 1)) / 2;
        for (var i = 0; i < prompts.Count; i++)
        {
            var (keys, label) = (prompts[i].Keys, prompts[i].Label);
            Small(keys, x, PromptMiddle - font.Middle, UiPainter.RedInk, font: font);
            Small(label, x + font.ExactWidth(keys) + PromptKeyGap, PromptMiddle - font.Middle, PartyMenu.TextColor, font: font);
            x += widths[i] + PromptGap;
        }
    }

    /// <summary>The prompts' capitals' height, their middle down the frame, and the gaps after a button's name and between prompts.</summary>
    private const float PromptText = 3.2f, PromptMiddle = 229, PromptKeyGap = 1.5f, PromptGap = 6;

    /// <summary>
    /// The right column's page and its tabs, the user's design (Nov 2, 2026): a loose leaf of parchment the list is on, and a paper tab
    /// sticking up from it for each section, as a stack of loose paper on the scroll with a tab to each section, the stack only
    /// thought of: the user's choice, only the page showing is drawn. The tabs behind are drawn a shade darker, their feet under the pages, with their icons faded, and those the
    /// window hasn't fainter still; the tab showing is in front, joined to the front page by plain paper over where the two meet. As the
    /// lists slide, the tab left goes behind as the new one comes in front, by the slide's eased share; the mouse on a tab behind lightens
    /// it. Each tab has its icon, the inventory's satchel (a shop's Sell), the stats' arm and brain, the skills' sword with a mortar and
    /// flask, and the spells' book with a fireball, with a small caption under it. Beside them, at the list's right edge, the party's gold
    /// after a purse.
    /// </summary>
    private void Tabs()
    {
        var shown = PartyMenu.TabOf(Menu.List);
        var from = Menu.Slide is { } slide ? PartyMenu.TabOf(slide.From) : shown;
        var change = Menu.ListChange;
        // How far each is the tab showing: 1 for it, 0 for the others, and between for the two the lists are sliding between.
        var open = new float[PartyMenu.Tabs.Count];
        for (var i = 0; i < open.Length; i++)
            open[i] = i == shown ? (i == from ? 1 : change) : i == from ? 1 - change : 0;

        for (var i = 0; i < open.Length; i++)
        {
            if (open[i] >= 1)
                continue;
            var has = HasTab(PartyMenu.Tabs[i]);
            var tint = i == _hoveredTab ? HoveredTab : BehindTab;
            _ui.Slip(_canvas, TabRect(i), tint with { A = (byte)(255 * (has ? 1 : MissingTab)) }, UiPainter.TabSheet);
            TabFace(i, has ? ClosedIcon : MissingIcon);
        }
        _ui.Slip(_canvas, PageRect, sheet: UiPainter.TallSheet);
        for (var i = 0; i < open.Length; i++)
        {
            if (open[i] <= 0)
                continue;
            var rect = TabRect(i);
            _ui.Slip(_canvas, rect, White with { A = (byte)(255 * open[i]) }, UiPainter.TabSheet, PageRect.Position.Y + PageEdge);
            JoinTab(rect, open[i]);
            TabFace(i, Mathf.Lerp(ClosedIcon, 1, open[i]));
        }
        // The stick on the tabs: the red square round the tab showing.
        if (OwnTab && Menu.Focus == PartyFocus.Tabs)
        {
            var rect = TabRect(shown);
            Art(Selected, new Rect2(rect.Position - Vector2.One, rect.Size + 2 * Vector2.One));
        }

        // The gold, after a purse, on a sheet of its own after the tabs.
        var font = _ui.Slim.Sized(GoldText);
        var gold = Num(Menu.Party.Gold);
        var box = GoldBox(font.ExactWidth(gold));
        var middle = box.Position.Y + box.Size.Y / 2;
        var purseX = box.Position.X + GoldInset;
        _ui.Slip(_canvas, box, sheet: UiPainter.GoldSheet);
        Small(gold, purseX + PurseSize + PurseGap, middle - font.Middle, PartyMenu.TextColor, font: font);
        Art(Purse, new Rect2(purseX, middle - PurseSize / 2f, PurseSize, PurseSize));
    }

    /// <summary>The gold's sheet, snug round the purse and an amount so wide, its right edge at the page's and its middle level with the tabs'.</summary>
    private static Rect2 GoldBox(float amount)
    {
        var width = GoldInset + PurseSize + PurseGap + amount + GoldInset;
        return new Rect2(PageRect.End.X - width, TabsAt.Y + GoldMiddle - GoldHeight / 2, width, GoldHeight);
    }

    /// <summary>A tab's icon and caption, at a strength.</summary>
    private void TabFace(int tab, float strength)
    {
        var rect = TabRect(tab);
        Art(TabIcons[tab], new Rect2(rect.Position.X + (TabWidth - TabIconSize) / 2f, rect.Position.Y + TabIconTop, TabIconSize, TabIconSize), strength);
        Caption(TabLabel(PartyMenu.Tabs[tab]), rect, PartyMenu.TextColor with { A = (byte)(255 * strength) });
    }

    /// <summary>
    /// The tab showing joined to the front page: paper from just inside the page's art's top, at the page's scale, over where the tab's
    /// foot and the page's torn top meet, across the tab but for its sides, its edges faded over <see cref="JoinFeather"/> so it runs
    /// into the papers round it, at a strength.
    /// </summary>
    private void JoinTab(Rect2 tab, float strength)
    {
        var sheet = UiPainter.TallSheet;
        if (UiImages.Art(sheet) is not { } texture)
            return;
        var paper = UiImages.ArtImage(sheet).Paper;
        var scale = PageRect.Size / paper.Size;
        // Between the tab's side edges, from above the page's top to under its dark edge, which it covers.
        var patch = new Rect2(tab.Position.X + TabEdge, PageRect.Position.Y - JoinFeather - 1, tab.Size.X - 2 * TabEdge,
            JoinFeather + 1 + PageEdge + JoinFeather);
        // From the page's paper below its torn top, so it's the page's own grain and tone there.
        var source = new Rect2(paper.Position.X + (patch.Position.X - PageRect.Position.X) / scale.X, paper.Position.Y + paper.Size.Y * 0.05f,
            patch.Size / scale);
        var size = texture.GetSize();
        float[] xs = [patch.Position.X, patch.Position.X + JoinSideFeather, patch.End.X - JoinSideFeather, patch.End.X];
        float[] ys = [patch.Position.Y, patch.Position.Y + JoinFeather, patch.End.Y - JoinFeather, patch.End.Y];
        Vector2 Uv(float x, float y) => (source.Position + new Vector2(x - patch.Position.X, y - patch.Position.Y) / scale) / size;
        var solid = _ui.Tint(White with { A = (byte)(255 * strength) });
        Color Edge(int i, int j) => i is 0 or 3 || j is 0 or 3 ? solid with { A = 0 } : solid;
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                _canvas.DrawPolygon(
                    [new(xs[i], ys[j]), new(xs[i + 1], ys[j]), new(xs[i + 1], ys[j + 1]), new(xs[i], ys[j + 1])],
                    [Edge(i, j), Edge(i + 1, j), Edge(i + 1, j + 1), Edge(i, j + 1)],
                    [Uv(xs[i], ys[j]), Uv(xs[i + 1], ys[j]), Uv(xs[i + 1], ys[j + 1]), Uv(xs[i], ys[j + 1])], texture);
            }
        }
    }

    /// <summary>How far the join between the tab showing and its page fades out above and below, and at its sides.</summary>
    private const float JoinFeather = 2, JoinSideFeather = 0.4f;

    /// <summary>
    /// How deep the page's dark top edge is, and a tab's side edges, in the frame's pixels: the sheets' own, about 11 of the tall
    /// sheet's 2119 down its paper and 8 of the tab sheet's 288 across. The tab showing stops at the page's edge, and the join covers
    /// the page's edge between the tab's, so the tab and its page share one outline, the user's choice (Nov 2, 2026).
    /// </summary>
    private const float PageEdge = 1, TabEdge = 0.8f;

    /// <summary>
    /// A tab's caption, the slim font at <see cref="UiPainter.CaptionText"/>, its capitals' top 0.5 under its icon, centred, and
    /// squeezed across to stay <see cref="CaptionInset"/> in from the tab's sides.
    /// </summary>
    private void Caption(string text, Rect2 tab, Rgba color)
    {
        var font = _ui.Slim.Sized(UiPainter.CaptionText);
        var width = font.ExactWidth(text);
        var squeeze = Math.Min(1, (TabWidth - 2 * CaptionInset) / width);
        var middle = tab.Position.Y + TabIconTop + TabIconSize + 0.5f + font.Capitals / 2;
        var at = new Vector2(tab.Position.X + (TabWidth - width * squeeze) / 2, middle - font.Middle);
        _canvas.DrawSetTransform(_base + at, 0, new Vector2(squeeze, 1));
        _ui.Text(_canvas, font, text, 0, 0, color);
        _canvas.DrawSetTransform(_base);
    }

    /// <summary>A piece of the game's own UI art (<see cref="UiImages.Art"/>) stretched over a rectangle in the frame, at a strength.</summary>
    private void Art(string name, Rect2 rect, float strength = 1) => Art(name, rect, new Rgba(255, 255, 255, (byte)(255 * strength)));

    /// <summary>A piece of the game's own UI art stretched over a rectangle in the frame, times a colour.</summary>
    private void Art(string name, Rect2 rect, Rgba color)
    {
        if (UiImages.Art(name) is { } texture)
            _canvas.DrawTextureRect(texture, rect, false, _ui.Tint(color));
    }

    /// <summary>A tab's caption: the stats', the skills', the spells', or the inventory's, which beside a shop is what the party sells.</summary>
    private string TabLabel(PartyList tab) => tab switch
    {
        PartyList.Stats => "Stats",
        PartyList.Skills => "Skills",
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

    /// <summary>Text in the slim font from a point in the frame, to a fraction of a pixel, its shadow first if it has one.</summary>
    private void Small(string text, float x, float y, Rgba color, Rgba? shadow = null, UiFont? font = null)
    {
        if (shadow is { } dark)
            SmallAt(text, x + 0.4f, y + 0.4f, dark, font);
        SmallAt(text, x, y, color, font);
    }

    private void SmallAt(string text, float x, float y, Rgba color, UiFont? font)
    {
        _canvas.DrawSetTransform(_base + new Vector2(x, y));
        _ui.Text(_canvas, font ?? _ui.Slim, text, 0, 0, color);
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

    /// <summary>How wide text is in the slim font, unrounded.</summary>
    private float SmallWidth(string text) => _ui.Slim.ExactWidth(text);

    /// <summary>
    /// A skill's popup (<c>WidgetCrafting</c>, <c>WidgetMenuPopup</c>) on the details' parchment at its own
    /// place: its title centred above a list in the slim font, of recipes, tasks, attributes, or members
    /// with their portraits, names and health.
    /// </summary>
    private void Crafting(CraftingMenu crafting)
    {
        var font = _ui.Slim;
        Paper();
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
            var rect = new Rect2(at.X + MenuTween.Value(from.X, to.X, change), at.Y + MenuTween.Value(from.Y, to.Y, change), size, size);
            FramedPortrait(portrait, rect);
            // A hungry member's portrait pulses amber, a starving one's red (HungerDisplay).
            if (HungerDisplay.Tint(Menu.Party.Slots[slot]!.Hunger, Time.GetTicksMsec() / 1000f) is { } tint)
                HungerTint(portrait, rect, tint);
        }
        if (Menu.Previous is { } before)
            _ui.Text(_canvas, NameFont, before.Name, at.X + NameAt.X, at.Y + NameAt.Y, FadingOut(PartyMenu.TextColor, change));
        _ui.Text(_canvas, NameFont, shown.Name, at.X + NameAt.X, at.Y + NameAt.Y, FadingIn(PartyMenu.TextColor, change));
        var icons = Icons(shown);
        var old = Menu.Previous is { } member ? Icons(member) : icons;
        var iconsY = IconsY;
        foreach (var (icon, x, _) in old.Except(icons))
            Icon(icon, at.X + x, at.Y + iconsY, FadingOut(White, change));
        foreach (var (icon, x, name) in icons)
            Icon(icon, at.X + x, at.Y + iconsY, old.Contains((icon, x, name)) ? null : FadingIn(White, change));
    }

    /// <summary>
    /// A portrait framed by the square sheet of parchment, the user's design (Nov 2, 2026): the sheet <see cref="PortraitBorder"/> past
    /// the portrait all round, and on it the portrait cut to the sheet's outline (<see cref="UiImages.CutTo"/>), so a thin rim of the
    /// parchment shows all round and the portrait's corners follow the sheet's torn edges.
    /// </summary>
    private void FramedPortrait(RecordId portrait, Rect2 rect)
    {
        // The original's sits in the frame painted into the tab.
        if (OriginalLook)
        {
            _ui.Image(_canvas, portrait, (int)rect.Position.X, (int)rect.Position.Y, new Vector2I((int)rect.Size.X, (int)rect.Size.Y));
            return;
        }
        _ui.Slip(_canvas, rect.Grow(PortraitBorder), sheet: UiPainter.SquareSheet);
        if (_ui.Images.CutTo(portrait, UiPainter.SquareSheet) is { } cut)
            _canvas.DrawTextureRect(cut, rect, false, _ui.Tint(White));
    }

    /// <summary>The portrait again over itself, in hunger's tint, cut as it is: the pulse over a hungry member's.</summary>
    private void HungerTint(RecordId portrait, Rect2 rect, Rgba tint)
    {
        if (OriginalLook)
            _ui.Image(_canvas, portrait, (int)rect.Position.X, (int)rect.Position.Y, new Vector2I((int)rect.Size.X, (int)rect.Size.Y), tint);
        else if (_ui.Images.CutTo(portrait, UiPainter.SquareSheet) is { } cut)
            _canvas.DrawTextureRect(cut, rect, false, _ui.Tint(tint));
        QueueRedraw();
    }

    /// <summary>Where the portrait so far round the party from the member shown goes, from the frames' corner, and how big it is.</summary>
    private static (Vector2I At, int Size) PortraitPlace(int i) =>
        i == 0 ? (ShownPortrait, ShownSize) : (OtherPortraits[Math.Min(i - 1, OtherPortraits.Length - 1)], OtherSize);

    /// <summary>
    /// A member's aspect's and school's icons after the bar, 4 apart, where they go across, and the names the mouse on one shows, the
    /// user's choice (Nov 2, 2026).
    /// </summary>
    private List<(RecordId Icon, float X, string Name)> Icons(Core.Characters.Character member)
    {
        var icons = new List<(RecordId, float, string)>();
        if (!OriginalLook)
        {
            // The port's: the aspect's right edge under the last small portrait's, the school's before it, the user's choice (Nov 2, 2026).
            var right = (float)OtherPortraits[^1].X + OtherSize;
            if (MenuImages.AspectIcon(member.Aspect) is { } ownAspect)
            {
                right -= IconSize;
                icons.Add((ownAspect, right, $"{member.Aspect} Aspect"));
                right -= IconGap;
            }
            if (MenuImages.SchoolIcon(member.School) is { } ownSchool)
            {
                right -= IconSize;
                icons.Add((ownSchool, right, $"School of {member.School}"));
                right -= IconGap;
            }
            // Hunger's, before them, the user's design (Nov 4, 2026).
            if (HungerDisplay.StatusIcon(member.Hunger) is { } hunger)
                icons.Add((hunger, right - IconSize, HungerDisplay.Tooltip(member.Hunger)!));
            return icons;
        }
        float x = OriginalIconsAt.X;
        if (MenuImages.AspectIcon(member.Aspect) is { } aspect)
        {
            icons.Add((aspect, x, $"{member.Aspect} Aspect"));
            x += _ui.Size(aspect).X + OriginalIconGap;
        }
        if (MenuImages.SchoolIcon(member.School) is { } school)
        {
            icons.Add((school, x, $"School of {member.School}"));
            x += _ui.Size(school).X + OriginalIconGap;
        }
        if (HungerDisplay.StatusIcon(member.Hunger) is { } hungry)
            icons.Add((hungry, x, HungerDisplay.Tooltip(member.Hunger)!));
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
        if (OriginalLook)
        {
            OriginalHealth(HealthPiece.Position, _ui.Main);
            return;
        }
        var shown = Menu.Shown!;
        var change = Menu.NumbersChange;
        var share = Menu.HealthBar(BarPrecision);
        if (Menu.PreviousNumbers is { } before)
            share = MenuTween.Value(before.Bar(BarPrecision), share, change);
        ThreeSlice(BarTrackArt, BarFrame);
        // The red from its left end under the frame to the share across the opening, its ragged end cut off straight.
        if (share > 0)
            ThreeSlice(BarFillArt, BarFrame, BarOpening.Position.X + BarOpening.Size.X * share / BarPrecision);
        ThreeSlice(BarFrameArt, BarFrame);
        if (Menu.PreviousNumbers is { } was)
            Numbers(was, FadingOut(White, change).A);
        Numbers(HeaderNumbers.Of(shown), FadingIn(White, change).A);
    }

    /// <summary>
    /// A piece of the bar's art over a rectangle in three slices: its ends, <see cref="BarEnd"/> of its pixels wide, kept their shape
    /// at the rectangle's height, and its middle stretched between them; cut off at <paramref name="right"/>, as the red is.
    /// </summary>
    private void ThreeSlice(string name, Rect2 rect, float right = float.MaxValue)
    {
        if (UiImages.Art(name) is not { } texture)
            return;
        var texels = texture.GetWidth() / BarArtWidth;
        var end = BarEnd * rect.Size.Y / BarArtHeight;
        float[] dx = [rect.Position.X, rect.Position.X + end, rect.End.X - end, rect.End.X];
        float[] sx = [0, BarEnd, BarArtWidth - BarEnd, BarArtWidth];
        for (var i = 0; i < 3 && dx[i] < right; i++)
        {
            var share = Math.Min(1, (right - dx[i]) / (dx[i + 1] - dx[i]));
            _canvas.DrawTextureRectRegion(texture, new Rect2(dx[i], rect.Position.Y, (dx[i + 1] - dx[i]) * share, rect.Size.Y),
                new Rect2(sx[i] * texels, 0, (sx[i + 1] - sx[i]) * texels * share, texture.GetHeight()), _ui.Tint(White));
        }
    }

    /// <summary>
    /// The original's: the hit points bar, cut to the share left, and the hit points, level and gold on the labels painted into the tab.
    /// As the member shown changes, the bar's width tweens to the new share, and the hit points, their maximum and the level crossfade,
    /// even where they're the same; the gold stays.
    /// </summary>
    private void OriginalHealth(Vector2I at, UiFont font)
    {
        var shown = Menu.Shown!;
        var bar = at + HealthBarAt;
        var change = Menu.NumbersChange;
        var width = Menu.HealthBar(HealthBarSize.X);
        if (Menu.PreviousNumbers is { } before)
        {
            width = MenuTween.Value(before.Bar(HealthBarSize.X), width, change);
            var fading = FadingOut(PartyMenu.TextColor, change);
            Number(font, before.Hp, at + HpAt, fading);
            Number(font, before.MaxHp, at + MaxHpAt, fading);
            Number(font, before.Level, at + LevelAt, fading);
        }
        _ui.ImageTo(_canvas, MenuImages.HealthBar, bar.X, bar.Y, HealthBarSize, bar.X + width);
        var color = FadingIn(PartyMenu.TextColor, change);
        Number(font, shown.Hp, at + HpAt, color);
        Number(font, shown.MaxHp, at + MaxHpAt, color);
        Number(font, shown.Level, at + LevelAt, color);
        Number(font, Menu.Party.Gold, at + GoldAt);
    }

    private void Number(UiFont font, long value, Vector2I at, Rgba? color = null) =>
        _ui.Text(_canvas, font, value.ToString(System.Globalization.CultureInfo.InvariantCulture), at.X, at.Y, color ?? PartyMenu.TextColor);

    /// <summary>The hit points and their maximum centred on the bar, and the level by the name, at an alpha.</summary>
    private void Numbers(HeaderNumbers numbers, byte a)
    {
        var bar = BarOpening;
        var hp = $"{Num(numbers.Hp)} / {Num(numbers.MaxHp)}";
        var slim = _ui.Slim;
        var small = slim.Sized(HitPointsText);
        Small(hp, bar.Position.X + (bar.Size.X - small.ExactWidth(hp)) / 2, bar.Position.Y + bar.Size.Y / 2f - small.Middle, BarText with { A = a },
            BarShadow with { A = (byte)(BarShadow.A * a / 255) }, small);
        var level = $"{LevelLabel} {Num(numbers.Level)}";
        // Level with the name's foot.
        var nameFoot = PortraitPiece.Position.Y + NameAt.Y + NameFont.Middle + NameFont.Capitals / 2;
        Small(level, LevelRight - SmallWidth(level), nameFoot - slim.Capitals / 2 - slim.Middle, PartyMenu.TextColor with { A = a });
    }

    /// <summary>
    /// The silhouette: the member's figure behind (<see cref="Figures"/>), crossfading to the new member's as the header changes
    /// over where it's another, and over it the slots, what's worn or each empty slot's image. As the member changes, or an item is
    /// put on or taken off, the slots the core says crossfade (<see cref="PartyMenu.SlotCrossfades"/>) fade from what they showed.
    /// </summary>
    private void Silhouette(Vector2I at)
    {
        if (OriginalLook)
        {
            OriginalSilhouetteSlots(at);
            return;
        }
        var middle = at.X + SlotOffset(EquipmentSlot.Helmet).X + SlotSize / 2f;
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
            var x = at.X + SlotOffset(slot).X;
            var y = at.Y + SlotOffset(slot).Y;
            Art(SlotBox, new Rect2(x - 1, y - 1, SlotSize + 2, SlotSize + 2));
            if (Menu.PreviousSlots is { } before && Menu.SlotCrossfades(slot))
            {
                SlotArt(before[(int)slot] ?? MenuImages.EmptySlots[(int)slot], x, y, FadingOut(White, change));
                SlotArt(Menu.SlotImage(slot), x, y, FadingIn(White, change));
            }
            else
            {
                SlotArt(Menu.SlotImage(slot), x, y, null);
            }
            if (OwnTab && slot == Menu.SelectedSlot)
                Art(Selected, new Rect2(x - 2, y - 2, SlotSize + 4, SlotSize + 4));
        }
    }

    /// <summary>The original's silhouette: each slot's worn item or its own image, 16 square in the frames painted into the tab.</summary>
    private void OriginalSilhouetteSlots(Vector2I at)
    {
        var change = Menu.SlotsChange;
        var size = new Vector2I(SlotSize, SlotSize);
        foreach (var slot in Enum.GetValues<EquipmentSlot>())
        {
            var (x, y) = (at.X + SlotOffset(slot).X, at.Y + SlotOffset(slot).Y);
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

    /// <summary>
    /// What a slot of the silhouette shows, its corner at (x, y): a worn item's icon 12 square inside the box's inner line, or the slot's
    /// own image of what it takes, filling the box to its inner line, the user's choice (Nov 2, 2026).
    /// </summary>
    private void SlotArt(RecordId image, int x, int y, Rgba? color)
    {
        if (MenuImages.EmptySlots.Contains(image))
        {
            var inner = SlotBoxInner * (SlotSize + 2);
            _ui.Image(_canvas, image, new Rect2(x - 1 + inner, y - 1 + inner, SlotSize + 2 - 2 * inner, SlotSize + 2 - 2 * inner), color);
            return;
        }
        _ui.Image(_canvas, image, x + SlotInset, y + SlotInset, new Vector2I(BoxedSize, BoxedSize), color);
    }

    /// <summary>How far in the slot's box its inner line ends, a share of its size: 50 of its 512.</summary>
    private const float SlotBoxInner = 50f / 512;

    /// <summary>
    /// The equipment card's corners, the user's design (Nov 2, 2026): the member's six stats in the top right in a tooltip's text, each
    /// by its name, in a column, with its total right-aligned, green where gear or a spell raises it and red where one lowers it, and their
    /// experience left to spend in the bottom right, "XP Left".
    /// </summary>
    private void CardStats()
    {
        var font = _ui.Slim.Sized(NameText);
        var right = EquipmentSheet.End.X - CardInset;
        var top = EquipmentSheet.Position.Y + CardInset;
        // The names spelled out in a column, the user's choice (Nov 2, 2026), where they were their first three letters, and the totals
        // right-aligned at the card's edge. What the item under the stick or the mouse would change shows as the total now, an arrow and
        // what it would be, green where it rises and red where it falls, the user's choice (Nov 3, 2026).
        var stats = Menu.Stats.Rows;
        var changes = PreviewChanges();
        float ValueWidth(StatRow r) => changes.TryGetValue(r.Stat, out var by)
            ? font.ExactWidth(Num(r.Value + r.Change)) + 2 * ArrowGap + ArrowWidth + font.ExactWidth(Num(r.Value + r.Change + by))
            : font.ExactWidth(Num(r.Value + r.Change));
        var labelX = right - stats.Select(ValueWidth).DefaultIfEmpty(0).Max() - CardLabelGap
            - stats.Select(r => font.ExactWidth(r.Name)).DefaultIfEmpty(0).Max();
        foreach (var row in stats)
        {
            var color = row.Change > 0 ? MenuDetailsBuilder.Gain : row.Change < 0 ? MenuDetailsBuilder.Loss : PartyMenu.TextColor;
            var value = Num(row.Value + row.Change);
            var middle = top + CardPitch / 2;
            Small(row.Name, labelX, middle - font.Middle, PartyMenu.TextColor, font: font);
            if (changes.TryGetValue(row.Stat, out var by))
            {
                var after = Num(row.Value + row.Change + by);
                var x = right - font.ExactWidth(after);
                Small(after, x, middle - font.Middle, by > 0 ? MenuDetailsBuilder.Gain : MenuDetailsBuilder.Loss, font: font);
                x -= ArrowGap + ArrowWidth;
                var ink = _ui.Tint(PartyMenu.TextColor);
                _canvas.DrawPolyline([new(x, middle - ArrowWidth), new(x + ArrowWidth, middle), new(x, middle + ArrowWidth)], ink, ArrowLine, true);
                x -= ArrowGap + font.ExactWidth(value);
                Small(value, x, middle - font.Middle, color, font: font);
            }
            else
            {
                Small(value, right - font.ExactWidth(value), middle - font.Middle, color, font: font);
            }
            top += CardPitch;
        }
        if (Menu.Experience is not { } exp)
            return;
        // The labels in a column, the values right-aligned at the card's edge.
        (string Label, string Value)[] lines = [(XpAvailable, exp.Remaining), (TotalXp, exp.Total), (NextLevel, exp.NextLevel)];
        var xpLabelX = right - lines.Max(l => font.ExactWidth(l.Value)) - CardLabelGap - lines.Max(l => font.ExactWidth(l.Label));
        var lineMiddle = EquipmentSheet.End.Y - CardInset - CardPitch * (lines.Length - 0.5f);
        foreach (var (label, value) in lines)
        {
            Small(label, xpLabelX, lineMiddle - font.Middle, PartyMenu.TextColor, font: font);
            Small(value, right - font.ExactWidth(value), lineMiddle - font.Middle, PartyMenu.TextColor, font: font);
            lineMiddle += CardPitch;
        }
    }

    /// <summary>How far in from the equipment card's paper its corners' text is, and how far apart the stats are.</summary>
    private const float CardInset = 5, CardPitch = 4.5f;

    /// <summary>The arrow between a stat's total and what an item would make it: its width, its gaps either side and its line.</summary>
    private const float ArrowWidth = 1.1f, ArrowGap = 1, ArrowLine = 0.4f;

    /// <summary>
    /// What putting on the item under the mouse, or with a key or a controller the stick, would change of the member's stats, into the
    /// slot being chosen for (<see cref="PartyMenu.StatChanges"/>); none for what's worn or isn't put on.
    /// </summary>
    private IReadOnlyDictionary<Core.Characters.Stat, int> PreviewChanges()
    {
        if (!OwnTab || OriginalLook || Menu.HasPopup)
            return NoChanges;
        if (!Menu.Pointing)
            return Menu.CursorStatChanges;
        return Menu.List == PartyList.Inventory && Menu.Grid && _hoveredCell is { } cell
            && Menu.Inventory.Rows.ElementAtOrDefault(cell) is { Wearer: null } row
            ? Menu.StatChanges(row.Item, Menu.Picking)
            : NoChanges;
    }

    private static readonly Dictionary<Core.Characters.Stat, int> NoChanges = [];

    /// <summary>
    /// The experience lines' labels on the card, and the first on the Stats page in the port's look, the user's choice (Nov 2, 2026): the
    /// experience left to spend is "XP Available", where the original says "XP Remaining"; and the gap between the card's labels and values.
    /// </summary>
    private const string XpAvailable = "XP Available", TotalXp = "Total XP", NextLevel = "Next Level";

    private const float CardLabelGap = 2;

    /// <summary>
    /// The grid's foot, the user's design (Nov 2, 2026): "Total Inventory" and how many items the party carries on the left, and on the
    /// right, where the quick toggle for what's worn was, the grid's filter and order, the user's choice (Nov 3, 2026): "Show All" and
    /// "Sort Type", each a label and its choice in red ink, which a click or A with the stick on it steps on (<see cref="PartyMenu.Filter"/>,
    /// <see cref="PartyMenu.Sort"/>), the one the stick is on outlined in red; or, with a slot's fitting items, which slot.
    /// </summary>
    private void InventoryFoot(GridLayout grid)
    {
        var font = _ui.Slim.Sized(FootText);
        var b = grid.Bounds;
        var middle = b.Top + grid.Lines * grid.Cell + FootMiddle;
        var total = $"{TotalInventory} {Num(Menu.CarriedCount)}";
        Small(total, b.Left, middle - font.Middle, PartyMenu.TextColor, font: font);
        if (OriginalLook)
            return;
        // The grid showing what fits a slot says which, where the filter and order are.
        if (Menu.Fitting is { } slot)
        {
            var fits = $"{FitsLabel} {PartyMenu.SlotName(slot)}";
            Small(fits, b.Left + grid.Columns * grid.Cell - font.ExactWidth(fits), middle - font.Middle, UiPainter.RedInk, font: font);
            return;
        }
        var controls = FootControls(font, grid);
        for (var i = 0; i < controls.Count; i++)
        {
            var (rect, label, value) = controls[i];
            Small(label, rect.Position.X, middle - font.Middle, PartyMenu.TextColor, font: font);
            Small(value, rect.Position.X + font.ExactWidth(label) + FootLabelGap, middle - font.Middle, UiPainter.RedInk, font: font);
            if (Menu.Focus == PartyFocus.Foot && Menu.FootChoice == i)
                _canvas.DrawRect(rect.Grow(1), _ui.Tint(UiPainter.RedInk), false, FootOutline);
        }
    }

    /// <summary>The filter's and the order's places under the grid, right-aligned at its edge, each its label and its choice.</summary>
    private List<(Rect2 Rect, string Label, string Value)> FootControls(UiFont font, GridLayout grid)
    {
        var b = grid.Bounds;
        var middle = b.Top + grid.Lines * grid.Cell + FootMiddle;
        (string Label, string Value)[] parts = [(ShowLabel, Menu.Filter.ToString()), (SortLabel, Menu.Sort.ToString())];
        float right = b.Left + grid.Columns * grid.Cell;
        var controls = new List<(Rect2, string, string)>();
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var width = font.ExactWidth(parts[i].Label) + FootLabelGap + font.ExactWidth(parts[i].Value);
            controls.Insert(0, (new Rect2(right - width, middle - FootRow / 2, width, FootRow), parts[i].Label, parts[i].Value));
            right -= width + FootControlGap;
        }
        return controls;
    }

    /// <summary>The control under the grid at a point in the frame, the filter 0 and the order 1, or null.</summary>
    private int? FootControlAt(Vector2 at)
    {
        var controls = FootControls(_ui.Slim.Sized(FootText), PartyMenu.InventoryGrid);
        for (var i = 0; i < controls.Count; i++)
        {
            if (controls[i].Rect.Grow(1).HasPoint(at))
                return i;
        }
        return null;
    }

    /// <summary>
    /// The grid's foot: its text's capitals this tall, its middle this far under the grid's last line, the row this tall to click, the gap
    /// after a control's label and between the controls, and the outline round the one the stick is on.
    /// </summary>
    private const float FootText = 3.4f, FootMiddle = 8.5f, FootRow = 7, FootLabelGap = 1.5f, FootControlGap = 6, FootOutline = 0.4f;

    private const string TotalInventory = "Total Inventory", FitsLabel = "Fits the", ShowLabel = "Show", SortLabel = "Sort",
        NothingFits = "Nothing to put here", NothingHere = "Nothing here";

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
            Figure.Woman => "silhouette_woman",
            Figure.LargeMan => "silhouette_large_man",
            _ => "silhouette_man",
        };
        if (UiImages.Art(name) is not { } texture)
            return;
        var width = FigureHeight * texture.GetWidth() / texture.GetHeight();
        Art(name, new Rect2(middle - width / 2, FigureTop, width, FigureHeight), strength);
    }

    /// <summary>The red square round the cursor's cell, the user's art (ui_square_select_red), drawn a pixel past the cell.</summary>
    private const string Selected = "ui_square_select_red";

    /// <summary>
    /// How big a worn item's wearer's portrait is in its cell's top left corner, how tall the count's capitals are in its bottom right,
    /// and how far in from the cell's corners they sit.
    /// </summary>
    private const int WearerSize = 6;

    /// <summary>The colour a worn item's box is drawn in, a little grey, as it's in use, the user's choice (Nov 2, 2026).</summary>
    private static readonly Rgba WornBox = new(178, 178, 186, 255);

    private const float CountText = 3.6f, CellInset = 2.5f;

    /// <summary>How much of a grid's cell its item's icon takes across, inside its box's inner line: 12 of the original's 18.</summary>
    private const float GridIcon = 12f / 18;

    /// <summary>How an unusable item's icon is drawn, faded; and a mark's size and its lines' thickness.</summary>
    private static readonly Rgba DimIcon = new(255, 255, 255, 110);

    private const float MarkSize = 3.2f, MarkLine = 0.55f;

    /// <summary>A cell's mark from its top left: a red cross for what can't be used, a green arrow up for what would be better.</summary>
    private void MarkArt(ItemMark mark, float x, float y)
    {
        const float s = MarkSize;
        if (mark == ItemMark.Unusable)
        {
            var red = _ui.Tint(UiPainter.RedInk);
            _canvas.DrawLine(new Vector2(x, y), new Vector2(x + s, y + s), red, MarkLine, true);
            _canvas.DrawLine(new Vector2(x + s, y), new Vector2(x, y + s), red, MarkLine, true);
            return;
        }
        var green = _ui.Tint(MenuDetailsBuilder.Gain);
        _canvas.DrawColoredPolygon([new Vector2(x + s / 2, y), new Vector2(x + s, y + s * 0.55f), new Vector2(x, y + s * 0.55f)], green);
        _canvas.DrawLine(new Vector2(x + s / 2, y + s * 0.5f), new Vector2(x + s / 2, y + s), green, MarkLine * 1.6f, true);
    }

    /// <summary>
    /// The inventory's grid (<see cref="PartyMenu.InventoryGrid"/>), the user's design (Nov 2, 2026): a cell for each of its rows in
    /// view, a stack or a worn item, the user's box with the item's icon in it, its count in the bottom right corner where there's more
    /// than one, and on a worn item the wearer's portrait small in the top left; the cursor's cell in the user's red square; and the
    /// arrows where lines are hidden above or below. The items' names show on a slip over the cursor's cell (<see cref="Tooltip"/>).
    /// </summary>
    private void Grid(GridLayout grid)
    {
        var list = Menu.Inventory;
        var count = _ui.Slim.Sized(CountText);
        // The grid of what fits a slot or what the filter shows, with nothing in it, says so.
        if (list.Rows.Count == 0 && !OriginalLook)
        {
            var font = _ui.Slim.Sized(FootText);
            var b = grid.Bounds;
            var nothing = Menu.Fitting is not null ? NothingFits : NothingHere;
            Small(nothing, b.Left + (grid.Columns * grid.Cell - font.ExactWidth(nothing)) / 2, b.Top + grid.Cell - font.Middle,
                PartyMenu.TextColor, font: font);
        }
        var last = Math.Min(list.Rows.Count, list.First + grid.Visible);
        for (var i = list.First; i < last; i++)
        {
            var (x, y) = grid.CellAt(i - list.First);
            var row = list.Rows[i];
            Art(SlotBox, new Rect2(x + 0.5f, y + 0.5f, grid.Cell - 1, grid.Cell - 1), row.Wearer is null ? White : WornBox);
            var icon = grid.Cell * GridIcon;
            // What the member shown can't use is dimmed with a red cross, and what would be better than what they wear has a green arrow,
            // in the top right corner.
            var mark = OriginalLook ? ItemMark.None : Menu.Mark(row);
            _ui.Image(_canvas, Menu.ItemIcon(row.Item), new Rect2(x + (grid.Cell - icon) / 2, y + (grid.Cell - icon) / 2, icon, icon),
                mark == ItemMark.Unusable ? DimIcon : null);
            if (mark != ItemMark.None)
                MarkArt(mark, x + grid.Cell - CellInset - MarkSize, y + CellInset);
            if (row.Wearer is { } wearer && Menu.Party.Slots[wearer] is { } who && Menu.Portrait(who.Entity) is { } portrait)
            {
                // In the bottom left, the count being in the bottom right.
                var wearerY = y + grid.Cell - 2 - WearerSize;
                _canvas.DrawRect(new Rect2(x + 1.5f, wearerY - 0.5f, WearerSize + 1, WearerSize + 1), _ui.Tint(UiPainter.Ink));
                _ui.Image(_canvas, portrait, x + 2, wearerY, new Vector2I(WearerSize, WearerSize));
            }
            if (row.Quantity > 1)
            {
                var text = Num(row.Quantity);
                var foot = y + grid.Cell - CellInset;
                Small(text, x + grid.Cell - CellInset - count.ExactWidth(text), foot - count.Capitals / 2 - count.Middle, BarText,
                    BarShadow, count);
            }
            if (i == list.Cursor && Menu.Focus == PartyFocus.List && Menu.CursorShows)
                Art(Selected, new Rect2(x - 1, y - 1, grid.Cell + 2, grid.Cell + 2));
        }
        ScrollBar(grid, list);
    }

    /// <summary>
    /// A list's arrows, which say it has rows hidden, in the original's look; in the port's, the original's arrows gone, the user's
    /// choice (Nov 2, 2026), a thin scroll bar beside its right edge as the grid's, while it has rows hidden.
    /// </summary>
    private void ListArrows<TRow>(MenuList<TRow> list, ListLayout layout)
    {
        if (OriginalLook)
        {
            _ui.Arrows(_canvas, list, layout);
            return;
        }
        if (list.Rows.Count <= list.Visible)
            return;
        var b = layout.Bounds;
        var (x, top, height) = (b.Right + 1.5f, (float)b.Top, (float)list.Visible * layout.RowHeight);
        _canvas.DrawRect(new Rect2(x, top, 0.75f, height), _ui.Tint(UiPainter.Ink with { A = 70 }));
        var count = (float)list.Rows.Count;
        _canvas.DrawRect(new Rect2(x - 0.25f, top + height * list.First / count, 1.25f, height * list.Visible / count), _ui.Tint(UiPainter.Ink));
    }

    /// <summary>
    /// The grid's scroll bar beside its right edge, while it has lines hidden: a faint ink line its height, and on it a darker one as
    /// long as the share of the lines in view and as far down as they are.
    /// </summary>
    private void ScrollBar<TRow>(GridLayout grid, MenuList<TRow> list)
    {
        var lines = (list.Rows.Count + grid.Columns - 1) / grid.Columns;
        if (lines <= grid.Lines)
            return;
        var b = grid.Bounds;
        var (x, top, height) = (b.Left + grid.Columns * grid.Cell + 1.5f, (float)b.Top, (float)grid.Lines * grid.Cell);
        _canvas.DrawRect(new Rect2(x, top, 0.75f, height), _ui.Tint(UiPainter.Ink with { A = 70 }));
        var first = (float)list.First / grid.Columns;
        _canvas.DrawRect(new Rect2(x - 0.25f, top + height * first / lines, 1.25f, height * grid.Lines / lines), _ui.Tint(UiPainter.Ink));
    }

    /// <summary>
    /// The name of what the mouse is on, else of what's selected, on a slip over it: the member's aspect or school by its icon, in any
    /// window with no popup up; or on the tab itself, its lists still and no popup up, an item worn in a slot of the silhouette, or one in
    /// the inventory grid's cells. With the mouse in use only what it's on has one, the user's choice (Nov 3, 2026); with a key or a
    /// controller the grid's is the cursor's (<see cref="PartyMenu.Pointing"/>).
    /// </summary>
    private void Tooltip()
    {
        if (_hoveredIcon is { } hovered && !Window.HasPopup && Menu.Shown is { } shown && Icons(shown).ElementAtOrDefault(hovered) is { Name: not null } icon)
        {
            var rect = IconRect(icon);
            NameSlip(icon.Name, rect.Position.X + rect.Size.X / 2, rect.Position.Y - 1);
            return;
        }
        if (!OwnTab || Menu.Slide is not null || Window.HasPopup || Menu.Crafting is not null)
            return;
        var grid = Menu.List == PartyList.Inventory && Menu.Grid;
        var pointing = Menu.Pointing;
        // A slot shows what's worn in it, or, empty, what it takes.
        if ((pointing ? _hoveredSlot : _hoveredSlot ?? Menu.SelectedSlot) is { } slot && (Menu.Worn(slot) is not null || !OriginalLook))
        {
            var rect = SlotRect(slot);
            if (Menu.Worn(slot) is { } worn)
                CardSlip(Menu.Card(new InventoryRow(worn, 1, Menu.Member)), rect.Position.X + rect.Size.X / 2, rect.Position.Y - 1);
            else
                NameSlip(PartyMenu.SlotName(slot), rect.Position.X + rect.Size.X / 2, rect.Position.Y - 1);
        }
        else if (grid && (pointing ? _hoveredCell : Menu.Focus == PartyFocus.List ? Menu.Inventory.Cursor : null) is { } cell
            && Menu.Inventory.Rows.ElementAtOrDefault(cell) is { } row
            && cell >= Menu.Inventory.First && cell < Menu.Inventory.First + _gridLayout.Visible)
        {
            var (x, y) = _gridLayout.CellAt(cell - Menu.Inventory.First);
            CardSlip(Menu.Card(row), x + _gridLayout.Cell / 2f, y - 1, y + _gridLayout.Cell + 1);
        }
    }

    /// <summary>
    /// An item's card on a slip over it, the user's choice (Nov 3, 2026), where the slip had only its name (<see cref="PartyMenu.Card"/>):
    /// the name, then in smaller text what it is, why the member can't use it in red ink, or what it would be better than in green, as
    /// it has them; over the item, or under it where over would reach the tabs.
    /// </summary>
    private void CardSlip(ItemCard card, float middle, float foot, float? below = null)
    {
        var name = _ui.Slim.Sized(NameText);
        var small = _ui.Slim.Sized(CardText);
        var lines = new List<(string Text, Rgba Color)>();
        if (card.About is { } about)
            lines.Add((about, PartyMenu.TextColor));
        if (card.Warning is { } warning)
            lines.Add((warning, UiPainter.RedInk));
        if (card.Better is { } better)
            lines.Add((better, MenuDetailsBuilder.Gain));
        var width = lines.Select(l => small.ExactWidth(l.Text)).Append(name.ExactWidth(card.Name)).Max();
        var first = SlipEnds + name.Capitals / 2;
        var height = first + (lines.Count > 0 ? CardGap + (lines.Count - 1) * CardLinePitch + small.Capitals / 2 : name.Capitals / 2) + SlipEnds;
        var left = Math.Clamp(middle - width / 2 - SlipSide, 4, FrameWidth - 4 - width - 2 * SlipSide);
        var top = below is { } under && foot - height < PageRect.Position.Y ? under : foot - height;
        _ui.Slip(_canvas, new Rect2(left, top, width + 2 * SlipSide, height), sheet: UiPainter.TooltipSheet);
        Small(card.Name, left + SlipSide, top + first - name.Middle, UiPainter.Ink, font: name);
        var y = top + first + CardGap;
        foreach (var (text, color) in lines)
        {
            Small(text, left + SlipSide, y - small.Middle, color, font: small);
            y += CardLinePitch;
        }
    }

    /// <summary>A card's lines under the name: their capitals' height, the first's middle this far under the name's, and the rest this far apart.</summary>
    private const float CardText = 3, CardGap = 4.6f, CardLinePitch = 4;

    /// <summary>
    /// How tall a name's capitals are on its slip, half the menus' text, the user's choice (Nov 2, 2026), and how far the slip reaches
    /// past it at the sides and above and below it.
    /// </summary>
    private const float NameText = UiPainter.StandardText / 2 * 1.25f, SlipSide = 3, SlipEnds = 2;

    /// <summary>The actiontip's choices: their capitals this tall, this far apart, and the slip this far past them at the sides and ends.</summary>
    private const float TipText = 3, TipPitch = 6.5f, TipSide = 4, TipEnds = 2.5f;

    /// <summary>
    /// The actiontip: its choices on a slip, the cursor's in red ink, beside where the right click opened it, or with the stick's beside its
    /// item, the worn slot or the grid's cell, kept inside the frame.
    /// </summary>
    private void ActionTipBox(ActionTip tip)
    {
        var font = _ui.Slim.Sized(TipText);
        var rect = TipRect(tip);
        _ui.Slip(_canvas, rect);
        for (var i = 0; i < tip.Choices.Rows.Count; i++)
        {
            var middle = rect.Position.Y + TipEnds + TipPitch * (i + 0.5f);
            Small(ActionTip.Label(tip.Choices.Rows[i]), rect.Position.X + TipSide, middle - font.Middle,
                i == tip.Choices.Cursor ? UiPainter.RedInk : UiPainter.Ink, font: font);
        }
    }

    /// <summary>Where the actiontip's slip is in the frame.</summary>
    private Rect2 TipRect(ActionTip tip)
    {
        var font = _ui.Slim.Sized(TipText);
        var width = tip.Choices.Rows.Select(c => font.ExactWidth(ActionTip.Label(c))).DefaultIfEmpty(0).Max() + 2 * TipSide;
        var height = tip.Choices.Rows.Count * TipPitch + 2 * TipEnds;
        Vector2 at;
        if (_tipAt is { } opened && opened.Tip == tip)
            at = opened.At + new Vector2(1, 1);
        else if (Menu.SelectedSlot is { } slot)
            at = new Vector2(SlotRect(slot).End.X + 1, SlotRect(slot).Position.Y);
        else
        {
            var (x, y) = _gridLayout.CellAt(Menu.Inventory.Cursor - Menu.Inventory.First);
            at = new Vector2(x + _gridLayout.Cell + 1, y);
        }
        return new Rect2(Math.Clamp(at.X, 4, FrameWidth - 4 - width), Math.Clamp(at.Y, 4, FrameHeight - 4 - height), width, height);
    }

    /// <summary>The actiontip's choice under a point in the frame, or null.</summary>
    private int? TipChoiceAt(ActionTip tip, Vector2 at)
    {
        var rect = TipRect(tip);
        if (!rect.HasPoint(at))
            return null;
        return Math.Clamp((int)((at.Y - rect.Position.Y - TipEnds) / TipPitch), 0, tip.Choices.Rows.Count - 1);
    }

    /// <summary>
    /// A name on a little slip of paper, centred across <paramref name="middle"/>, its foot at <paramref name="foot"/>, kept inside the
    /// frame; or with its top at <paramref name="below"/> where over would reach the tabs above the pages.
    /// </summary>
    private void NameSlip(string name, float middle, float foot, float? below = null)
    {
        var font = _ui.Slim.Sized(NameText);
        var width = font.ExactWidth(name);
        var height = font.Capitals + 2 * SlipEnds;
        var left = Math.Clamp(middle - width / 2 - SlipSide, 4, FrameWidth - 4 - width - 2 * SlipSide);
        var top = below is { } under && foot - height < PageRect.Position.Y ? under : foot - height;
        var paper = new Rect2(left, top, width + 2 * SlipSide, height);
        _ui.Slip(_canvas, paper, sheet: UiPainter.TooltipSheet);
        Small(name, left + SlipSide, paper.Position.Y + height / 2 - font.Middle, UiPainter.Ink, font: font);
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
    /// given. The stats have the experience lines under their rows (<see cref="StatRows"/>).
    /// </summary>
    private void List(int shift, UiFont font, Rgba? cursor = null, PartyList? which = null)
    {
        var shown = which ?? Menu.List;
        var layout = Menu.LayoutOf(shown).Shifted(shift);
        var b = layout.Bounds;
        if (shown == Menu.List)
            _listLayout = layout;
        var highlight = cursor ?? Menu.HighlightNow;
        if (OriginalLook)
        {
            // The original's list has its title over it, 70 into it and 20 above.
            var title = shown switch
            {
                PartyList.Inventory => (Window as BarterMenu)?.PartyTitle ?? MenuImages.InventoryTitle,
                PartyList.Stats => MenuImages.StatsTitle,
                PartyList.Skills => MenuImages.SkillsTitle,
                _ => MenuImages.SpellsTitle,
            };
            _ui.Image(_canvas, title, b.Left + 70 - _ui.Size(title).X / 2, b.Top - 20);
        }
        switch (shown)
        {
            case PartyList.Inventory when Menu.Grid:
                var grid = PartyMenu.InventoryGrid.Shifted(shift);
                if (shown == Menu.List)
                    _gridLayout = grid;
                Grid(grid);
                if (OwnTab)
                    InventoryFoot(grid);
                break;
            case PartyList.Inventory:
                Items(Menu.Inventory, layout, font, highlight);
                break;
            case PartyList.Stats:
                StatRows(layout, font, highlight);
                break;
            case PartyList.Skills:
                SkillRows(Menu.Skills, layout, font, highlight);
                break;
            default:
                SpellRows(Menu.Spells, layout, font, highlight);
                break;
        }
    }

    /// <summary>The stats' rows (<c>SMStatItem</c>), 11 apart, and 4 under them the experience lines.</summary>
    private void StatRows(ListLayout layout, UiFont font, Rgba highlight)
    {
        _ui.List(Menu.Stats, layout, PartyMenu.TextColor, highlight, (row, top, c) => StatRowAt(row, layout, top, font, c));
        ListArrows(Menu.Stats, layout);
        ExperienceLinesAt(layout, layout.Bounds.Top + Menu.ExperienceAt, font, _ => true);
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
        // The original's "XP Remaining:" is "XP Available:" in the port's look.
        var remaining = OriginalLook ? exp.RemainingLabel : XpAvailable + ":";
        var valueX = b.Left + font.Width(remaining) + 4;
        foreach (var (label, value) in new[] { (remaining, exp.Remaining), (exp.TotalLabel, exp.Total), (exp.NextLevelLabel, exp.NextLevel) })
        {
            if (shows(top))
            {
                _ui.Text(_canvas, font, label, b.Left, top, b, PartyMenu.TextColor);
                _ui.Text(_canvas, font, value, valueX, top, b, PartyMenu.TextColor);
            }
            top += PartyMenu.ExperiencePitch;
        }
    }

    /// <summary>
    /// A skills list's rows (<c>WidgetSkillInfo</c>), 13 apart, with their arrows: the icon, the name 12 after
    /// it, the level, and the difference gear makes when it isn't 0.
    /// </summary>
    private void SkillRows(MenuList<SkillRow> list, ListLayout layout, UiFont font, Rgba highlight)
    {
        _ui.List(list, layout, PartyMenu.TextColor, highlight, (row, top, c) => SkillRowAt(row, layout, top, font, c));
        ListArrows(list, layout);
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
        ListArrows(list, layout);
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
            _ui.Image(_canvas, Menu.ItemIcon(row.Item), x + 13, top, new Vector2I(12, 12));
            if (row.Quantity != 0)
                _ui.Text(_canvas, font, Num(row.Quantity), x + Columns.Quantity, top, b, PartyMenu.TextColor);
            OneLine(font, row.Item.Name, x + (row.Quantity != 0 ? Columns.Name : Columns.NameNoQuantity), top, b.Right, c);
        });
        ListArrows(list, layout);
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
            {
                _ui.Text(_canvas, font, title, area.Left + size.X + 2, y - font.LineHeight, PartyMenu.TextColor);
                // A heading's red flourish under it, centred across the details; the original has none.
                if (!OriginalLook)
                {
                    var height = FlourishWidth * 115 / 655f;
                    Art(Flourish, new Rect2(area.Left + (area.Right - area.Left - FlourishWidth) / 2, y - 1, FlourishWidth, height));
                    y += FlourishRoom;
                }
            }
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
