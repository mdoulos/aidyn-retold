using AidynRetold.Core.Camp;
using AidynRetold.Core.Characters;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Game;

/// <summary>A control painted by whoever owns it: the camp band, cut to its rectangle, and the debug marks over the whole camp screen.</summary>
public partial class PaintedControl : Control
{
    public Action<CanvasItem>? Painter { get; set; }

    public override void _Draw() => Painter?.Invoke(this);
}

/// <summary>
/// Draws the camp screen (<see cref="CampMenu"/>, the remade game's) on a <see cref="UiScreen"/>'s canvas: the campsite's picture
/// as wide as the screen and panned between the fire centred and its foot (<see cref="CampMenu.Pan"/>); the party's figures at their
/// slots, each pose's image scaled to the campsite's one canonical height by the slot's perspective and the figure's stature, mirrored
/// where the arrangement says, lit by the biome's ambient light and by the fire through its normal map (<see cref="FigureShader"/>),
/// those behind the fire under it (<see cref="PartyBehind"/>) and those in front over it (<see cref="PartyFront"/>), or a marker where
/// a member has no figure yet; the debug overlay of the fire, the pot, the slots and the safe area while the campsite's anchors
/// are placeholders (<see cref="CampsiteRecord.Calibrated"/>) or asked for; and the band: the campsite's safe area as a strip of the
/// pause menu's parchment between two rollers of its 3D scroll (<see cref="PauseScrollView"/>, drawn smaller and its rollers out at the
/// band's ends), which unrolls and rolls up as the pause menu's does from its Party tab, with the five tabs down its left on slips of
/// the gold's parchment and the tab showing beside them, the user's design (Nov 7, 2026). The band is a control of its own, cut at the
/// rollers (<see cref="Band"/>), and the debug marks another over everything (<see cref="Top"/>). The mouse points at a tab, a row or
/// Rest and a click presses A on it, as in the pause menu. The night's talk shows in trek's box, which is drawn over this layer.
/// </summary>
public partial class CampView : Control
{
    /// <summary>The band's colours on the parchment: the journal's brown, the Party tab's red for the cursor, and the bar's cream over its red.</summary>
    private static readonly Rgba Text = new(98, 70, 60, 255), Dim = new(170, 152, 130, 255), Cursor = new(150, 40, 35, 255),
        Note = new(130, 105, 85, 255), Said = new(70, 45, 40, 255), Overlay = new(120, 255, 160, 255), Warn = new(255, 170, 90, 255),
        Button = new(255, 236, 130, 255), BarText = new(250, 242, 226, 255), BarShadow = new(40, 20, 14, 170), SlipDim = new(228, 218, 202, 255);

    private static readonly RecordId Parchment = RecordId.Parse("acre:image/parchment"), Thirds = RecordId.Parse("acre:image/debug_thirds");

    /// <summary>The tabs' icons, the user's, in <see cref="CampMenu.Tabs"/>' order: a tent, food, a mortar and flask, crossed arms and a breastplate.</summary>
    private static readonly string[] TabIcons = ["ui_camp", "ui_food", "ui_potions", "ui_weapons", "ui_armor"];

    /// <summary>
    /// How much of the scroll's length its parchment spans: lines 6 to 234 of the frame's 240 (<see cref="ParchmentSwap"/>), so the band's
    /// parchment, this share of the scroll's length, lies within its rollers' ends.
    /// </summary>
    private const float PaperShare = 228f / 240;

    /// <summary>How tall the scroll's control is, of its length, for the rollers' ends past the parchment.</summary>
    private const float ScrollRoom = 1.5f;

    // The band's layout, in the band's pixels from its top left corner: the tabs' slips down the left, spread from the band's top to
    // its foot, each with its icon and caption, and the tab showing joined to the content's paper beside them, the wide sheet, the
    // user's design (Nov 7, 2026). The sides keep clear of the rollers, which lie over the band's ends.
    // The paper is the tabs' height: the top tab's top at its top and the bottom tab's foot at its foot, the tabs as tall as the gaps
    // between them leave (the user's design, Nov 7, 2026). Every tab's slip runs on under the paper; the tab showing's over it, fading.
    // The tabs are as tall as the paper's height left them with gaps of TabGap, and set TabSpacing apart, so the column stands a little
    // inside the paper's top and foot and the paper hides all of each slip that runs on under it, its torn corners too (Nov 7, 2026).
    private const float PaperTop = 2.5f, Side = 12, TabWidth = 42, TabGap = 1.2f, TabSpacing = 0.4f, TabUnder = 8, TabIconSize = 9, TabIconLeft = 6,
        PointerLeft = 1.5f, PaperInsetX = 3, PaperInsetY = 3;

    /// <summary>
    /// How far the join between the tab showing and the paper fades out at its ends and along its sides, how deep the paper's torn
    /// left edge and a tab's top and bottom edges are, as the Party tab has them for its page.
    /// </summary>
    private const float JoinFeather = 2, JoinSideFeather = 0.4f, PaperEdge = 1, TabEdge = 0.8f;

    // The Camp tab: the name and level over each portrait, 30 square as the HUD's, the portraits 2 apart, the bar under each and its
    // status badges under that, and the foot row with the location, the inventory and Rest.
    // Each portrait framed as the Party tab's are, on the small square sheet with its border, as large as five fit with a gap between
    // their frames that keeps them and their shadows apart; the bar and the badges as wide as the frame.
    private const float PortraitSize = 30, PortraitBorder = 0.85f, FrameGap = 2.5f, NameRow = 6, BarHeight = 7, BadgeHeight = 6, BadgePitch = 6.75f,
        BadgeIcon = 5, BadgeIconLeft = 1.5f, FootHeight = 10;

    /// <summary>The tab the mouse is over, which is lit, and opens only when it's clicked, as the pause menu's tabs do.</summary>
    private int? _hoveredTab;

    /// <summary>The tabs' slips behind and the one under the mouse, the Party tab's tints.</summary>
    private static readonly Rgba HoveredTab = new(242, 236, 226, 255);

    /// <summary>
    /// How many frames the band is drawn invisible as the camp opens, so its textures load then rather than as it unrolls, where the
    /// loading stalled the scroll for a tenth of a second (Nov 7, 2026).
    /// </summary>
    private const int WarmFrames = 2;
    private int _warmFrames;
    private bool _rendering;

    private readonly UiPainter _ui;
    private readonly PluginSet _plugins;
    private readonly PauseScrollView? _scroll;

    /// <summary>What the mouse can land on: a tab, a recipe or Rest.</summary>
    private enum Target { Tab, Recipe, Rest, Bar, Badge, RestOption }

    /// <summary>How far apart the ways to rest are in their list, and how far in from its ends and its left side.</summary>
    private const float RestPitch = 7.5f, RestListEnds = 2.5f, RestListSide = 9, RestTimeRow = 5.5f;

    /// <summary>What the hover over a bar or a badge says, and where the bar or badge is on this control, or none.</summary>
    private (string Text, Rect2 Area)? _tip;

    /// <summary>A hover's slip: how far its text is from its sides and its ends, the Party tab's.</summary>
    private const float TipSide = 3, TipEnds = 2;

    /// <summary>Where each tab, recipe and Rest was drawn, in the band's pixels, for the mouse: its area, what it is and its index.</summary>
    private readonly List<(Rect2 Area, Target Target, int Index)> _hits = [];

    /// <summary>The band, in this control's pixels: the campsite's safe area over the picture panned to its foot.</summary>
    private Rect2 _band;

    /// <summary>The transform the band's content is drawn at on its control, for text placed to a fraction of a pixel.</summary>
    private Vector2 _base;

    /// <param name="scroll">The band's scroll, the pause menu's actor again, or null without one: then the band has no rollers.</param>
    public CampView(PluginSet plugins, PauseScrollView? scroll)
    {
        _plugins = plugins;
        _scroll = scroll;
        _ui = new UiPainter(plugins) { TextSize = UiPainter.StandardText };
        Name = "Camp screen";
        MouseFilter = MouseFilterEnum.Pass;
        Band.Painter = PaintBand;
        Top.Painter = PaintTop;
        Glow.Painter = PaintGlow;
        FireBehind.Painter = PaintFireBehind;
        Fire.Painter = PaintFire;
        Front.Painter = PaintFront;
        FrontLight.Painter = PaintGlow;
        FrontLight.SetAnchorsPreset(LayoutPreset.FullRect);
        Front.AddChild(FrontLight);
        PartyBehind.Painter = PaintPartyBehind;
        PartyBehind.Material = _figureLight;
        Campfire.Painter = PaintCampfire;
        PartyFront.Painter = PaintPartyFront;
        PartyFront.Material = _figureLight;
        PartyLight.Painter = PaintGlow;
        PartyLight.SetAnchorsPreset(LayoutPreset.FullRect);
        PartyFront.AddChild(PartyLight);
    }

    /// <summary>The band's control, cut at the rollers, placed by <see cref="_Draw"/>: the layer puts it over this control and under the scroll.</summary>
    public PaintedControl Band { get; } = new() { Name = "Camp band", ClipContents = true, MouseFilter = MouseFilterEnum.Ignore, Visible = false };

