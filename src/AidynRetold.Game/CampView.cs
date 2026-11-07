using AidynRetold.Core.Camp;
using AidynRetold.Core.Ui;
using AidynRetold.Plugins;
using AidynRetold.Plugins.Records;
using AidynRetold.Views;
using Godot;

namespace AidynRetold.Game;

/// <summary>
/// Draws the camp screen (<see cref="CampMenu"/>, the remade game's) on a <see cref="UiScreen"/>'s canvas: the campsite's picture
/// fitted to the screen, black either side; the party's figures at their slots, each pose's image scaled by the slot's perspective
/// and the figure's stature, mirrored where the arrangement says, or a marker where a member has no figure yet; the debug overlay of
/// the fire, the pot, the slots and the safe area while the campsite's anchors are placeholders (<see cref="CampsiteRecord.Calibrated"/>)
/// or asked for; and in the safe area the rows, the open activity's recipes and the last thing said. The mouse points at a row and a
/// click presses A on it, as in the pause menu. The night's talk shows in trek's box, which is drawn over this layer.
/// </summary>
public partial class CampView : Control
{
    /// <summary>The rows' colours on the parchment: the journal's brown, the Party tab's red for the cursor.</summary>
    private static readonly Rgba Text = new(98, 70, 60, 255), Dim = new(170, 152, 130, 255), Cursor = new(150, 40, 35, 255),
        Note = new(130, 105, 85, 255), Said = new(70, 45, 40, 255), Overlay = new(120, 255, 160, 255), Warn = new(255, 170, 90, 255),
        Button = new(255, 236, 130, 255);

    private static readonly RecordId Parchment = RecordId.Parse("acre:image/parchment"), Thirds = RecordId.Parse("acre:image/debug_thirds");

    /// <summary>How long the pan between the fire centred and the picture's foot takes, in seconds.</summary>
    public const float PanSeconds = 1f;

    /// <summary>
    /// Where a picture taller than the screen is panned to, 0 to 1: 0 with the fire at the screen's centre, for the talk, and 1 with the
    /// picture's foot at the screen's, for the rows. It moves toward <see cref="PanTarget"/> over <see cref="PanSeconds"/>.
    /// </summary>
    private float _pan;

    /// <summary>The pan the camp asks for: the fire centred while a talk shows, the foot otherwise.</summary>
    private float PanTarget => Camp is { Talking: true } ? 0 : 1;

    /// <summary>Whether the pan has arrived where the camp asks, so the rows may show.</summary>
    private bool Panned => Mathf.IsEqualApprox(_pan, PanTarget);

    private readonly UiPainter _ui;
    private readonly PluginSet _plugins;
    /// <summary>What the mouse can land on: a tab, a recipe or Rest.</summary>
    private enum Target { Tab, Recipe, Rest }

    /// <summary>Where each tab, recipe and Rest was drawn, for the mouse: its area, what it is and its index.</summary>
    private readonly List<(Rect2 Area, Target Target, int Index)> _hits = [];

    /// <summary>The tabs' column's width in the band, and the portraits' pitch and size on the Camp tab.</summary>
    private const int TabsWidth = 60, MemberPitch = 44, PortraitSize = 30;

