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

    private static readonly RecordId Parchment = RecordId.Parse("acre:image/parchment");

    private readonly UiPainter _ui;
    private readonly PluginSet _plugins;
    /// <summary>Where each row and recipe was drawn, for the mouse: its area, its index and whether it's a recipe.</summary>
    private readonly List<(Rect2 Area, int Index, bool Recipe)> _hits = [];

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

    public override void _GuiInput(InputEvent e)
    {
        if (Camp is not { Choice: CampChoice.None, Talking: false } camp)
            return;
        if (e is InputEventMouseMotion motion)
        {
            if (Hit(motion.Position) is var (index, recipe) && (recipe ? camp.PointRecipe(index) : camp.Point(index)))
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
        else if (button.ButtonIndex == MouseButton.Left && Hit(button.Position) is var (index, recipe))
        {
            if (recipe ? camp.PointRecipe(index) : camp.Point(index))
                Click?.Invoke(MenuAction.Accept);
            AcceptEvent();
        }
    }

    private (int Index, bool Recipe)? Hit(Vector2 at)
    {
        foreach (var (area, index, recipe) in _hits)
        {
            if (area.HasPoint(at))
                return (index, recipe);
        }
        return null;
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
        DrawMenu(camp, site, fit, size);
        if (DebugButtons && camp.Choice == CampChoice.None)
            DrawButtons(size);
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

    /// <summary>Where the picture is drawn: as large as fits the screen, whole, centred, black either side.</summary>
    private static Rect2 Fit(CampsiteRecord? site, Vector2 size)
    {
        if (site is null || site.Width <= 0 || site.Height <= 0)
            return new Rect2(Vector2.Zero, size);
        var scale = Mathf.Min(size.X / site.Width, size.Y / site.Height);
        var drawn = new Vector2(site.Width * scale, site.Height * scale);
        return new Rect2((size - drawn) / 2, drawn);
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
            else
            {
                // No figure to draw yet: a marker on the anchor, with who stands there, so the slot can be judged all the same.
                var tall = (CampPoses.IsSeated(placement.Pose) ? 95 : 170) * pixel * placement.Scale;
                var wide = tall * 0.38f;
                var color = new Color(0.9f, 0.85f, 0.7f, 0.45f);
                DrawRect(new Rect2(foot.X - wide / 2, foot.Y - tall, wide, tall), color);
                if (!ShowOverlay)
                    continue;
                DrawCircle(foot, 2, Colors.White);
                _ui.TextSize = UiPainter.FineText;
                _ui.Text(this, _ui.Main, $"{MemberName(placement.Member)}, {Plain(placement.Pose)}{(placement.Mirrored ? " (mirrored)" : "")}", (int)(foot.X - wide / 2), (int)(foot.Y - tall) - 10, Button);
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
        var left = (int)band.Position.X + 12;
        var top = (int)band.Position.Y + 10;
        var columnWidth = (int)(band.Size.X * 0.46f);
        _ui.TextSize = UiPainter.StandardText;
        var row = _ui.Main.LineHeight;
        var y = top;
        var rows = camp.Rows;
        for (var i = 0; i < rows.Count; i++)
        {
            var item = rows[i];
            var current = i == camp.Cursor && camp.Open is null;
            var color = current ? Cursor : item.Enabled ? Text : Dim;
            _ui.TextSize = UiPainter.StandardText;
            if (current)
                Pointer(left, y + row / 2, color);
            _ui.Text(this, _ui.Main, item.Text, left + 6, y, color);
            if (item.Note is { } note)
            {
                _ui.TextSize = UiPainter.SmallText;
                _ui.Text(this, _ui.Main, note, left + columnWidth / 2, y + 1, Note);
            }
            _hits.Add((new Rect2(band.Position.X, y, columnWidth, row), i, false));
            y += row;
        }

        if (camp.Open is { } open)
        {
            var right = (int)band.Position.X + columnWidth + 12;
            var ry = top;
            _ui.TextSize = UiPainter.StandardText;
            _ui.Text(this, _ui.Main, $"{open.Activity.Name}: {camp.Member?.Name ?? "nobody"}  (C-left, C-right to change)", right, ry, Text);
            ry += row;
            _ui.TextSize = UiPainter.SmallText;
            var small = _ui.Main.LineHeight;
            if (camp.Recipes.Count == 0)
            {
                _ui.Text(this, _ui.Main, CampMenu.NothingToMake, right, ry, Dim);
            }
            for (var i = 0; i < camp.Recipes.Count; i++)
            {
                var current = i == camp.RecipeCursor;
                if (current)
                    Pointer(right, ry + small / 2, Cursor);
                _ui.Text(this, _ui.Main, camp.ProductName(camp.Recipes[i]), right + 6, ry, current ? Cursor : Text);
                _hits.Add((new Rect2(right, ry, band.End.X - right, small), i, true));
                ry += small;
            }
        }

        if (camp.Message is { } message)
        {
            _ui.TextSize = UiPainter.SmallText;
            _ui.Text(this, _ui.Main, message, left, (int)band.End.Y - _ui.Main.LineHeight - 2, Said);
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
        _view.Camp = camp;
        Visible = camp is not null;
        _view.QueueRedraw();
    }
}