    /// <summary>The debug marks over everything: the buttons and the rule-of-thirds lines; the layer puts it over the scroll.</summary>
    public PaintedControl Top { get; } = new() { Name = "Camp marks", MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>
    /// The flames behind the campfire's pot and the pot over them (<see cref="PaintFireBehind"/>), under the firelight, so the pot is lit
    /// as the rest of the campfire is; the layer puts it over this control.
    /// </summary>
    public PaintedControl FireBehind { get; } = new() { Name = "Camp fire behind the pot", MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>The firelight over the picture, added to it (<see cref="PaintGlow"/>); the layer puts it over the flames behind the pot.</summary>
    public PaintedControl Glow { get; } = new()
    {
        Name = "Camp firelight",
        MouseFilter = MouseFilterEnum.Ignore,
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
    };

    /// <summary>The smoke, the flames in front of the pot and the embers (<see cref="PaintFire"/>); the layer puts it over the firelight.</summary>
    public PaintedControl Fire { get; } = new() { Name = "Camp fire", MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>
    /// The stones in front of the fire (<see cref="PaintFront"/>), over the flames and the embers, hiding the flames' feet; the layer
    /// puts it over <see cref="Fire"/>. Its child, <see cref="FrontLight"/>, is cut to the stones' shape (ClipChildren).
    /// </summary>
    public PaintedControl Front { get; } = new()
    {
        Name = "Camp fire's front stones",
        MouseFilter = MouseFilterEnum.Ignore,
        ClipChildren = ClipChildrenMode.AndDraw,
    };

    /// <summary>
    /// The firelight again on the front stones only, added to them as <see cref="Glow"/> is to the stones behind, which it's under, so
    /// they're lit alike (<see cref="PaintGlow"/>, cut to <see cref="Front"/>'s stones).
    /// </summary>
    public PaintedControl FrontLight { get; } = new()
    {
        Name = "Camp fire's front stones' firelight",
        MouseFilter = MouseFilterEnum.Ignore,
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
    };

    /// <summary>
    /// The figures behind the fire (<see cref="PaintPartyBehind"/>), lit (<see cref="FigureShader"/>); the layer puts it over this control,
    /// which draws the picture, and under <see cref="Campfire"/>.
    /// </summary>
    public PaintedControl PartyBehind { get; } = new() { Name = "Camp figures behind the fire", MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>The campfire's art over the figures behind it, the vignette over both and the debug overlay (<see cref="PaintCampfire"/>).</summary>
    public PaintedControl Campfire { get; } = new() { Name = "Camp campfire", MouseFilter = MouseFilterEnum.Ignore };

    /// <summary>
    /// The figures in front of the fire (<see cref="PaintPartyFront"/>), lit as those behind it are, over the flames, the embers and the
    /// front stones, which they stand before; the layer puts it over <see cref="Front"/>. Its child, <see cref="PartyLight"/>, is cut to
    /// the figures' shapes (ClipChildren).
    /// </summary>
    public PaintedControl PartyFront { get; } = new()
    {
        Name = "Camp figures in front of the fire",
        MouseFilter = MouseFilterEnum.Ignore,
        ClipChildren = ClipChildrenMode.AndDraw,
    };

    /// <summary>
    /// The firelight again on the figures in front of the fire only, added to them as <see cref="Glow"/> is to those behind it, which it's
    /// over, so they're lit alike (<see cref="PaintGlow"/>, cut to <see cref="PartyFront"/>'s figures).
    /// </summary>
    public PaintedControl PartyLight { get; } = new()
    {
        Name = "Camp figures' firelight",
        MouseFilter = MouseFilterEnum.Ignore,
        Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
    };

    /// <summary>Where the picture was drawn last, and its campsite, which the fire is placed on.</summary>
    private Rect2 _fit;
    private CampsiteRecord? _site;

    /// <summary>The camp screen showing, or null.</summary>
    public CampMenu? Camp { get; set; }

    /// <summary>Whether the debug overlay is drawn whatever the campsite says: F3, as trek's overlay.</summary>
    public bool ShowOverlay { get; set; }

    /// <summary>A click on a row, which the view has put the cursor on: the button to press.</summary>
    public Action<MenuAction>? Click { get; set; }

    /// <summary>
    /// Whether the debug buttons are drawn, at the top right: a debug build's. Poses sits or stands the figures afresh
    /// (<see cref="PosesRequested"/>), Talk starts a camp talk (<see cref="TalkRequested"/>), and Debug shows the technical text and marks
    /// that won't be in the game (<see cref="ShowOverlay"/>), off until it's clicked.
    /// </summary>
    public bool DebugButtons { get; set; }

    /// <summary>The Talk button was clicked: a camp talk is to show.</summary>
    public Action? TalkRequested { get; set; }

    /// <summary>The Poses button was clicked: the figures are to sit or stand afresh, the user's (Nov 7, 2026).</summary>
    public Action? PosesRequested { get; set; }

    /// <summary>Where the Poses, Talk and Debug buttons were drawn, or empty.</summary>
    private Rect2 _posesButton, _talkButton, _debugButton;

    /// <summary>
    /// A slot's choice in the debug overlay was made: the slot, and the figure to put there standing or seated, or none to empty it, the
    /// user's (Nov 7, 2026).
    /// </summary>
    public Action<string, RecordId?, bool>? SlotChosen { get; set; }

    /// <summary>What a slot's list offers: nobody, or a figure standing or seated, and whether the figure has that pose there.</summary>
    private readonly record struct SlotChoice(string Text, RecordId? Figure, bool Seated, bool Possible);

    /// <summary>Where each slot's choice was drawn, the slot whose list is open, and where its list's choices were drawn.</summary>
    private readonly List<(Rect2 Area, string Slot)> _slotBoxes = [];
    private string? _openSlot;
    private readonly List<(Rect2 Area, SlotChoice Choice)> _slotChoices = [];

    /// <summary>The slots' choices under the buttons: their left edge from the screen's right, their rows' height and their label's width.</summary>
    private const float ChoicesLeft = 107, ChoiceRow = 9, ChoiceLabel = 38;

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Camp is not { Open: true } camp)
            return;
        if (e is InputEventMouseMotion overList && camp.RestListOpen)
        {
            if (HitEntry(overList.Position) is { Target: Target.RestOption } way && camp.PointRestOption(way.Index))
                QueueRedraw();
            return;
        }
        if (e is InputEventMouseMotion motion)
        {
            var hit = Hit(motion.Position);
            if (hit is var (target, index) ? Point(camp, target, index) : Unhover())
                QueueRedraw();
            // The bar's and the badges' hovers, as the Party tab's bar's and icons' are.
            var tip = HitEntry(motion.Position) is { Target: Target.Bar or Target.Badge } entry ? TipOf(camp, entry) : null;
            if (tip != _tip)
            {
                _tip = tip;
                Top.QueueRedraw();
            }
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true } button)
            return;
        if (button.ButtonIndex == MouseButton.Right)
        {
            Click?.Invoke(MenuAction.Back);
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && ClickSlotChoices(button.Position))
        {
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && _talkButton.HasArea() && _talkButton.HasPoint(button.Position))
        {
            TalkRequested?.Invoke();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && _posesButton.HasArea() && _posesButton.HasPoint(button.Position))
        {
            PosesRequested?.Invoke();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && _debugButton.HasArea() && _debugButton.HasPoint(button.Position))
        {
            ShowOverlay = !ShowOverlay;
            QueueRedraw();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && camp.RestListOpen)
        {
            // A way to rest is pointed at and pressed as A; a click anywhere else shuts the list, as B does.
            if (HitEntry(button.Position) is { Target: Target.RestOption } way)
            {
                camp.PointRestOption(way.Index);
                Click?.Invoke(MenuAction.Accept);
            }
            else
            {
                camp.CloseRestList();
            }
            QueueRedraw();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && Hit(button.Position) is var (target, index))
        {
            // A tab opens on a click, as the pause menu's do; a recipe or Rest is pointed at and pressed as A; a bar or a badge only
            // has its hover.
            if (target == Target.Tab)
            {
                camp.Choose(CampMenu.Tabs[index]);
            }
            else if (target is not (Target.Bar or Target.Badge))
            {
                Point(camp, target, index);
                Click?.Invoke(MenuAction.Accept);
            }
            QueueRedraw();
            AcceptEvent();
        }
    }

    /// <summary>What's under a point of this control, with where it is on this control.</summary>
    private (Rect2 Area, Target Target, int Index)? HitEntry(Vector2 at)
    {
        var inBand = at - _band.Position;
        foreach (var (area, target, index) in Enumerable.Reverse(_hits))
        {
            if (area.HasPoint(inBand))
                return (new Rect2(area.Position + _band.Position, area.Size), target, index);
        }
        return null;
    }

    /// <summary>What a bar's or a badge's hover says, and where it is: a bar's index is its member's, a badge's its member's times four and its own.</summary>
    private static (string Text, Rect2 Area)? TipOf(CampMenu camp, (Rect2 Area, Target Target, int Index) entry)
    {
        var members = camp.Members;
        if (entry.Target == Target.Bar && members.ElementAtOrDefault(entry.Index) is { } member)
            return (CampMenu.BarTooltip(member), entry.Area);
        if (entry.Target == Target.Badge && members.ElementAtOrDefault(entry.Index / 4) is { } badged
            && CampMenu.Badges(badged).ElementAtOrDefault(entry.Index % 4) is { Tooltip: { } text })
            return (text, entry.Area);
        return null;
    }

    /// <summary>What's under a point of this control, the hits being in the band's pixels.</summary>
    private (Target Target, int Index)? Hit(Vector2 at)
    {
        // The last drawn first, so what's on top, the ways to rest over the badges, takes the mouse.
        var inBand = at - _band.Position;
        foreach (var (area, target, index) in Enumerable.Reverse(_hits))
        {
            if (area.HasPoint(inBand))
                return (target, index);
        }
        return null;
    }

    /// <summary>
    /// The mouse over something: a tab lit, opening only on a click, as the pause menu's tabs do; or the recipe or Rest pointed at in the
    /// content; whether anything changed.
    /// </summary>
    private bool Point(CampMenu camp, Target target, int index)
    {
        int? hovered = target == Target.Tab ? index : null;
        var changed = hovered != _hoveredTab;
        _hoveredTab = hovered;
        switch (target)
        {
            case Target.Tab:
                return changed;
            case Target.Recipe:
                return camp.PointRecipe(index) | changed;
            case Target.Bar or Target.Badge:
                return changed;
            default:
                // A rest button: Quick Rest's index is 0, Long Rest's 1.
                return camp.PointRestButton(index == 0 ? CampRestButton.Quick : CampRestButton.Long) | changed;
        }
    }

    /// <summary>The mouse off the tabs: none lit; whether one was.</summary>
    private bool Unhover()
    {
        var was = _hoveredTab is not null;
        _hoveredTab = null;
        return was;
    }

    /// <summary>A camp has opened: the band draws invisible for a frame or two, so what it draws with is loaded before it unrolls.</summary>
    public void Warm() => _warmFrames = WarmFrames;

    /// <summary>The fire moves every frame, at the frame's rate, not the ticks'.</summary>
    public override void _Process(double delta)
    {
        if (Camp is null)
            return;
        Glow.QueueRedraw();
        FireBehind.QueueRedraw();
        Fire.QueueRedraw();
        Front.QueueRedraw();
        FrontLight.QueueRedraw();
        PartyLight.QueueRedraw();
        LightFigures();
    }

    public override void _Draw()
    {
        _hits.Clear();
        if (Camp is not { } camp)
        {
            Render(false);
            HideBand();
            Top.QueueRedraw();
            return;
        }
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);
        var site = camp.Site;
        var fit = Fit(site, size, camp.Pan);
        (_fit, _site) = (fit, site);
        if (site is not null && _ui.Images.Get(site.Image) is { } picture)
            DrawTextureRect(picture, fit, false);
        // The figures behind the fire, the campfire and the vignette over them, and the figures in front of the fire over its layers
        // (CampsiteRecord.FireDepth), each on its own control, drawn as the picture pans.
        PartyBehind.QueueRedraw();
        Campfire.QueueRedraw();
        PartyFront.QueueRedraw();
        LightFigures();
        _band = BandRect(site, fit, size);
        Render(true);
        LayBand(camp);
        Top.QueueRedraw();
    }

    /// <summary>Whether the scroll's viewports render: while a camp shows, so the rollers are drawn the frame the band begins to unroll.</summary>
    private void Render(bool on)
    {
        if (_scroll is null || _rendering == on)
            return;
        _rendering = on;
        _scroll.Rendering = on;
    }

    /// <summary>The band's rectangle: the campsite's safe area over the picture as it's fitted now, or a strip low on the screen with none.</summary>
    private static Rect2 BandRect(CampsiteRecord? site, Rect2 fit, Vector2 size)
    {
        if (site?.SafeUi is { } safe)
            return new Rect2(At(fit, safe.X, safe.Y), new Vector2(fit.Size.X * safe.Width, fit.Size.Y * safe.Height));
        return new Rect2((size.X - 234) / 2, size.Y * 0.583f, 234, 85);
    }

    /// <summary>
    /// Lays the band out as its scroll stands: the scroll's control over the band, its length the band's height over the parchment's
    /// share of it and its rollers' middles at the band's ends when open; the band's control cut between the rollers' middles and to
    /// their ends, and faded with the scroll, and in as the rollers stand up across the screen as the pause menu's parchment is
    /// (<see cref="ScrollParchmentView"/>). Nothing while the band is away.
    /// </summary>
    private void LayBand(CampMenu camp)
    {
        if (!camp.BandShown || camp.Scroll is not { } scroll)
        {
            HideBand();
            return;
        }
        var clip = _band.Grow(2);
        var alpha = scroll.Alpha;
        var paper = 1f;
        if (_scroll is { } view)
        {
            var lines = _band.Size.Y / PaperShare;
            var height = lines * ScrollRoom;
            view.Lines = lines;
            view.OpenHalfWidth = _band.Size.X / 2;
            view.Position = new Vector2(0, _band.GetCenter().Y - height / 2);
            view.Size = new Vector2(Size.X, height);
            view.Visible = true;
            var (left, right, top, bottom, upright) = view.Arrange();
            // The rollers fade in from nothing as they stand up across the screen, opaque by the time they stand, the user's choice for the
            // band alone (Nov 7, 2026); the pause menu's scroll keeps its own rise.
            var standing = Math.Clamp(upright / ScrollParchmentView.UprightTo, 0, 1);
            alpha *= standing * standing * (3 - 2 * standing);
            view.Alpha = alpha;
            if (!float.IsInfinity(left) && !float.IsInfinity(right))
            {
                var cutLeft = Math.Max(clip.Position.X, Mathf.Round(left + view.Position.X));
                var cutRight = Math.Min(clip.End.X, Mathf.Round(right + view.Position.X));
                var cutTop = Math.Max(clip.Position.Y, Mathf.Round(top + view.Position.Y));
                var cutBottom = Math.Min(clip.End.Y, Mathf.Round(bottom + view.Position.Y));
                clip = new Rect2(cutLeft, cutTop, Math.Max(0, cutRight - cutLeft), Math.Max(0, cutBottom - cutTop));
            }
            var t = Math.Clamp((upright - ScrollParchmentView.UprightFrom) / (ScrollParchmentView.UprightTo - ScrollParchmentView.UprightFrom), 0, 1);
            paper = t * t * (3 - 2 * t);
        }
        Band.Position = clip.Position;
        Band.Size = clip.Size;
        Band.Visible = clip.HasArea() && alpha * paper > 0;
        Band.Modulate = new Color(1, 1, 1, alpha * paper);
        Band.QueueRedraw();
    }