    public CampView(PluginSet plugins)
    {
        _plugins = plugins;
        _ui = new UiPainter(plugins) { TextSize = UiPainter.StandardText };
        Name = "Camp screen";
        MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>The camp screen showing, or null.</summary>
    public CampMenu? Camp { get; set; }

    /// <summary>Whether the debug overlay is drawn whatever the campsite says: F3, as trek's overlay.</summary>
    public bool ShowOverlay { get; set; }

    /// <summary>A click on a row, which the view has put the cursor on: the button to press.</summary>
    public Action<MenuAction>? Click { get; set; }

    /// <summary>
    /// Whether the debug buttons are drawn, at the top right: a debug build's. Talk starts a camp talk at once (<see cref="TalkRequested"/>),
    /// and Debug shows the technical text and marks that won't be in the game (<see cref="ShowOverlay"/>), off until it's clicked.
    /// </summary>
    public bool DebugButtons { get; set; }

    /// <summary>The Talk button was clicked: a camp talk is to show now.</summary>
    public Action? TalkRequested { get; set; }

    /// <summary>Where the Talk and Debug buttons were drawn, or empty.</summary>
    private Rect2 _talkButton, _debugButton;

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
            QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Camp is null || Panned)
            return;
        _pan = Mathf.MoveToward(_pan, PanTarget, (float)delta / PanSeconds);
        QueueRedraw();
    }

    /// <summary>A camp opens on the talk with the fire centred: the pan starts there.</summary>
    public void Opened() => _pan = 0;

    public override void _GuiInput(InputEvent e)
    {
        if (Camp is not { Choice: CampChoice.None, Talking: false } camp)
            return;
        if (e is InputEventMouseMotion motion)
        {
            if (Hit(motion.Position) is var (target, index) && Point(camp, target, index))
                QueueRedraw();
            return;
        }
        if (e is not InputEventMouseButton { Pressed: true } button)
            return;
        if (button.ButtonIndex == MouseButton.Right)
        {
            Click?.Invoke(MenuAction.Back);
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && _talkButton.HasArea() && _talkButton.HasPoint(button.Position))
        {
            TalkRequested?.Invoke();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && _debugButton.HasArea() && _debugButton.HasPoint(button.Position))
        {
            ShowOverlay = !ShowOverlay;
            QueueRedraw();
            AcceptEvent();
        }
        else if (button.ButtonIndex == MouseButton.Left && Hit(button.Position) is var (target, index))
        {
            // A tab is shown by the click alone; a recipe or Rest is pointed at and pressed as A.
            Point(camp, target, index);
            if (target != Target.Tab)
                Click?.Invoke(MenuAction.Accept);
            QueueRedraw();
            AcceptEvent();
        }
    }

    private (Target Target, int Index)? Hit(Vector2 at)
    {
        foreach (var (area, target, index) in _hits)
        {
            if (area.HasPoint(at))
                return (target, index);
        }
        return null;
    }

    /// <summary>The mouse over something: the tab shown, or the recipe pointed at; whether anything changed.</summary>
    private static bool Point(CampMenu camp, Target target, int index)
    {
        switch (target)
        {
            case Target.Tab:
                var tab = CampMenu.Tabs[index];
                if (camp.Tab == tab)
                    return false;
                camp.Show(tab);
                return true;
            case Target.Recipe:
                return camp.PointRecipe(index);
            default:
                return false;
        }
    }

    public override void _Draw()
    {
        _hits.Clear();
        if (Camp is not { } camp)
            return;
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);
        var site = camp.Site;
        var fit = Fit(site, size);
        if (site is not null && _ui.Images.Get(site.Image) is { } picture)
            DrawTextureRect(picture, fit, false);
        if (camp.Arrangement is { } arrangement && site is not null)
            DrawParty(arrangement, site, fit);
        // The vignette as painted: a transparent image with its dark at its own alpha, laid over the picture pixel for pixel.
        if (site?.Vignette is { } vignette && _ui.Images.Get(vignette) is { } shade)
            DrawTextureRect(shade, fit, false);
        // The rows go while a talk shows in the box over the screen, and come back as it ends; the overlay and the debug buttons too.
        _talkButton = _debugButton = default;
        if (camp.Talking)
            return;
        if (site is not null && ShowOverlay)
            DrawOverlay(camp, site, fit);
        // The rows wait for the pan to bring the picture's foot up, so they're never drawn over the fire.
        if (Panned)
            DrawMenu(camp, site, fit, size);
        if (DebugButtons && camp.Choice == CampChoice.None)
            DrawButtons(size);
        // The rule-of-thirds lines over the whole screen, as the user drew them, with the overlay.
        if (ShowOverlay && _ui.Images.Get(Thirds) is { } thirds)
            DrawTextureRect(thirds, new Rect2(Vector2.Zero, size), false);
    }

    /// <summary>The debug buttons at the top right: Talk, a camp talk at once without resting, and Debug, the overlay on or off.</summary>
    private void DrawButtons(Vector2 size)
    {
        _debugButton = DrawButton(new Rect2(size.X - 36, 3, 32, 11), "Debug", ShowOverlay);
        _talkButton = DrawButton(new Rect2(size.X - 70, 3, 30, 11), "Talk", false);
    }

    private Rect2 DrawButton(Rect2 area, string text, bool lit)
    {
        var yellow = new Color(Button.R / 255f, Button.G / 255f, Button.B / 255f);
        DrawRect(area, lit ? yellow with { A = 0.35f } : new Color(0, 0, 0, 0.6f));
        DrawRect(area, yellow with { A = 0.9f }, false, 1);
        _ui.TextSize = UiPainter.SmallText;
        _ui.Text(this, _ui.Main, text, (int)area.Position.X + 4, (int)area.Position.Y + 2, Button);
        return area;
    }

    /// <summary>
    /// Where the picture is drawn, never squeezed: as wide as the screen, and if that leaves it taller than the screen, panned
    /// (<see cref="_pan"/>) between the fire at the screen's centre and its foot at the screen's; else as large as fits, whole, centred.
    /// </summary>
    private Rect2 Fit(CampsiteRecord? site, Vector2 size)
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
        return new Rect2(0, Mathf.Lerp(centred, foot, _pan), drawn.X, drawn.Y);
    }

    /// <summary>A point given as shares of the picture, in the canvas.</summary>
    private static Vector2 At(Rect2 fit, float x, float y) => fit.Position + new Vector2(fit.Size.X * x, fit.Size.Y * y);

    private void DrawParty(CampArrangement arrangement, CampsiteRecord site, Rect2 fit)
    {
        // A picture pixel's size on the canvas: a figure's image is painted at the picture's own scale, times the slot's and its stature.
        var pixel = fit.Size.X / site.Width;
        foreach (var placement in arrangement.InDrawOrder)
        {
            var foot = At(fit, placement.Slot.X, placement.Slot.Y);
            if (placement.Image is { } pose && _ui.Images.Get(pose.Image) is { } image)
            {
                var scale = pixel * placement.Scale;
                var width = image.GetWidth() * scale;
                var height = image.GetHeight() * scale;
                var anchorX = placement.Mirrored ? image.GetWidth() - pose.AnchorX : pose.AnchorX;
                var left = foot.X - anchorX * scale;
                var top = foot.Y - pose.AnchorY * scale;
                // A negative width draws the image mirrored.
                var rect = placement.Mirrored ? new Rect2(left + width, top, -width, height) : new Rect2(left, top, width, height);
                DrawTextureRect(image, rect, false, Tint(site));
            }
            else if (ShowOverlay)
            {
                // No figure to draw yet: nothing in the game, and with the overlay a dot on the anchor with who stands there.
                DrawCircle(foot, 2, Colors.White);
                _ui.TextSize = UiPainter.FineText;
                _ui.Text(this, _ui.Main, $"{MemberName(placement.Member)}, {Plain(placement.Pose)}{(placement.Mirrored ? " (mirrored)" : "")}", (int)foot.X - 10, (int)foot.Y - 12, Button);
            }
        }
    }

    /// <summary>The biome's ambient tint over the figures, restrained; white with none.</summary>
    private static Color Tint(CampsiteRecord site) =>
        site.Ambient is { } a ? new Color(a.R / 255f, a.G / 255f, a.B / 255f, a.A / 255f) : Colors.White;

    private void DrawOverlay(CampMenu camp, CampsiteRecord site, Rect2 fit)
    {
        var green = new Color(Overlay.R / 255f, Overlay.G / 255f, Overlay.B / 255f);
        var fire = At(fit, site.Fire.X, site.Fire.Y);
        DrawLine(fire - new Vector2(6, 0), fire + new Vector2(6, 0), Colors.OrangeRed, 1);
        DrawLine(fire - new Vector2(0, 6), fire + new Vector2(0, 6), Colors.OrangeRed, 1);
        if (site.Pot is { } pot)
            DrawArc(At(fit, pot.X, pot.Y), 4, 0, Mathf.Tau, 16, Colors.Orange, 1);
        if (site.SafeUi is { } safe)
            DrawRect(new Rect2(At(fit, safe.X, safe.Y), new Vector2(fit.Size.X * safe.Width, fit.Size.Y * safe.Height)), green with { A = 0.6f }, false, 1);
        _ui.TextSize = UiPainter.FineText;
        var taken = camp.Arrangement?.Placements.ToDictionary(p => p.Slot.Id, p => p) ?? [];
        foreach (var slot in site.Slots)
        {
            var at = At(fit, slot.X, slot.Y);
            var used = taken.ContainsKey(slot.Id);
            DrawArc(at, 3, 0, Mathf.Tau, 12, used ? green : green with { A = 0.5f }, 1);
            DrawLine(at - new Vector2(0, 3), at + new Vector2(0, 3), green with { A = 0.6f }, 1);
            if (slot.Interaction is { } hand)
                DrawLine(at, At(fit, hand.X, hand.Y), Colors.Orange with { A = 0.5f }, 1);
            _ui.Text(this, _ui.Main, $"{Plain(slot.Id)} x{slot.Scale:0.00} d{slot.Depth}", (int)at.X + 4, (int)at.Y - 4, used ? Overlay : Dim);
        }
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
            _ui.Text(this, _ui.Main, text, 3, line, color);
            line += 7;
        }
    }

    private void DrawMenu(CampMenu camp, CampsiteRecord? site, Rect2 fit, Vector2 size)
    {
        // The band is as wide as the conversation box, 16 to 304 of the 320 frame centred on the screen, and from where the camp's
        // safe area starts (or 0.565 of the picture with none) down to near the picture's foot, the user's layout (Nov 7, 2026).
        var frameLeft = (size.X - DialogueBox.FrameWidth) / 2;
        var bandTop = At(fit, 0, site?.SafeUi?.Y ?? 0.565f).Y;
        var bandBottom = At(fit, 0, site?.SafeUi is { } safe ? safe.Y + safe.Height : 0.945f).Y;
        var band = new Rect2(frameLeft + DialogueBox.OverscanSide, bandTop, DialogueBox.FrameWidth - 2 * DialogueBox.OverscanSide, bandBottom - bandTop);
        // The pause menu's parchment, the game's own, sized to the band: its torn top and bottom edges are transparent, so the band
        // is drawn a little taller than it is and the rows start under the tear.
        if (_ui.Images.Get(Parchment) is { } parchment)
            DrawTextureRect(parchment, band.Grow(2), false);
        else
            DrawRect(band, new Color(0, 0, 0, 0.55f));
        var left = (int)band.Position.X + 10;
        var top = (int)band.Position.Y + 10;
        _ui.TextSize = UiPainter.StandardText;
        var row = _ui.Main.LineHeight;

        // The tabs down the left, the one showing in the cursor's red with the pointer.
        var y = top;
        foreach (var tab in CampMenu.Tabs)
        {
            var current = tab == camp.Tab;
            if (current)
                Pointer(left, y + row / 2, Cursor);
            _ui.TextSize = UiPainter.StandardText;
            _ui.Text(this, _ui.Main, tab.ToString(), left + 6, y, current ? Cursor : Text);
            _hits.Add((new Rect2(band.Position.X, y, TabsWidth + 10, row), Target.Tab, (int)tab));
            y += row;
        }
        var contentLeft = left + TabsWidth;
        var dim = new Color(Dim.R / 255f, Dim.G / 255f, Dim.B / 255f, 0.8f);
        DrawLine(new Vector2(contentLeft - 6, top), new Vector2(contentLeft - 6, band.End.Y - 8), dim, 1);

        if (camp.Tab == CampTab.Camp)
            DrawCampTab(camp, contentLeft, top, band, row);
        else
            DrawRecipes(camp, contentLeft, top, band, row);

        if (camp.Message is { } message)
        {
            _ui.TextSize = UiPainter.SmallText;
            _ui.Text(this, _ui.Main, message, contentLeft, (int)band.End.Y - _ui.Main.LineHeight - 4, Said);
        }
    }

    /// <summary>The Camp tab: Rest, the one thing A does here, then each member's portrait with their health and statuses under it.</summary>
    private void DrawCampTab(CampMenu camp, int left, int top, Rect2 band, int row)
    {
        Pointer(left, top + row / 2, Cursor);
        _ui.TextSize = UiPainter.StandardText;
        _ui.Text(this, _ui.Main, camp.RestLabel, left + 6, top, Cursor);
        _hits.Add((new Rect2(left, top, band.End.X - left, row), Target.Rest, 0));

        var y = top + row + 6;
        var x = left + 6;
        foreach (var member in camp.Members)
        {
            if (member.Record.Portrait is { } portrait)
                _ui.Image(this, portrait, x, y, new Vector2I(PortraitSize, PortraitSize));
            else
                DrawRect(new Rect2(x, y, PortraitSize, PortraitSize), new Color(0, 0, 0, 0.3f));
            // The health bar under the portrait, as the HUD's: green, red below a third, over a dark backing.
            var share = member.MaxHp > 0 ? Mathf.Clamp((float)member.Hp / member.MaxHp, 0, 1) : 0;
            var barY = y + PortraitSize + 1;
            _ui.Image(this, PartyPortraits.Pixel, new Rect2(x, barY, PortraitSize, PartyPortraits.BarHeight), PartyPortraits.BarBacking);
            if (share > 0)
                _ui.Image(this, PartyPortraits.Pixel, new Rect2(x, barY, PortraitSize * share, PartyPortraits.BarHeight), share <= PartyPortraits.LowHealth ? PartyPortraits.BarLow : PartyPortraits.BarFill);
            // The statuses' icons after the bar, as the HUD shows them, and the name and hit points under.
            var iconX = x;
            foreach (var icon in PartyPortraits.StatusIcons(member.Hunger, member.Rest))
            {
                _ui.Image(this, icon, iconX, barY + PartyPortraits.BarHeight + 1, new Vector2I(PartyPortraits.IconSize, PartyPortraits.IconSize));
                iconX += PartyPortraits.IconSize + 1;
            }
            _ui.TextSize = UiPainter.SmallText;
            var nameY = barY + PartyPortraits.BarHeight + PartyPortraits.IconSize + 3;
            _ui.Text(this, _ui.Main, member.Name, x, nameY, member.IsDead ? Dim : Text);
            _ui.TextSize = UiPainter.FineText;
            _ui.Text(this, _ui.Main, $"{member.Hp} / {member.MaxHp}", x, nameY + _ui.Main.LineHeight + 1, Note);
            x += MemberPitch;
        }
    }

    /// <summary>A recipes tab: who makes them, the recipes with the cursor, or why there's nothing to make.</summary>
    private void DrawRecipes(CampMenu camp, int left, int top, Rect2 band, int row)
    {
        var y = top;
        if (camp.Current is { } offer && camp.Member is { } member)
        {
            _ui.TextSize = UiPainter.StandardText;
            _ui.Text(this, _ui.Main, offer.Activity.Name, left, y, Text);
            _ui.TextSize = UiPainter.SmallText;
            _ui.Text(this, _ui.Main, string.Format(System.Globalization.CultureInfo.InvariantCulture, CampMenu.MakerNote, member.Name), left + 70, y + 1, Note);
            y += row;
        }
        if (camp.Empty is { Length: > 0 } empty)
        {
            _ui.TextSize = UiPainter.SmallText;
            _ui.Text(this, _ui.Main, empty, left, y, Dim);
            return;
        }
        _ui.TextSize = UiPainter.SmallText;
        var small = _ui.Main.LineHeight;
        for (var i = 0; i < camp.Recipes.Count; i++)
        {
            var current = i == camp.RecipeCursor;
            if (current)
                Pointer(left, y + small / 2, Cursor);
            _ui.Text(this, _ui.Main, camp.ProductName(camp.Recipes[i]), left + 6, y, current ? Cursor : Text);
            _hits.Add((new Rect2(left, y, band.End.X - left, small), Target.Recipe, i));
            y += small;
        }
    }

    /// <summary>The cursor's pointer before a row: a small triangle, as the face has no glyph for one.</summary>
    private void Pointer(int x, int middle, Rgba color) =>
        DrawPolygon([new Vector2(x, middle - 2.5f), new Vector2(x + 4, middle), new Vector2(x, middle + 2.5f)],
            [new Color(color.R / 255f, color.G / 255f, color.B / 255f)]);

    private string MemberName(RecordId member) => _plugins.TryGet<EntityRecord>(member)?.Name ?? member.Key;

    /// <summary>A slot's or pose's name for the text: the face draws an underscore as one of the original sheets' icons, so spaces instead.</summary>
    private static string Plain(string name) => name.Replace('_', ' ');
}