    /// <summary>The band away: not drawn, but for the warm-up's invisible frames as a camp opens.</summary>
    private void HideBand()
    {
        if (_scroll is { } view)
            view.Visible = false;
        if (_warmFrames > 0 && Camp is not null)
        {
            _warmFrames--;
            Band.Position = _band.Position;
            Band.Size = _band.Size;
            Band.Modulate = new Color(1, 1, 1, 0);
            Band.Visible = true;
            Band.QueueRedraw();
            return;
        }
        Band.Visible = false;
    }

    /// <summary>The debug marks over everything: the buttons at the top right, and the rule-of-thirds lines over the whole screen, as the user drew them, with the overlay.</summary>
    private void PaintTop(CanvasItem canvas)
    {
        _posesButton = _talkButton = _debugButton = default;
        if (Camp is not { Talking: false } camp)
            return;
        if (DebugButtons && camp.Choice == CampChoice.None && !camp.TalkPending)
        {
            _debugButton = DrawButton(canvas, new Rect2(Size.X - 36, 3, 32, 11), "Debug", ShowOverlay);
            _talkButton = DrawButton(canvas, new Rect2(Size.X - 70, 3, 30, 11), "Talk", false);
            if (camp.Arrangement is not null)
                _posesButton = DrawButton(canvas, new Rect2(Size.X - 107, 3, 33, 11), "Poses", false);
        }
        if (ShowOverlay && _ui.Images.Get(Thirds) is { } thirds)
            canvas.DrawTextureRect(thirds, new Rect2(Vector2.Zero, Size), false);
        _slotBoxes.Clear();
        _slotChoices.Clear();
        if (DebugButtons && ShowOverlay && camp.Choice == CampChoice.None && !camp.TalkPending && camp.Arrangement is { } arrangement)
            DrawSlotChoices(canvas, arrangement);
        else
            _openSlot = null;
        if (camp is { Open: true, Tab: CampTab.Camp } && _tip is var (text, area))
            Tip(canvas, text, area);
    }

    /// <summary>
    /// The debug overlay's choice for each of the campsite's slots, under the buttons at the top right, the user's (Nov 7, 2026): its name
    /// and who's there, standing or sitting, or none; a click opens its list of nobody and each figure standing and sitting, those the
    /// figure has no such pose for at the slot dimmed, and a click on one puts it there (<see cref="SlotChosen"/>).
    /// </summary>
    private void DrawSlotChoices(CanvasItem canvas, CampArrangement arrangement)
    {
        var fine = Font(UiPainter.FineText);
        var yellow = new Color(Button.R / 255f, Button.G / 255f, Button.B / 255f);
        var left = Size.X - ChoicesLeft;
        var boxWidth = ChoicesLeft - 4 - ChoiceLabel;
        var top = 17f;
        Rect2? open = null;
        foreach (var slot in arrangement.Site.Slots)
        {
            var placed = arrangement.Placements.FirstOrDefault(p => p.Slot.Id == slot.Id);
            var now = placed is null ? "none"
                : placed.Figure is null ? $"{MemberName(placed.Member)}, no figure"
                : $"{MemberName(placed.Figure.Entity)} {(CampPoses.IsSeated(placed.Pose) ? "sitting" : "standing")}";
            canvas.DrawRect(new Rect2(left, top, ChoiceLabel - 1, ChoiceRow - 1), new Color(0, 0, 0, 0.6f));
            _ui.Text(canvas, fine, Plain(slot.Id), (int)left + 2, (int)top + 1, Button);
            var box = new Rect2(left + ChoiceLabel, top, boxWidth, ChoiceRow - 1);
            canvas.DrawRect(box, slot.Id == _openSlot ? yellow with { A = 0.35f } : new Color(0, 0, 0, 0.6f));
            canvas.DrawRect(box, yellow with { A = 0.9f }, false, 0.4f);
            _ui.Text(canvas, fine, now, (int)box.Position.X + 2, (int)top + 1, Button);
            _ui.Text(canvas, fine, "v", (int)(box.End.X - 5), (int)top + 1, Button);
            _slotBoxes.Add((box, slot.Id));
            if (slot.Id == _openSlot)
                open = box;
            top += ChoiceRow;
        }
        if (open is not { } under || arrangement.Site.Slot(_openSlot!) is not { } chosen)
            return;
        // The open list, over the rows under its slot.
        List<SlotChoice> choices = [new("none", null, false, true)];
        foreach (var figure in _plugins.All<CampFigureRecord>(RecordTypes.CampFigure).OrderBy(f => f.Id.ToString(), StringComparer.Ordinal))
        {
            var name = MemberName(figure.Entity);
            choices.Add(new($"{name} standing", figure.Id, false, CampArranger.Pick(figure, chosen, seated: false) is not null));
            choices.Add(new($"{name} sitting", figure.Id, true, CampArranger.Pick(figure, chosen, seated: true) is not null));
        }
        var y = under.End.Y + 1;
        var list = new Rect2(under.Position.X, y, under.Size.X, choices.Count * ChoiceRow);
        canvas.DrawRect(list, new Color(0.05f, 0.04f, 0.03f));
        canvas.DrawRect(list, yellow with { A = 0.9f }, false, 0.4f);
        foreach (var choice in choices)
        {
            var row = new Rect2(list.Position.X, y, list.Size.X, ChoiceRow);
            _ui.Text(canvas, fine, choice.Text, (int)row.Position.X + 2, (int)y + 1, choice.Possible ? Button : Dim);
            _slotChoices.Add((row, choice));
            y += ChoiceRow;
        }
    }

    /// <summary>
    /// A click on the slots' choices: in the open list, its choice made if the figure can take it, and the list shut; on a slot's box, its
    /// list opened, or shut if it was; elsewhere, an open list shut. Whether the click was theirs.
    /// </summary>
    private bool ClickSlotChoices(Vector2 at)
    {
        if (_openSlot is { } slot)
        {
            foreach (var (area, choice) in _slotChoices)
            {
                if (!area.HasPoint(at))
                    continue;
                if (choice.Possible)
                {
                    _openSlot = null;
                    SlotChosen?.Invoke(slot, choice.Figure, choice.Seated);
                }
                Top.QueueRedraw();
                return true;
            }
        }
        foreach (var (area, id) in _slotBoxes)
        {
            if (!area.HasPoint(at))
                continue;
            _openSlot = _openSlot == id ? null : id;
            Top.QueueRedraw();
            return true;
        }
        if (_openSlot is null)
            return false;
        _openSlot = null;
        Top.QueueRedraw();
        return true;
    }

    /// <summary>
    /// A hover's slip over what it's for, as the Party tab's names are on a little slip of the tooltip's parchment: its text in the slim
    /// face at the Fine size, centred over the bar or badge, its foot just above it, kept inside the screen.
    /// </summary>
    private void Tip(CanvasItem canvas, string text, Rect2 over)
    {
        var font = _ui.Slim.Sized(UiPainter.FineText);
        var width = font.ExactWidth(text);
        var height = font.Capitals + 2 * TipEnds;
        var left = Math.Clamp(over.GetCenter().X - width / 2 - TipSide, 4, Size.X - 4 - width - 2 * TipSide);
        var paper = new Rect2(left, over.Position.Y - 1 - height, width + 2 * TipSide, height);
        if (UiPainter.Original)
            canvas.DrawRect(paper, new Color(0, 0, 0, 0.75f));
        else
            _ui.Slip(canvas, paper, sheet: UiPainter.TooltipSheet);
        canvas.DrawSetTransform(new Vector2(left + TipSide, paper.Position.Y + height / 2 - font.Middle));
        _ui.Text(canvas, font, text, 0, 0, UiPainter.Original ? new Rgba(255, 255, 255, 255) : UiPainter.Ink);
        canvas.DrawSetTransform(Vector2.Zero);
    }

    private Rect2 DrawButton(CanvasItem canvas, Rect2 area, string text, bool lit)
    {
        var yellow = new Color(Button.R / 255f, Button.G / 255f, Button.B / 255f);
        canvas.DrawRect(area, lit ? yellow with { A = 0.35f } : new Color(0, 0, 0, 0.6f));
        canvas.DrawRect(area, yellow with { A = 0.9f }, false, 1);
        _ui.Text(canvas, Font(UiPainter.SmallText), text, (int)area.Position.X + 4, (int)area.Position.Y + 2, Button);
        return area;
    }

    /// <summary>
    /// Where the picture is drawn, never squeezed: as wide as the screen, and if that leaves it taller than the screen, panned
    /// (<see cref="CampMenu.Pan"/>) between the fire at the screen's centre and its foot at the screen's; else as large as fits, whole, centred.
    /// </summary>
    private static Rect2 Fit(CampsiteRecord? site, Vector2 size, float pan)
    {
        if (site is null || site.Width <= 0 || site.Height <= 0)
            return new Rect2(Vector2.Zero, size);
        var scale = size.X / site.Width;
        var drawn = new Vector2(site.Width * scale, site.Height * scale);
        if (drawn.Y <= size.Y)
        {
            scale = Mathf.Min(scale, size.Y / site.Height);
            drawn = new Vector2(site.Width * scale, site.Height * scale);
            return new Rect2((size - drawn) / 2, drawn);
        }
        var foot = size.Y - drawn.Y;
        var centred = Mathf.Clamp(size.Y / 2 - site.Fire.Y * drawn.Y, foot, 0);
        return new Rect2(0, Mathf.Lerp(centred, foot, pan), drawn.X, drawn.Y);
    }

    /// <summary>A point given as shares of the picture, in the canvas.</summary>
    private static Vector2 At(Rect2 fit, float x, float y) => fit.Position + new Vector2(fit.Size.X * x, fit.Size.Y * y);

    /// <summary>
    /// Where the campfire's art is drawn, the user's (Nov 7, 2026): its fire's middle on the picture's fire, as wide as the record says in
    /// the picture's pixels, its height kept to its shape, scaled with the picture as it's drawn. Its pot is drawn the same size on the
    /// same spot.
    /// </summary>
    private static Rect2 CampfireRect(CampfireArt campfire, Texture2D image, CampsiteRecord site, Rect2 fit)
    {
        var pixel = fit.Size.X / site.Width;
        var size = new Vector2(campfire.Width, campfire.Width * image.GetHeight() / image.GetWidth()) * pixel;
        var at = At(fit, site.Fire.X, site.Fire.Y) - new Vector2(campfire.Anchor.X * size.X, campfire.Anchor.Y * size.Y);
        return new Rect2(at, size);
    }

    /// <summary>The campsite and where its picture is drawn now, as this control draws it, or null with none.</summary>
    private (CampsiteRecord Site, Rect2 Fit)? Scene() =>
        Camp is { Site: { Width: > 0, Height: > 0 } site } camp ? (site, Fit(site, Size, camp.Pan)) : null;

    /// <summary>The figures behind the fire, under the campfire.</summary>
    private void PaintPartyBehind(CanvasItem canvas)
    {
        if (Camp is { Arrangement: { } arrangement } && Scene() is var (site, fit))
            DrawParty(canvas, arrangement, site, fit, front: false);
    }

    /// <summary>The campfire's art, over the figures behind the fire; the vignette as painted over both; and the debug overlay.</summary>
    private void PaintCampfire(CanvasItem canvas)
    {
        if (Camp is not { } camp || Scene() is not var (site, fit))
            return;
        if (site.Campfire is { } campfire && _ui.Images.Get(campfire.Image) is { } campfireImage)
            canvas.DrawTextureRect(campfireImage, CampfireRect(campfire, campfireImage, site, fit), false);
        // The vignette as painted: a transparent image with its dark at its own alpha, laid over the picture pixel for pixel.
        if (site.Vignette is { } vignette && _ui.Images.Get(vignette) is { } shade)
            canvas.DrawTextureRect(shade, fit, false);
        if (ShowOverlay && !camp.Talking)
            DrawOverlay(canvas, camp, site, fit);
    }

    /// <summary>The figures in front of the fire, over its layers, with the firelight added to them by their child (<see cref="PartyLight"/>).</summary>
    private void PaintPartyFront(CanvasItem canvas)
    {
        if (Camp is { Arrangement: { } arrangement } && Scene() is var (site, fit))
            DrawParty(canvas, arrangement, site, fit, front: true);
    }

    /// <summary>The party's figures behind the fire or in front of it, by their slots' depths against the fire's.</summary>
    private void DrawParty(CanvasItem canvas, CampArrangement arrangement, CampsiteRecord site, Rect2 fit, bool front)
    {
        // A picture pixel's size on the canvas: a figure's image is scaled to the campsite's canonical height, times the slot's and its stature.
        var pixel = fit.Size.X / site.Width;
        foreach (var placement in arrangement.InDrawOrder.Where(p => p.Slot.Depth > CampsiteRecord.FireDepth == front))
        {
            var foot = At(fit, placement.Slot.X, placement.Slot.Y);
            if (placement.Image is { } pose && Lit(pose) is { } image)
            {
                var scale = pixel * placement.PictureScale(site);
                var width = image.GetWidth() * scale;
                var height = image.GetHeight() * scale;
                var anchorX = placement.Mirrored ? image.GetWidth() - pose.AnchorX : pose.AnchorX;
                var left = foot.X - anchorX * scale;
                var top = foot.Y - pose.AnchorY * scale;
                // A negative width draws the image mirrored in the same place (UiPainter.Image).
                var rect = new Rect2(left, top, placement.Mirrored ? -width : width, height);
                canvas.DrawTextureRect(image, rect, false, LightData(site, placement.Slot, foot, pixel));
            }
            else if (ShowOverlay)
            {
                // No figure to draw yet: nothing in the game, and with the overlay a dot on the anchor with who stands there.
                canvas.DrawCircle(foot, 2, Colors.White);
                _ui.Text(canvas, Font(UiPainter.FineText), $"{MemberName(placement.Member)}, {Plain(placement.Pose)}{(placement.Mirrored ? " (mirrored)" : "")}", (int)foot.X - 10, (int)foot.Y - 12, Button);
            }
        }
    }

    // The fire, the user's choice (Nov 7, 2026): over the campfire's art (CampsiteRecord.Campfire) and its painted flames, flames from the
    // user's sheet of them (art-flames-flicker, cut into camp_flame_00 to 13, the first seven tall and the rest short), some behind its
    // pot and some in front; smoke from the sheet of plumes, faint; embers from the sheet of them (art-flames-embers, cut into
    // camp_ember_00 to 15, the first eight round and the rest streaks); and the firelight. Sizes and offsets are in the picture's pixels,
    // from the fire's anchor (CampsiteRecord.Fire), and scaled with it as it's drawn.

    private static readonly RecordId[] Flames = [.. Enumerable.Range(0, 14).Select(i => RecordId.Parse($"acre:image/camp_flame_{i:00}"))];
    private static readonly RecordId[] Smokes = [.. Enumerable.Range(0, 12).Select(i => RecordId.Parse($"acre:image/camp_smoke_{i:00}"))];
    private static readonly RecordId[] Embers = [.. Enumerable.Range(0, 16).Select(i => RecordId.Parse($"acre:image/camp_ember_{i:00}"))];

    /// <summary>
    /// The flames across the pit, back to front: each one's offset from the fire's anchor, its height, whether it's drawn from the tall
    /// flames or the short, how many shapes a second it changes through, and whether it burns in front of the campfire's pot, over its
    /// pot layer, or behind it, under the pot, the user's design (Nov 7, 2026). The campfire's art has no flames of its own, only its
    /// glowing logs, so these spread across them: low at the ends, taller toward the middle.
    /// </summary>
    private static readonly (float X, float Height, bool Tall, float Rate, bool Over)[] FlameLayout =
    [
        (-27, 18, false, 7.9f, true), (27, 19, false, 8.3f, true), (-4, 40, true, 6.8f, false), (4, 41, true, 7.3f, false),
        (-9, 38, true, 6.5f, false), (0, 44, true, 6f, false), (-17, 28, false, 7.5f, true), (17, 29, false, 8.5f, true),
        (9, 35, true, 7f, true),
    ];

    /// <summary>
    /// How far under the fire's anchor the flames' feet are: on the campfire's logs, as its art is drawn; how wide a flame is drawn of its
    /// sprite's shape; and how opaque.
    /// </summary>
    private const float FlameDrop = 14, FlameWidth = 1.05f, FlameAlpha = 0.92f;

    /// <summary>How far over the campsite's pot's bottom the smoke starts, with no campfire art to give the pot's rim: its rim.</summary>
    private const float SmokeFromPot = 30;

    /// <summary>
    /// The smoke: how many plumes rise at once, how long each takes, how far it rises and how tall it grows, how faint it is, and how far
    /// a plume starts from the rim's middle and wanders either side as it rises.
    /// </summary>
    private const int SmokePlumes = 4;
    private const float SmokeSeconds = 9, SmokeRise = 190, SmokeFrom = 80, SmokeTo = 160, SmokeAlpha = 0.24f, SmokeSpread = 4, SmokeWander = 10;

    /// <summary>
    /// The wind, the user's design (Nov 7, 2026): every 10 to 20 seconds it turns to the left, to still or to the right, chosen afresh
    /// each time, and eases round over a few seconds, a gentle gust in it. The flames lean with it, and the smoke and the embers rise
    /// slanting toward its side. Each turn falls in a slot of its own <see cref="WindSlot"/> seconds long, somewhere in its first
    /// <see cref="WindSlack"/>, so turns come 10 to 20 seconds apart; a turn takes <see cref="WindTurn"/> seconds; and the wind leans
    /// what it moves <see cref="WindLean"/> degrees.
    /// </summary>
    private const float WindSlot = 15, WindSlack = 5, WindTurn = 3, WindLean = 20;

    /// <summary>
    /// The wind's lean at a moment, in radians: to the right over 0, to the left under, 0 when it's still. Each camp's winds are its own,
    /// by its seed.
    /// </summary>
    private float Wind(float seconds)
    {
        var salt = (int)((Camp?.Seed ?? 0) % 9973) * 3;
        var slot = (int)MathF.Floor(seconds / WindSlot);
        if (seconds < Turn(slot))
            slot--;
        var side = Mathf.Lerp(Side(slot - 1), Side(slot), Smooth(Math.Clamp((seconds - Turn(slot)) / WindTurn, 0, 1)));
        var gust = 1 + 0.15f * Noise(seconds * 0.4f, 403 + salt);
        return Mathf.DegToRad(WindLean) * side * gust;

        float Turn(int slot) => slot * WindSlot + WindSlack * Hash(slot, 401 + salt);
        float Side(int slot) => MathF.Floor(Hash(slot, 402 + salt) * 3) - 1;
    }