/// <summary>
/// The camp screen over the world (<see cref="Core.GameSession.Camp"/>): under trek's UI layer, so the conversation box draws the
/// night's talk over it and the cut-scene fade darkens it as a story scene is cued, and over the world, which it covers whole. The
/// session makes the screen and holds its state; this draws it each tick and after a press, and hands its clicks on.
/// </summary>
public partial class CampLayer : CanvasLayer
{
    private readonly UiScreen _screen = new() { ForwardsMouse = true };
    private readonly CampView _view;

    public CampLayer(PluginSet plugins)
    {
        Name = "Camp";
        Layer = PauseMenuView.MenuLayer - 2;
        Visible = false;
        _screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_screen);
        _view = new CampView(plugins) { Click = _screen.Click };
        _view.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen.Canvas.AddChild(_view);
    }

    /// <summary>The UI screen, whose lines and filter the game sets.</summary>
    public UiScreen Screen => _screen;

    /// <summary>A click on a row, which the view has put the cursor on: the button to press (<see cref="UiScreen.Clicked"/>).</summary>
    public event Action<MenuAction>? Clicked
    {
        add => _screen.Clicked += value;
        remove => _screen.Clicked -= value;
    }

    /// <summary>Whether the debug button is drawn: a debug build's.</summary>
    public bool DebugButtons
    {
        get => _view.DebugButtons;
        set => _view.DebugButtons = value;
    }

    /// <summary>The debug button was clicked: a camp talk is to show now (<see cref="Core.GameSession.TriggerCampTalk"/>).</summary>
    public event Action? TalkRequested
    {
        add => _view.TalkRequested += value;
        remove => _view.TalkRequested -= value;
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
            _view.Opened();
        _view.Camp = camp;
        Visible = camp is not null;
        _view.QueueRedraw();
    }
}