    /// <summary>The firelight's soft round, made once: white in the middle fading out to nothing at the edge.</summary>
    private static readonly GradientTexture2D LightRound = new()
    {
        Width = 256,
        Height = 256,
        Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f),
        FillTo = new Vector2(1, 0.5f),
        Gradient = new Gradient
        {
            Offsets = [0, 0.35f, 1],
            Colors = [Colors.White, new Color(1, 1, 1, 0.45f), new Color(1, 1, 1, 0)],
        },
    };

    /// <summary>The firelight's colour: the warm orange of the painted fire's light on the ground.</summary>
    private static readonly Color FireLight = new(1f, 0.56f, 0.24f);

    /// <summary>
    /// The firelight, added to the picture under the flames: a wide pool on the ground round the pit and a brighter round about the
    /// flames, both ebbing and flickering as the fire does (<see cref="FireStrength"/>).
    /// </summary>
    private void PaintGlow(CanvasItem canvas)
    {
        if (Camp is null || _site is not { } site || site.Width <= 0)
            return;
        var pixel = _fit.Size.X / site.Width;
        var fire = At(_fit, site.Fire.X, site.Fire.Y);
        var strength = FireStrength(Seconds);
        Light(canvas, fire + new Vector2(0, 22 * pixel), new Vector2(720, 300) * pixel, FireLight with { A = 0.16f * strength });
        Light(canvas, fire, new Vector2(230, 190) * pixel, FireLight with { A = 0.22f * strength });
        Light(canvas, fire, new Vector2(85, 85) * pixel, new Color(1f, 0.7f, 0.35f, 0.3f * FireStrength(Seconds * 1.7f + 3)));
    }

    private static void Light(CanvasItem canvas, Vector2 centre, Vector2 size, Color color) =>
        canvas.DrawTextureRect(LightRound, new Rect2(centre - size / 2, size), false, color);

    /// <summary>How strong the fire burns at a moment, about 0.8 to 1.1: a slow ebb and a quicker flicker over it.</summary>
    private static float FireStrength(float seconds) =>
        0.92f + 0.09f * MathF.Sin(seconds * 2.3f) + 0.06f * Noise(seconds * 2.1f, 11) + 0.05f * Noise(seconds * 9f, 23);

    /// <summary>
    /// The flames behind the campfire's pot, then its pot over them, the same size as the campfire on the same spot, so they burn up
    /// behind it, the user's design (Nov 7, 2026).
    /// </summary>
    private void PaintFireBehind(CanvasItem canvas)
    {
        if (Camp is null || _site is not { } site || site.Width <= 0)
            return;
        var pixel = _fit.Size.X / site.Width;
        PaintFlames(canvas, At(_fit, site.Fire.X, site.Fire.Y), pixel, Seconds, over: false);
        if (site.Campfire is { Pot: { } potImage } campfire && _ui.Images.Get(potImage) is { } pot && _ui.Images.Get(campfire.Image) is { } image)
            canvas.DrawTextureRect(pot, CampfireRect(campfire, image, site, _fit), false);
    }

    /// <summary>
    /// The smoke, then the flames in front of the pot, then the embers. The smoke rises from the middle of the pot's rim in a few plumes
    /// at once, each faint, growing and wandering either side as it rises and fading in and out, a different plume each time round,
    /// slanting and leaning with the wind (<see cref="Wind"/>). Each plume's foot is the middle of its image's foot, the sheet's plumes
    /// cut so (Nov 7, 2026), so a mirrored one starts where the rest do.
    /// </summary>
    private void PaintFire(CanvasItem canvas)
    {
        if (Camp is null || _site is not { } site || site.Width <= 0)
            return;
        var pixel = _fit.Size.X / site.Width;
        var fire = At(_fit, site.Fire.X, site.Fire.Y);
        var rim = Rim(site, fire, pixel);
        var seconds = Seconds;
        var wind = Wind(seconds);
        for (var i = 0; i < SmokePlumes; i++)
        {
            var age = seconds / SmokeSeconds + (float)i / SmokePlumes;
            var round = (int)MathF.Floor(age);
            var u = age - round;
            if (_ui.Images.Get(Smokes[(int)(Hash(round, i * 31 + 7) * Smokes.Length)]) is not { } smoke)
                continue;
            var height = Mathf.Lerp(SmokeFrom, SmokeTo, u) * pixel;
            var width = height * smoke.GetWidth() / smoke.GetHeight();
            var rise = SmokeRise * u;
            // The wind it leans with: halfway between the wind as it rose from the pot and the wind now, so a plume already up keeps
            // some of the slant it rose with as the wind turns.
            var lean = (Wind(seconds - u * SmokeSeconds) + wind) / 2;
            var aside = (Hash(round, i) * 2 - 1) * SmokeSpread + SmokeWander * Noise(round + u * 2, i + 40) * u + rise * MathF.Tan(lean);
            var foot = rim + new Vector2(aside, -rise) * pixel;
            var alpha = SmokeAlpha * MathF.Pow(MathF.Sin(MathF.PI * u), 1.5f);
            var mirror = Hash(round, i + 90) < 0.5f;
            canvas.DrawSetTransform(foot, lean);
            // A negative width draws the plume mirrored in the same place (UiPainter.Image), so its foot stays on the rim.
            canvas.DrawTextureRect(smoke, new Rect2(-width / 2, -height, mirror ? -width : width, height), false, new Color(0.95f, 0.9f, 0.84f, alpha));
        }
        canvas.DrawSetTransform(Vector2.Zero);
        PaintFlames(canvas, fire, pixel, seconds, over: true);
        PaintEmbers(canvas, fire, pixel, seconds);
    }

    /// <summary>
    /// The stones in front of the fire, the campfire's front (<see cref="CampfireArt.Front"/>), the same size as the campfire on the same
    /// spot, over the flames and the embers, hiding the flames' feet and the embers as they leave the logs.
    /// </summary>
    private void PaintFront(CanvasItem canvas)
    {
        if (Camp is null || _site is not { } site || site.Width <= 0)
            return;
        if (site.Campfire is { Front: { } frontImage } campfire && _ui.Images.Get(frontImage) is { } front && _ui.Images.Get(campfire.Image) is { } image)
            canvas.DrawTextureRect(front, CampfireRect(campfire, image, site, _fit), false);
    }

    /// <summary>Where the smoke rises from: the pot's rim as the campfire's art draws it, else over the campsite's pot, else over the fire.</summary>
    private Vector2 Rim(CampsiteRecord site, Vector2 fire, float pixel)
    {
        if (site.Campfire is { Rim: { } rim } campfire && _ui.Images.Get(campfire.Image) is { } image)
        {
            var drawn = CampfireRect(campfire, image, site, _fit);
            return drawn.Position + new Vector2(rim.X * drawn.Size.X, rim.Y * drawn.Size.Y);
        }
        return site.Pot is { } pot ? At(_fit, pot.X, pot.Y) - new Vector2(0, SmokeFromPot * pixel) : fire - new Vector2(0, 70 * pixel);
    }

    /// <summary>
    /// The flames behind the pot or those in front of it (<see cref="FlameLayout"/>). Each changes through the sheet's shapes, one fading
    /// into the next, its height flickering and its tip swaying, its foot on the logs, and leans with the wind (<see cref="Wind"/>).
    /// </summary>
    private void PaintFlames(CanvasItem canvas, Vector2 fire, float pixel, float seconds, bool over)
    {
        var wind = Wind(seconds);
        for (var i = 0; i < FlameLayout.Length; i++)
        {
            if (FlameLayout[i].Over != over)
                continue;
            var (x, tall, flicker, rate) = (FlameLayout[i].X, FlameLayout[i].Height, FlameLayout[i].Tall, FlameLayout[i].Rate);
            var shape = seconds * rate + i * 0.37f;
            var step = (int)MathF.Floor(shape);
            var blend = Smooth(shape - step);
            var height = tall * (0.86f + 0.14f * Noise(seconds * 3.1f, i * 5 + 1)) * pixel;
            var sway = 0.16f * Noise(seconds * 1.4f, i * 7 + 2);
            var foot = fire + new Vector2((x + 1.5f * Noise(seconds * 2.6f, i * 3 + 9)) * pixel, FlameDrop * pixel);
            // The sway skews the flame's tip; the wind turns the whole flame about its foot.
            canvas.DrawSetTransformMatrix(new Transform2D(wind, foot) * new Transform2D(new Vector2(1, 0), new Vector2(sway, 1), Vector2.Zero));
            Flame(canvas, FlameOf(step, i, flicker), height, FlameAlpha * (1 - blend));
            Flame(canvas, FlameOf(step + 1, i, flicker), height, FlameAlpha * blend);
        }
        canvas.DrawSetTransform(Vector2.Zero);
    }

    /// <summary>
    /// The embers, the user's design (Nov 7, 2026): small, uneven and now and then. Each of a few places in the air tries to send one up
    /// once a round of its own length; only some tries rise, fewer while the fire's mood, a slow wander, is low, so some stretches have
    /// none and some two or three close together, one every two seconds or so on the whole. How many places, how long a round is, the
    /// share of tries that rise as the fire's mood is middling, the seconds an ember is in the air at least and at most, and how large
    /// it's drawn: the picture's pixels to the sheet's, so its bright core is a pixel or three with its glow round it. Some embers fly
    /// higher, the user's choice (Nov 7, 2026): the share that do, and how much higher and longer they rise.
    /// </summary>
    private const int EmberPlaces = 6;
    private const float EmberRound = 2.8f, EmberChance = 0.27f, EmberShortest = 0.8f, EmberLongest = 1.9f, EmberScale = 0.3f;
    private const float EmberHighShare = 0.4f, EmberHigher = 1.35f;

    /// <summary>
    /// The embers rising from the fire. Each starts among the flames and rises fast, slowing as it cools, wandering and drifting off to a
    /// side, slanting with the wind (<see cref="Wind"/>), which takes over from its own drift as it blows; it shows at once, twinkles and
    /// fades slowly, reddening and shrinking as it goes; each is its own size and brightness, and rises its own height.
    /// </summary>
    private void PaintEmbers(CanvasItem canvas, Vector2 fire, float pixel, float seconds)
    {
        var wind = Wind(seconds);
        var blowing = MathF.Min(1, MathF.Abs(wind) / Mathf.DegToRad(WindLean));
        var mood = 0.5f + 0.5f * Noise(seconds * 0.15f, 307);
        for (var i = 0; i < EmberPlaces; i++)
        {
            var seed = i * 17;
            var length = EmberRound * (0.75f + 0.6f * Hash(i, 301));
            var age = seconds / length + Hash(i, 302);
            var round = (int)MathF.Floor(age);
            if (Hash(round, seed + 3) > EmberChance * (0.3f + 1.4f * mood))
                continue;
            // How much higher it flies, its seconds in the air, longer as it flies higher, and when in the round it rises.
            var higher = Hash(round, seed + 15) < EmberHighShare ? EmberHigher : 1;
            var life = MathF.Min(length, Mathf.Lerp(EmberShortest, EmberLongest, Hash(round, seed + 4)) * higher);
            var u = ((age - round) * length - (length - life) * Hash(round, seed + 5)) / life;
            if (u is < 0 or > 1 || _ui.Images.Get(Embers[(int)(Hash(round, seed + 6) * Embers.Length)]) is not { } ember)
                continue;
            // Its own drift to a side, half of it as the wind blows full, and the wind's slant.
            var height = (45 + 120 * Hash(round, seed + 8)) * higher;
            var lean = (Hash(round, seed + 7) - 0.5f) * 70 * (1 - 0.5f * blowing) + height * MathF.Tan(wind);
            var rise = height * (1 - (1 - u) * (1 - u));
            var wander = 9 * Noise(seconds * 1.6f + i * 4.3f, seed + 9) * u;
            var from = new Vector2((Hash(round, seed + 10) - 0.5f) * 34, 4 - 20 * Hash(round, seed + 11));
            var at = fire + (from + new Vector2(lean * u + wander, -rise)) * pixel;
            var shown = Smooth(MathF.Min(1, u / 0.1f)) * (1 - Smooth(MathF.Max(0, (u - 0.35f) / 0.65f)));
            var alpha = (0.3f + 0.7f * Hash(round, seed + 12)) * shown * (0.7f + 0.3f * Noise(seconds * 11 + i * 7, seed + 13));
            if (alpha <= 0.01f)
                continue;
            var size = new Vector2(ember.GetWidth(), ember.GetHeight()) * EmberScale * (0.6f + 0.8f * Hash(round, seed + 14)) * (1 - 0.4f * u) * pixel;
            // The streaks lean up and to the right on the sheet: one drifting left is mirrored, a negative width drawing it so in the same place.
            canvas.DrawTextureRect(ember, new Rect2(at - size / 2, new Vector2(lean < 0 ? -size.X : size.X, size.Y)), false,
                new Color(1, 1 - 0.3f * u, 1 - 0.45f * u, alpha));
        }
    }

    /// <summary>A flame's sprite for a step of its changing, chosen from the tall or the short by a hash of the step, and whether it's mirrored.</summary>
    private (RecordId Image, bool Mirror) FlameOf(int step, int flame, bool tall)
    {
        var pick = (int)(Hash(step, flame * 13 + 5) * 7);
        return (Flames[(tall ? 0 : 7) + pick], Hash(step, flame * 13 + 6) < 0.5f);
    }

    /// <summary>A flame drawn standing on the transform's origin, so tall, at an alpha.</summary>
    private void Flame(CanvasItem canvas, (RecordId Image, bool Mirror) flame, float height, float alpha)
    {
        if (alpha <= 0.01f || _ui.Images.Get(flame.Image) is not { } texture)
            return;
        var width = height * texture.GetWidth() / texture.GetHeight() * FlameWidth;
        // Two bands of the sprite: above, as it is; its foot, the bottom FlameFoot of it, fading in from nothing at the bottom to the
        // flame's own at the band's top, the user's design (Nov 7, 2026).
        var (left, right) = flame.Mirror ? (1f, 0f) : (0f, 1f);
        var band = 1 - FlameFoot;
        var shown = new Color(1, 1, 1, alpha);
        var clear = new Color(1, 1, 1, 0);
        var foot = -height * FlameFoot;
        canvas.DrawPolygon([new(-width / 2, -height), new(width / 2, -height), new(width / 2, foot), new(-width / 2, foot)],
            [shown, shown, shown, shown], [new(left, 0), new(right, 0), new(right, band), new(left, band)], texture);
        canvas.DrawPolygon([new(-width / 2, foot), new(width / 2, foot), new(width / 2, 0), new(-width / 2, 0)],
            [shown, shown, clear, clear], [new(left, band), new(right, band), new(right, 1), new(left, 1)], texture);
    }

    /// <summary>How much of a flame's height, from its foot, fades in from nothing.</summary>
    private const float FlameFoot = 0.25f;

    /// <summary>The seconds the fire has burned, the clock it's animated by.</summary>
    private static float Seconds => Time.GetTicksMsec() / 1000f;

    private static float Smooth(float t) => t * t * (3 - 2 * t);

    /// <summary>A smooth wander between -1 and 1 over time, a different one for each seed.</summary>
    private static float Noise(float t, int seed)
    {
        var i = (int)MathF.Floor(t);
        var f = t - i;
        return Mathf.Lerp(Hash(i, seed), Hash(i + 1, seed), Smooth(f)) * 2 - 1;
    }

    /// <summary>A number from 0 to 1 for a step and a seed, the same each time.</summary>
    private static float Hash(int step, int seed)
    {
        var h = (uint)(step * 374761393 + seed * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0x1000000;
    }

    /// <summary>The biome's ambient light on the figures, multiplied into their colours, its alpha unused; white with none.</summary>
    private static Color Ambient(CampsiteRecord site) =>
        site.Ambient is { } a ? new Color(a.R / 255f, a.G / 255f, a.B / 255f) : Colors.White;

    // The figures' light, the user's design (Nov 7, 2026; the campsite brief's section 7): each figure's colours times the biome's ambient
    // light, and the fire's added over it through the figure's normal map, as a point of light among the flames, warm and flickering
    // with them. The camp's ground is worked out from the picture's perspective, in figure heights (the campsite's one canonical height):
    // a slot's scale says how much nearer the camera than the fire it is, the camera standing FireDistance from the fire, so a figure in
    // front of the fire has the fire behind it, and its back, which faces the viewer, takes little of the light and its edges the most,
    // and one beside the fire has it on the side that faces it. Below a figure's middle the flames are higher than it, above lower.

    /// <summary>
    /// How far the camera is from the fire, in figure heights: from the ring of the campfire's stones, 182 of the picture's pixels wide
    /// and about 40 deep, which looks down on it at about 13 degrees, the camera a figure height and a third over the ground.
    /// </summary>
    private const float FireDistance = 5.9f;

    /// <summary>
    /// The ambient light from above and below, times the biome's: what faces up takes a little more of the canopy's light, and what faces
    /// down a little less, greener, from the grass (the brief's "do not simply tint every pixel the same colour").
    /// </summary>
    private static readonly Color FigureAbove = new(1.12f, 1.1f, 1.06f), FigureBelow = new(0.78f, 0.84f, 0.72f);

    /// <summary>
    /// The fire's light on the figures: its colour, how strong it is at its strongest, how high the flames' middle is over the ground, how far
    /// its light reaches, fading smoothly to nothing there, in figure heights (the debug overlay draws it on the ground, the user's ask,
    /// Nov 7, 2026), and how far round a figure's edge it wraps.
    /// </summary>
    private static readonly Color FigureFireColor = new(1f, 0.6f, 0.3f);
    private const float FigureFireStrength = 1.2f, FigureFireRise = 0.12f, FigureFireRadius = 2.75f, FigureFireWrap = 0.3f;

    /// <summary>
    /// The figures' shader: the image's colour times the ambient light, more from above than below, plus the fire's, the fire's as much as
    /// the image's normal faces it and wrapped a little round, fading with the distance to nothing at its reach. A figure's own light comes in its draw's colour, which its image isn't
    /// tinted by (<see cref="LightData"/>): red how far in front of the fire it stands, green how large a figure height is at its slot,
    /// blue its ground's row. A mirrored image turns its normals with it. Green in a normal map is up.
    /// </summary>
    private const string FigureShader = """
        shader_type canvas_item;
        render_mode unshaded;

        uniform vec3 ambient = vec3(1.0);
        uniform vec3 above = vec3(1.0);
        uniform vec3 below = vec3(1.0);
        uniform vec3 fire_color = vec3(1.0, 0.6, 0.3);
        uniform float fire_strength = 0.0;
        uniform float fire_x = 0.0;
        uniform float fire_rise = 0.12;
        uniform float fire_radius = 2.75;
        uniform float wrap = 0.3;

        varying vec4 figure;
        varying vec2 at;

        void vertex() {
            figure = COLOR;
            at = VERTEX;
        }

        void fragment() {
            vec4 albedo = texture(TEXTURE, UV);
            vec3 n = texture(NORMAL_TEXTURE, UV).rgb * 2.0 - 1.0;
            n.x *= dFdx(UV.x) < 0.0 ? -1.0 : 1.0;
            n = normalize(n);
            float forward = (figure.r - 0.5) * 8.0;
            float size = max(figure.g * 256.0, 0.001);
            float ground = figure.b * 512.0 - 128.0;
            vec3 to_fire = vec3((fire_x - at.x) / size, fire_rise - (ground - at.y) / size, -forward);
            float d = length(to_fire);
            float facing = max(0.0, (dot(n, to_fire / max(d, 0.001)) + wrap) / (1.0 + wrap));
            float near = clamp(1.0 - (d / fire_radius) * (d / fire_radius), 0.0, 1.0);
            float falloff = near * near;
            vec3 sky = ambient * mix(below, above, n.y * 0.5 + 0.5);
            COLOR = vec4(albedo.rgb * (sky + fire_color * fire_strength * facing * falloff), albedo.a);
        }
        """;

    private static readonly Shader FigureLightShader = new() { Code = FigureShader };
    private readonly ShaderMaterial _figureLight = new() { Shader = FigureLightShader };

    /// <summary>A figure's image with its normal map, made once: the image alone where it has none, which the shader takes as flat.</summary>
    private readonly Dictionary<RecordId, Texture2D?> _lit = [];

    private Texture2D? Lit(CampPose pose)
    {
        if (_lit.TryGetValue(pose.Image, out var made))
            return made;
        var image = _ui.Images.Get(pose.Image);
        if (pose.Normal is { } normal && image is CanvasTexture { DiffuseTexture: { } diffuse } && _ui.Images.Get(normal) is CanvasTexture { DiffuseTexture: { } map })
            image = new CanvasTexture { DiffuseTexture = diffuse, NormalTexture = map, TextureFilter = TextureFilterEnum.LinearWithMipmaps };
        return _lit[pose.Image] = image;
    }

    /// <summary>A figure's own light, in its draw's colour for the shader: how far in front of the fire its slot is, how large a figure height is there, and its ground's row.</summary>
    private static Color LightData(CampsiteRecord site, CampSlot slot, Vector2 foot, float pixel)
    {
        var forward = slot.Scale > 0 ? FireDistance * (1 - 1 / slot.Scale) : 0;
        var height = pixel * (site.FigureHeight > 0 ? site.FigureHeight : 230) * slot.Scale;
        return new Color(Math.Clamp(forward / 8 + 0.5f, 0, 1), Math.Clamp(height / 256, 0, 1), Math.Clamp((foot.Y + 128) / 512, 0, 1));
    }

    /// <summary>The figures' light as the fire burns now: the biome's ambient, the fire's place and its strength, every frame.</summary>
    private void LightFigures()
    {
        if (Scene() is not var (site, fit))
            return;
        _figureLight.SetShaderParameter("ambient", Ambient(site));
        _figureLight.SetShaderParameter("above", FigureAbove);
        _figureLight.SetShaderParameter("below", FigureBelow);
        _figureLight.SetShaderParameter("fire_color", FigureFireColor);
        _figureLight.SetShaderParameter("fire_strength", FigureFireStrength * FireStrength(Seconds));
        _figureLight.SetShaderParameter("fire_x", At(fit, site.Fire.X, site.Fire.Y).X);
        _figureLight.SetShaderParameter("fire_rise", FigureFireRise);
        _figureLight.SetShaderParameter("fire_radius", FigureFireRadius);
        _figureLight.SetShaderParameter("wrap", FigureFireWrap);
    }

    /// <summary>
    /// How far the fire's light on the figures reaches, a thin ring on the ground round the fire, the user's ask (Nov 7, 2026): a circle on
    /// the ground, so drawn in the picture's perspective, nearer the camera lower and wider (<see cref="FireDistance"/>, the campsite's
    /// horizon and canonical height). A figure standing on it has its feet at the light's reach and the rest of it just past. Nothing
    /// where the campsite hasn't its horizon or its height.
    /// </summary>
    private void FireReach(CanvasItem canvas, CampsiteRecord site, Rect2 fit)
    {
        if (site.Horizon <= 0 || site.FigureHeight <= 0)
            return;
        var pixel = fit.Size.X / site.Width;
        var fire = At(fit, site.Fire.X, site.Fire.Y);
        var horizon = fit.Position.Y + fit.Size.Y * site.Horizon;
        // On the ground, the light's reach less the flames' height over it.
        var radius = MathF.Sqrt(FigureFireRadius * FigureFireRadius - FigureFireRise * FigureFireRise);
        const int Steps = 96;
        var ring = new Vector2[Steps + 1];
        for (var i = 0; i <= Steps; i++)
        {
            var angle = Mathf.Tau * i / Steps;
            var across = radius * MathF.Cos(angle);
            var forward = radius * MathF.Sin(angle);
            // A point nearer the camera than the fire by forward stands at this scale, its row as far under the horizon again.
            var scale = 1 / (1 - forward / FireDistance);
            ring[i] = new Vector2(fire.X + across * site.FigureHeight * pixel * scale, horizon + (fire.Y - horizon) * scale);
        }
        canvas.DrawPolyline(ring, new Color(1f, 0.62f, 0.25f, 0.85f), 0.3f, true);
        // Named over its left end, clear of the band, which its front can run under, and of the buttons.
        const string Name = "fire light's reach";
        var fine = Font(UiPainter.FineText);
        var end = ring[Steps / 2];
        _ui.Text(canvas, fine, Name, (int)end.X + 2, (int)end.Y - 7, Warn);
    }

    private void DrawOverlay(CanvasItem canvas, CampMenu camp, CampsiteRecord site, Rect2 fit)
    {
        var green = new Color(Overlay.R / 255f, Overlay.G / 255f, Overlay.B / 255f);
        var fire = At(fit, site.Fire.X, site.Fire.Y);
        canvas.DrawLine(fire - new Vector2(6, 0), fire + new Vector2(6, 0), Colors.OrangeRed, 1);
        canvas.DrawLine(fire - new Vector2(0, 6), fire + new Vector2(0, 6), Colors.OrangeRed, 1);
        if (site.Pot is { } pot)
            canvas.DrawArc(At(fit, pot.X, pot.Y), 4, 0, Mathf.Tau, 16, Colors.Orange, 1);
        if (site.SafeUi is { } safe)
            canvas.DrawRect(new Rect2(At(fit, safe.X, safe.Y), new Vector2(fit.Size.X * safe.Width, fit.Size.Y * safe.Height)), green with { A = 0.6f }, false, 1);
        var fine = Font(UiPainter.FineText);
        var taken = camp.Arrangement?.Placements.ToDictionary(p => p.Slot.Id, p => p) ?? [];
        foreach (var slot in site.Slots)
        {
            var at = At(fit, slot.X, slot.Y);
            var used = taken.ContainsKey(slot.Id);
            canvas.DrawArc(at, 3, 0, Mathf.Tau, 12, used ? green : green with { A = 0.5f }, 1);
            canvas.DrawLine(at - new Vector2(0, 3), at + new Vector2(0, 3), green with { A = 0.6f }, 1);
            if (slot.Interaction is { } hand)
                canvas.DrawLine(at, At(fit, hand.X, hand.Y), Colors.Orange with { A = 0.5f }, 1);
            _ui.Text(canvas, fine, $"{Plain(slot.Id)} x{slot.Scale:0.00} d{slot.Depth}", (int)at.X + 4, (int)at.Y - 4, used ? Overlay : Dim);
        }
        FireReach(canvas, site, fit);
        var line = 2;
        var lines = new List<(string Text, Rgba Color)>
        {
            ($"{site.Name} {site.Width} x {site.Height}, seed {camp.Seed:X8}, layout {camp.Arrangement?.Layout.Size}: {Plain(string.Join(", ", camp.Arrangement?.Layout.Slots ?? []))}", Overlay),
        };
        if (!site.Calibrated)
            lines.Add(("Placeholders: the fire, the pot and the slots aren't measured in the engine yet (campsite.calibrated).", Warn));
        if (camp.Arrangement is { Fallback: true })
            lines.Add(("The fallback arrangement: no layout took the party.", Warn));
        foreach (var note in camp.Arrangement?.Diagnostics ?? [])
            lines.Add((note, Warn));
        foreach (var (text, color) in lines)
        {
            _ui.Text(canvas, fine, text, 3, line, color);
            line += 7;
        }
    }

    /// <summary>
    /// The band on its control: the pause menu's parchment, the game's own, over the whole band, its torn top and bottom edges a little
    /// past it; the tabs down the left; the tab showing; and the last thing said. Drawn from the band's own corner, the control being cut
    /// to what shows between the rollers.
    /// </summary>
    private void PaintBand(CanvasItem canvas)
    {
        if (Camp is not { } camp)
            return;
        _base = _band.Position - Band.Position;
        canvas.DrawSetTransform(_base);
        var width = _band.Size.X;
        var height = _band.Size.Y;
        if (_ui.Images.Get(Parchment) is { } parchment)
            canvas.DrawTextureRect(parchment, new Rect2(-2, -2, width + 4, height + 4), false);
        else
            canvas.DrawRect(new Rect2(0, 0, width, height), new Color(0, 0, 0, 0.55f));
        // The content's paper, the wide sheet, from the tabs' right ends to the band's right side, as tall as the tabs' column, and the
        // content within its edges.
        var paper = new Rect2(Side + TabWidth - 1, PaperTop, width - Side - (Side + TabWidth - 1), height - 2 * PaperTop);
        DrawTabs(canvas, camp, paper);
        var content = new Rect2(paper.Position.X + PaperInsetX, paper.Position.Y + PaperInsetY, paper.Size.X - 2 * PaperInsetX, paper.Size.Y - 2 * PaperInsetY);
        if (camp.Tab == CampTab.Camp)
            DrawCampTab(canvas, camp, content);
        else
            DrawRecipes(canvas, camp, content);
        if (camp.Message is { } message && camp.Tab != CampTab.Camp)
        {
            var small = Font(UiPainter.SmallText);
            Print(canvas, small, message, content.Position.X, content.End.Y - small.LineHeight, Said);
        }
        canvas.DrawSetTransform(Vector2.Zero);
    }

    /// <summary>
    /// The tabs down the band's left, spread from its top to its foot, each on a slip of the gold's parchment with its icon at the left
    /// and its caption after, the user's design (Nov 7, 2026): the others in brown on slips a shade darker, behind the content's paper;
    /// the tab showing in the cursor's red on its slip plain, over the paper, cut at the paper's edge and joined to it as the Party
    /// tab's is to its page, sideways; and the pointer before it while the cursor is on the tabs.
    /// </summary>
    private void DrawTabs(CanvasItem canvas, CampMenu camp, Rect2 paper)
    {
        var small = Font(UiPainter.SmallText);
        var count = CampMenu.Tabs.Count;
        var tabHeight = (paper.Size.Y - (count - 1) * TabGap) / count;
        var columnTop = paper.Position.Y + (paper.Size.Y - count * tabHeight - (count - 1) * TabSpacing) / 2;
        // Each slip runs on under the paper, so no end of it shows beside the paper; what the mouse finds is the part that shows.
        Rect2 SlipRect(int i) => new(Side, columnTop + i * (tabHeight + TabSpacing), TabWidth + TabUnder, tabHeight);
        Rect2 TabRect(int i) => SlipRect(i) with { Size = new Vector2(paper.Position.X - Side, tabHeight) };
        var showing = CampMenu.Tabs.ToList().IndexOf(camp.Tab);
        for (var i = 0; i < count; i++)
        {
            if (i == showing)
                continue;
            if (UiPainter.Original)
                canvas.DrawRect(TabRect(i), new Color(0, 0, 0, 0.12f));
            else
                _ui.Slip(canvas, SlipRect(i), i == _hoveredTab ? HoveredTab : SlipDim, sheet: UiPainter.GoldSheet);
            TabFace(canvas, small, i, TabRect(i), current: false);
            _hits.Add((TabRect(i), Target.Tab, i));
        }
        if (UiPainter.Original)
            canvas.DrawRect(paper, new Color(0, 0, 0, 0.18f));
        else
            _ui.Slip(canvas, paper, sheet: UiPainter.WideSheet);
        var current = TabRect(showing);
        if (UiPainter.Original)
        {
            canvas.DrawRect(current, new Color(0, 0, 0, 0.25f));
        }
        else
        {
            // Over the paper, cut at its border, so the tab's top and foot edges stop where they meet the paper's, and joined to it
            // between them, so the tab and the paper read as one sheet (the user's eye, Nov 7, 2026).
            _ui.Slip(canvas, SlipRect(showing), sheet: UiPainter.GoldSheet, cutRightAt: paper.Position.X + PaperEdge);
            JoinTab(canvas, current, paper);
        }
        TabFace(canvas, small, showing, current, current: true);
        // The pointer inside the tab, at its left, clear of the roller over the band's end.
        if (camp.Focus == CampFocus.Tabs)
            Pointer(canvas, Side + PointerLeft, current.GetCenter().Y, Cursor);
        _hits.Add((current, Target.Tab, showing));
    }

    /// <summary>A tab's icon and caption on its slip, the tab showing in the cursor's red and the others in brown with their icons faded.</summary>
    private void TabFace(CanvasItem canvas, UiFont small, int i, Rect2 rect, bool current)
    {
        var middle = rect.GetCenter().Y;
        if (UiImages.Art(TabIcons[i]) is { } icon)
            canvas.DrawTextureRect(icon, new Rect2(rect.Position.X + TabIconLeft, middle - TabIconSize / 2, TabIconSize, TabIconSize),
                false, current ? Colors.White : new Color(1, 1, 1, 0.75f));
        Print(canvas, small, CampMenu.Tabs[i].ToString(), rect.Position.X + TabIconLeft + TabIconSize + 2, middle - small.Middle, current ? Cursor : Text);
    }

    /// <summary>
    /// The tab showing joined to the content's paper, as the Party tab's is to its page but sideways: paper from just inside the
    /// paper's torn left edge, at the paper's scale, over where the tab's right end and the paper's edge meet, down the tab but for its
    /// top and bottom edges, its ends faded over <see cref="JoinFeather"/> so it runs into the papers either side.
    /// </summary>
    private void JoinTab(CanvasItem canvas, Rect2 tab, Rect2 paper)
    {
        var sheet = UiPainter.WideSheet;
        if (UiImages.Art(sheet) is not { } texture)
            return;
        var art = UiImages.ArtImage(sheet).Paper;
        var scale = paper.Size / art.Size;
        var patch = new Rect2(tab.End.X - JoinFeather - 1, tab.Position.Y + TabEdge, JoinFeather + 1 + PaperEdge + JoinFeather, tab.Size.Y - 2 * TabEdge);
        // From the paper's parchment inside its torn left edge, at the patch's own rows, so it's the paper's grain and tone there.
        var source = new Rect2(art.Position.X + art.Size.X * 0.05f, art.Position.Y + (patch.Position.Y - paper.Position.Y) / scale.Y, patch.Size / scale);
        var size = texture.GetSize();
        float[] xs = [patch.Position.X, patch.Position.X + JoinFeather, patch.End.X - JoinFeather, patch.End.X];
        float[] ys = [patch.Position.Y, patch.Position.Y + JoinSideFeather, patch.End.Y - JoinSideFeather, patch.End.Y];
        Vector2 Uv(float x, float y) => (source.Position + new Vector2(x - patch.Position.X, y - patch.Position.Y) / scale) / size;
        var solid = Colors.White;
        Color Corner(int i, int j) => i is 0 or 3 || j is 0 or 3 ? solid with { A = 0 } : solid;
        for (var i = 0; i < 3; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                canvas.DrawPolygon(
                    [new(xs[i], ys[j]), new(xs[i + 1], ys[j]), new(xs[i + 1], ys[j + 1]), new(xs[i], ys[j + 1])],
                    [Corner(i, j), Corner(i + 1, j), Corner(i + 1, j + 1), Corner(i, j + 1)],
                    [Uv(xs[i], ys[j]), Uv(xs[i + 1], ys[j]), Uv(xs[i + 1], ys[j + 1]), Uv(xs[i], ys[j + 1])], texture);
            }
        }
    }

    /// <summary>
    /// The Camp tab, the user's design (Nov 7, 2026): each member's name and level in a row over their portrait, which pulses with their
    /// hunger or tiredness as the Party tab's does; under it the hit points bar in the Party tab's own art, taller for the hit points
    /// inside it, split into Stamina's lighter red and Endurance's darker by the same rule; under that their statuses stacked in a
    /// column, the one place they are, each in a badge as wide as the portrait, its icon fixed at the left and its name centred in the
    /// rest; and along the foot the location and the inventory at the left and Rest at the right, the one thing A does here.
    /// </summary>
    private void DrawCampTab(CanvasItem canvas, CampMenu camp, Rect2 content)
    {
        var small = Font(UiPainter.SmallText);
        var fine = Font(UiPainter.FineText);
        var members = camp.Members;
        // As large as five framed portraits fit across with their gaps, the HUD's 30 at most, the same size for a smaller party.
        var slots = Math.Max(members.Count, Core.Characters.Party.MaxSize);
        var block = Math.Min(PortraitSize + 2 * PortraitBorder, (content.Size.X - (slots - 1) * FrameGap) / slots);
        var size = block - 2 * PortraitBorder;
        var pitch = block + FrameGap;
        var total = members.Count * pitch - FrameGap;
        var x0 = content.Position.X + Math.Max(0, (content.Size.X - total) / 2);
        var seconds = Time.GetTicksMsec() / 1000f;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var x = x0 + i * pitch;
            // The name at the left and the level at the right of the frame, their capitals' middles level.
            var rowMiddle = content.Position.Y + NameRow / 2 - 0.5f;
            Print(canvas, small, member.Name, x, rowMiddle - small.Middle, member.IsDead ? Dim : Text);
            var level = CampMenu.LevelLabel(member);
            Print(canvas, fine, level, x + block - fine.ExactWidth(level), rowMiddle - fine.Middle, Note);
            var frameTop = content.Position.Y + NameRow;
            var rect = new Rect2(x + PortraitBorder, frameTop + PortraitBorder, size, size);
            FramedPortrait(canvas, member, rect, seconds);
            // The bar under the frame, as wide as it, with the hit points on it.
            var frame = UiPainter.BarSpanning(x, x + block, frameTop + block + 1, BarHeight);
            var share = member.MaxHp > 0 ? Math.Clamp((float)member.Hp / member.MaxHp, 0, 1) : 0;
            var stamina = member.MaxHp > 0 ? Math.Clamp((float)member.StaminaPoints / member.MaxHp, 0, 1) : 0;
            _ui.HealthBar(canvas, frame, share, stamina, HeaderNumbers.Of(member).BarSplits ? 1 : 0);
            _hits.Add((new Rect2(x, frame.Position.Y, block, BarHeight), Target.Bar, i));
            var opening = UiPainter.BarOpening(frame);
            var hp = $"{member.Hp} / {member.MaxHp}";
            Print(canvas, fine, hp, opening.Position.X + (opening.Size.X - fine.ExactWidth(hp)) / 2, opening.Position.Y + opening.Size.Y / 2 - fine.Middle,
                BarText, BarShadow, bold: true);
            // The statuses' badges stacked under the bar, each icon drawn whole, so its round edge isn't cut as a sampled image's is.
            var badgeY = frame.End.Y + 1;
            var badges = CampMenu.Badges(member);
            for (var n = 0; n < badges.Count; n++)
            {
                var (icon, name, _) = badges[n];
                var badge = new Rect2(x, badgeY, block, BadgeHeight);
                _hits.Add((badge, Target.Badge, i * 4 + n));
                if (UiPainter.Original)
                    canvas.DrawRect(badge, new Color(0, 0, 0, 0.2f));
                else
                    _ui.Slip(canvas, badge, sheet: UiPainter.TooltipSheet);
                if (_ui.Images.Get(icon) is { } mark)
                    canvas.DrawTextureRect(mark, new Rect2(x + BadgeIconLeft, badgeY + (BadgeHeight - BadgeIcon) / 2, BadgeIcon, BadgeIcon), false);
                var textLeft = x + BadgeIconLeft + BadgeIcon + 1;
                var textRoom = x + block - 1 - textLeft;
                Print(canvas, fine, name, textLeft + Math.Max(0, (textRoom - fine.ExactWidth(name)) / 2), badgeY + BadgeHeight / 2 - fine.Middle, Said);
                badgeY += BadgePitch;
            }
        }
        // The foot: Long Rest on a slip at the right and Quick Rest beside it, the cursor's with the pointer while the cursor is in the
        // content, and the location at the left, level with them, the user's design (Nov 7, 2026).
        var longWidth = small.ExactWidth(CampMenu.RestText) + 14;
        var longButton = new Rect2(content.End.X - longWidth, content.End.Y - FootHeight, longWidth, FootHeight);
        var quickWidth = small.ExactWidth(CampMenu.QuickRestText) + 14;
        var quickButton = new Rect2(longButton.Position.X - RestButtonGap - quickWidth, longButton.Position.Y, quickWidth, FootHeight);
        var focused = camp.Focus == CampFocus.Content && !camp.RestListOpen;
        RestButton(canvas, quickButton, CampMenu.QuickRestText, small, focused && camp.RestButton == CampRestButton.Quick);
        _hits.Add((quickButton, Target.Rest, 0));
        RestButton(canvas, longButton, CampMenu.RestText, small, focused && camp.RestButton == CampRestButton.Long);
        _hits.Add((longButton, Target.Rest, 1));
        Print(canvas, fine, camp.LocationLine, content.Position.X, longButton.GetCenter().Y - fine.Middle, Text);
        if (camp.RestListOpen)
            DrawRestList(canvas, camp, longButton, small);
    }

    /// <summary>How far apart Quick Rest and Long Rest stand.</summary>
    private const float RestButtonGap = 3;

    /// <summary>A rest button: a slip of the gold's parchment with its label, in red with the pointer when the cursor's on it.</summary>
    private void RestButton(CanvasItem canvas, Rect2 button, string label, UiFont small, bool current)
    {
        var middle = button.GetCenter().Y;
        if (UiPainter.Original)
            canvas.DrawRect(button, new Color(0, 0, 0, 0.2f));
        else
            _ui.Slip(canvas, button, sheet: UiPainter.GoldSheet);
        if (current)
            Pointer(canvas, button.Position.X + 4, middle, Cursor);
        Print(canvas, small, label, button.Position.X + 9, middle - small.Middle, current ? Cursor : Text);
    }

    /// <summary>
    /// The ways to rest, open where the Long Rest button is, the user's design (Nov 7, 2026): a sheet of parchment over the button, its
    /// bottom right at the button's, growing up and left to fit, each way a row, the cursor's in red with the pointer.
    /// </summary>
    private void DrawRestList(CanvasItem canvas, CampMenu camp, Rect2 button, UiFont small)
    {
        // The current time at the top, in the smallest size, then the ways under it.
        var fine = Font(UiPainter.FineText);
        var time = camp.CurrentTimeLine;
        var options = camp.RestOptions;
        var width = Math.Max(options.Max(o => small.ExactWidth(o.Label)), fine.ExactWidth(time)) + RestListSide + 5;
        var height = RestTimeRow + options.Count * RestPitch + 2 * RestListEnds;
        var list = new Rect2(button.End.X - width, button.End.Y - height, width, height);
        if (UiPainter.Original)
            canvas.DrawRect(list, new Color(0, 0, 0, 0.75f));
        else
            _ui.Slip(canvas, list);
        Print(canvas, fine, time, list.Position.X + RestListSide, list.Position.Y + RestListEnds + RestTimeRow / 2 - fine.Middle, Note);
        for (var i = 0; i < options.Count; i++)
        {
            var row = new Rect2(list.Position.X, list.Position.Y + RestListEnds + RestTimeRow + i * RestPitch, width, RestPitch);
            var middle = row.GetCenter().Y;
            var current = i == camp.RestCursor;
            if (current)
                Pointer(canvas, row.Position.X + 4, middle, Cursor);
            Print(canvas, small, options[i].Label, row.Position.X + RestListSide, middle - small.Middle, current ? Cursor : Text);
            _hits.Add((row, Target.RestOption, i));
        }
    }

    /// <summary>
    /// A portrait framed as the Party tab's are, the user's design (Nov 2, 2026, and for the camp Nov 7): the small square sheet of
    /// parchment a border past it all round, and on it the portrait cut to the sheet's outline, which pulses amber or red with hunger or
    /// tiredness (<see cref="StatusPulse"/>); the original look's plain portrait, pulsing alike.
    /// </summary>
    private void FramedPortrait(CanvasItem canvas, Core.Characters.Character member, Rect2 rect, float seconds)
    {
        var tint = StatusPulse.Tint(member.Hunger, member.Rest, seconds);
        if (member.Record.Portrait is not { } portrait)
        {
            canvas.DrawRect(rect, new Color(0, 0, 0, 0.3f));
            return;
        }
        if (UiPainter.Original)
        {
            _ui.Image(canvas, portrait, rect);
            if (tint is { } plain)
                _ui.Image(canvas, portrait, rect, plain);
            return;
        }
        _ui.Slip(canvas, rect.Grow(PortraitBorder), sheet: UiPainter.SquareSheet);
        if (_ui.Images.CutTo(portrait, UiPainter.SquareSheet) is not { } cut)
            return;
        canvas.DrawTextureRect(cut, rect, false, _ui.Tint(new Rgba(255, 255, 255, 255)));
        if (tint is { } pulse)
            canvas.DrawTextureRect(cut, rect, false, _ui.Tint(pulse));
    }

    /// <summary>A recipes tab: who makes them, the recipes with the cursor, as many as fit with the cursor's among them, or why there's nothing to make.</summary>
    private void DrawRecipes(CanvasItem canvas, CampMenu camp, Rect2 content)
    {
        var standard = Font(UiPainter.StandardText);
        var small = Font(UiPainter.SmallText);
        var left = content.Position.X;
        var y = content.Position.Y;
        if (camp.Current is { } offer && camp.Member is { } member)
        {
            Print(canvas, standard, offer.Activity.Name, left, y, Text);
            Print(canvas, small, string.Format(System.Globalization.CultureInfo.InvariantCulture, CampMenu.MakerNote, member.Name), left + 60, y + 1, Note);
            y += standard.LineHeight;
        }
        if (camp.Empty is { Length: > 0 } empty)
        {
            Print(canvas, small, empty, left, y, Dim);
            return;
        }
        var row = small.LineHeight;
        // The rows that fit, a window over the recipes that keeps the cursor's within it, with a line for the message under them; the
        // cursor's row in red with the pointer while the cursor is in the content.
        var fit = Math.Max(1, (int)((content.End.Y - row - y) / row));
        var first = Math.Clamp(camp.RecipeCursor - fit + 1, 0, Math.Max(0, camp.Recipes.Count - fit));
        var focused = camp.Focus == CampFocus.Content;
        for (var i = first; i < Math.Min(camp.Recipes.Count, first + fit); i++)
        {
            var current = i == camp.RecipeCursor && focused;
            if (current)
                Pointer(canvas, left, y + row / 2f, Cursor);
            Print(canvas, small, camp.ProductName(camp.Recipes[i]), left + 6, y, current ? Cursor : Text);
            _hits.Add((new Rect2(left, y, content.Size.X, row), Target.Recipe, i));
            y += row;
        }
        if (first > 0)
            Print(canvas, small, "^", content.End.X - 6, content.Position.Y + row, Dim);
        if (first + fit < camp.Recipes.Count)
            Print(canvas, small, "v", content.End.X - 6, y - row, Dim);
    }

    /// <summary>The main font with its capitals this tall, whatever the painter's size is set to.</summary>
    private UiFont Font(float capitals) => _ui.Main.Sized(capitals);

    /// <summary>
    /// One line of text from a point placed to a fraction of a pixel on the band, in a colour; over its shadow, a little down and right,
    /// if it has one; and drawn twice a fifth of a pixel apart for a touch of weight if bold, as the Party tab's bar's hit points are.
    /// </summary>
    private void Print(CanvasItem canvas, UiFont font, string text, float x, float y, Rgba color, Rgba? shadow = null, bool bold = false)
    {
        if (shadow is { } dark)
        {
            PrintAt(canvas, font, text, x + 0.4f, y + 0.4f, dark);
            if (bold)
                PrintAt(canvas, font, text, x + 0.6f, y + 0.4f, dark);
        }
        PrintAt(canvas, font, text, x, y, color);
        if (bold)
            PrintAt(canvas, font, text, x + 0.2f, y, color);
    }

    private void PrintAt(CanvasItem canvas, UiFont font, string text, float x, float y, Rgba color)
    {
        canvas.DrawSetTransform(_base + new Vector2(x, y));
        _ui.Text(canvas, font, text, 0, 0, color);
        canvas.DrawSetTransform(_base);
    }

    /// <summary>The cursor's pointer before a row: a small triangle, as the face has no glyph for one.</summary>
    private static void Pointer(CanvasItem canvas, float x, float middle, Rgba color) =>
        canvas.DrawPolygon([new Vector2(x, middle - 2.5f), new Vector2(x + 4, middle), new Vector2(x, middle + 2.5f)],
            [new Color(color.R / 255f, color.G / 255f, color.B / 255f)]);

    private string MemberName(RecordId member) => _plugins.TryGet<EntityRecord>(member)?.Name ?? member.Key;

    /// <summary>A slot's or pose's name for the text: the face draws an underscore as one of the original sheets' icons, so spaces instead.</summary>
    private static string Plain(string name) => name.Replace('_', ' ');
}

/// <summary>
/// The camp screen over the world (<see cref="Core.GameSession.Camp"/>): under trek's UI layer, so the conversation box draws the
/// night's talk over it and the cut-scene fade darkens it as a story scene is cued, and over the world, which it covers whole. The
/// session makes the screen and holds its state; this draws it each tick and after a press, plays its band's scroll as the session
/// cues it (<see cref="CampScrollCue"/>), and hands its clicks on. The band's scroll is the pause menu's actor again, from models of
/// this layer's own.
/// </summary>
public partial class CampLayer : CanvasLayer
{
    private readonly UiScreen _screen = new() { ForwardsMouse = true };
    private readonly Models _models;
    private readonly PauseScrollView? _scroll;
    private readonly CampView _view;

    public CampLayer(PluginSet plugins)
    {
        Name = "Camp";
        Layer = PauseMenuView.MenuLayer - 2;
        Visible = false;
        _screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_screen);
        _models = new Models(plugins);
        _scroll = PauseScrollView.Create(plugins, _models);
        _view = new CampView(plugins, _scroll) { Click = _screen.Click };
        _view.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // The picture with the figures behind the fire and the campfire, the flames behind the pot and the pot, the firelight, the fire
        // in front over it, the front stones with their own firelight and the figures in front of the fire with theirs, then the band,
        // the rollers over the band's edges, and the debug marks over all.
        _screen.Canvas.AddChild(_view);
        _view.PartyBehind.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.PartyBehind);
        _view.Campfire.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.Campfire);
        _view.FireBehind.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.FireBehind);
        _view.Glow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.Glow);
        _view.Fire.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.Fire);
        _view.Front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.Front);
        _view.PartyFront.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.PartyFront);
        _screen.Canvas.AddChild(_view.Band);
        if (_scroll is not null)
        {
            _scroll.Visible = false;
            _screen.Canvas.AddChild(_scroll);
        }
        _view.Top.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view.Top);
    }

    public override void _ExitTree() => _models.Dispose();

    /// <summary>The UI screen, whose lines and filter the game sets.</summary>
    public UiScreen Screen => _screen;

    /// <summary>The band's scroll actor's clock in its state, from 1, which the core's camp steps by; without the actor, past any state's end.</summary>
    public int ScrollFrame => _scroll?.Frame ?? 1000;

    /// <summary>The length of the state the band's scroll actor is in, in frames.</summary>
    public int ScrollLength => _scroll?.Length ?? 1;

    /// <summary>A step of the band's scroll actor, as the core cues it, the actor started afresh first where the cue says.</summary>
    public void PlayScroll(CampScrollCue cue)
    {
        if (_scroll is null)
            return;
        if (cue.Restart)
            _scroll.Restart();
        _scroll.Play(cue.Cue);
    }

    /// <summary>A click on a row, which the view has put the cursor on: the button to press (<see cref="UiScreen.Clicked"/>).</summary>
    public event Action<MenuAction>? Clicked
    {
        add => _screen.Clicked += value;
        remove => _screen.Clicked -= value;
    }

    /// <summary>Whether the debug buttons are drawn: a debug build's.</summary>
    public bool DebugButtons
    {
        get => _view.DebugButtons;
        set => _view.DebugButtons = value;
    }

    /// <summary>The debug button was clicked: a camp talk is to show (<see cref="Core.GameSession.TriggerCampTalk"/>).</summary>
    public event Action? TalkRequested
    {
        add => _view.TalkRequested += value;
        remove => _view.TalkRequested -= value;
    }

    /// <summary>The debug Poses button was clicked: the figures are to sit or stand afresh (<see cref="Core.GameSession.ReposeCamp"/>).</summary>
    public event Action? PosesRequested
    {
        add => _view.PosesRequested += value;
        remove => _view.PosesRequested -= value;
    }

    /// <summary>A slot's choice in the debug overlay was made: the slot, and the figure for it standing or seated, or none (<see cref="Core.GameSession.SetCampSlot"/>).</summary>
    public event Action<string, RecordId?, bool>? SlotChosen
    {
        add => _view.SlotChosen += value;
        remove => _view.SlotChosen -= value;
    }

    /// <summary>Whether the debug overlay is drawn whatever the campsite says.</summary>
    public bool ShowOverlay
    {
        get => _view.ShowOverlay;
        set
        {
            _view.ShowOverlay = value;
            _view.QueueRedraw();
        }
    }

    /// <summary>Draws the session's camp screen as it stands, each tick and after a press: shown while there's one, hidden once it has closed.</summary>
    public void Draw(CampMenu? camp)
    {
        if (camp is not null && !ReferenceEquals(_view.Camp, camp))
            _view.Warm();
        _view.Camp = camp;
        Visible = camp is not null;
        _view.QueueRedraw();
    }
}
