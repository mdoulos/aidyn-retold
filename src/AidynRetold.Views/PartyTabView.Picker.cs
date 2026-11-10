using AidynRetold.Core.Ui;
using AidynRetold.Plugins.Records;
using Godot;

namespace AidynRetold.Views;

/// <summary>The dialog that changes a portrait (<see cref="PortraitPicker"/>): the colour behind it and what it wears, the user's design (Nov 9, 2026).</summary>
public partial class PartyTabView
{
    private const float PickerWidth = 178, PickerHeight = 112, PickerPortrait = 54, PickerPortraitTop = 19, PickerArrow = 18, PickerArrowGap = 12,
        PickerSwatch = 12, PickerSwatchGap = 2.6f, PickerSwatchTop = 11, PickerTitleTop = 6, PickerOverlayRow = 11, PickerOverlayTop = 5, PickerOverlayWidth = 100, PickerBox = 7.5f;

    private const string PickerTitle = "Portrait";

    /// <summary>The mouse's place on the dialog's arrows, and on an overlay's row, <see cref="PickerOnOverlays"/> less its index: a swatch's index is itself.</summary>
    private const int PickerOnPrevious = -1, PickerOnNext = -2, PickerOnOverlays = -3;

    private int? _pickerHover;

    private readonly record struct PickerLayout(Rect2 Paper, Rect2 Portrait, Rect2 Previous, Rect2 Next, Rect2[] Swatches, Rect2[] Overlays);

    /// <summary>Where the dialog's parts are, centred on the screen.</summary>
    private PickerLayout PickerAt(int backgrounds, int overlays)
    {
        var height = PickerHeight + overlays * PickerOverlayRow;
        var paper = new Rect2((Size - new Vector2(PickerWidth, height)) / 2, PickerWidth, height);
        var middle = paper.Position.X + PickerWidth / 2;
        var portrait = new Rect2(middle - PickerPortrait / 2, paper.Position.Y + PickerPortraitTop, PickerPortrait, PickerPortrait);
        var arrowTop = portrait.Position.Y + (PickerPortrait - PickerArrow) / 2;
        var row = backgrounds * PickerSwatch + (backgrounds - 1) * PickerSwatchGap;
        var swatches = Enumerable.Range(0, backgrounds).Select(i => new Rect2(middle - row / 2 + i * (PickerSwatch + PickerSwatchGap),
            portrait.End.Y + PickerSwatchTop, PickerSwatch, PickerSwatch)).ToArray();
        var overlayTop = portrait.End.Y + PickerSwatchTop + PickerSwatch + PickerOverlayTop;
        var rows = Enumerable.Range(0, overlays).Select(i => new Rect2(middle - PickerOverlayWidth / 2, overlayTop + i * PickerOverlayRow, PickerOverlayWidth, PickerOverlayRow)).ToArray();
        return new PickerLayout(paper, portrait, new Rect2(portrait.Position.X - PickerArrowGap - PickerArrow, arrowTop, PickerArrow, PickerArrow),
            new Rect2(portrait.End.X + PickerArrowGap, arrowTop, PickerArrow, PickerArrow), swatches, rows);
    }

    /// <summary>What of the dialog is under a point: an arrow, an overlay's row, or a swatch's index; null for none of them.</summary>
    private static int? PickerHit(PickerLayout layout, Vector2 point)
    {
        if (layout.Previous.HasPoint(point))
            return PickerOnPrevious;
        if (layout.Next.HasPoint(point))
            return PickerOnNext;
        for (var i = 0; i < layout.Overlays.Length; i++)
            if (layout.Overlays[i].HasPoint(point))
                return PickerOnOverlays - i;
        for (var i = 0; i < layout.Swatches.Length; i++)
            if (layout.Swatches[i].Grow(PickerSwatchGap / 2).HasPoint(point))
                return i;
        return null;
    }

    /// <summary>
    /// The mouse and the dialog: a right click on a portrait that can be recoloured opens it; while it's up the arrows and the swatches
    /// change the colour, and a click off it or a right click closes it. True if the event was the dialog's.
    /// </summary>
    private bool PickerInput(InputEvent e)
    {
        if (!OwnTab || e is not InputEventMouse mouse)
            return false;
        if (Menu.Picker is not { } picker)
        {
            _pickerHover = null;
            return e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && OpenPickerAt(mouse.Position);
        }
        var layout = PickerAt(picker.Backgrounds.Count, picker.Overlays.Count);
        if (e is InputEventMouseMotion)
        {
            var over = PickerHit(layout, mouse.Position);
            if (over != _pickerHover)
            {
                _pickerHover = over;
                QueueRedraw();
            }
            return true;
        }
        if (e is not InputEventMouseButton button)
            return false;
        var screen = UiScreen.Of(this);
        if (button.ButtonIndex == MouseButton.Right)
        {
            if (button.Pressed)
                screen?.Click(MenuAction.Back);
            return true;
        }
        if (button.ButtonIndex != MouseButton.Left)
            return false;
        if (!button.Pressed)
            return true;
        switch (PickerHit(layout, mouse.Position))
        {
            case PickerOnPrevious:
                picker.FocusColors();
                screen?.Click(MenuAction.Left);
                break;
            case PickerOnNext:
                picker.FocusColors();
                screen?.Click(MenuAction.Right);
                break;
            case <= PickerOnOverlays and var row:
                picker.AimAtOverlay(PickerOnOverlays - row);
                screen?.Click(MenuAction.Accept);
                break;
            case { } swatch when swatch != picker.Selected:
                picker.AimAt(swatch);
                screen?.Click(MenuAction.Right);
                break;
            case null when !layout.Paper.HasPoint(mouse.Position):
                screen?.Click(MenuAction.Back);
                break;
        }
        QueueRedraw();
        return true;
    }

    /// <summary>Opens the dialog for the portrait under a point, of any member shown. True if there was a portrait, so the right click isn't B.</summary>
    private bool OpenPickerAt(Vector2 point)
    {
        if (Menu.Shown is null || Menu.HasPopup || Menu.OriginalLook || Menu.Slide is not null)
            return false;
        var at = point - (Vector2)_origin - (Vector2)PortraitsAt;
        var order = Menu.PortraitOrder;
        for (var i = 0; i < order.Count; i++)
        {
            var (place, size) = PortraitPlace(i);
            if (!new Rect2(place, new Vector2(size, size)).HasPoint(at))
                continue;
            // A portrait that can't be recoloured yet does nothing, rather than the right click closing the menu.
            if (Menu.Party.Slots[order[i]] is { } member && Menu.OpenPicker(member))
                QueueRedraw();
            return true;
        }
        return false;
    }

    /// <summary>The dialog: a slip of parchment with the portrait between its arrows and a swatch for each colour under it.</summary>
    private void PickerArt(PortraitPicker picker)
    {
        var layout = PickerAt(picker.Backgrounds.Count, picker.Overlays.Count);
        _ui.Slip(_canvas, layout.Paper);
        var font = NameFont;
        _ui.Text(_canvas, font, PickerTitle, layout.Paper.Position.X + (PickerWidth - font.ExactWidth(PickerTitle)) / 2,
            (int)(layout.Paper.Position.Y + PickerTitleTop), PartyMenu.TextColor);
        if (picker.Member.Record.Portrait is { } portrait)
            FramedPortrait(portrait, layout.Portrait);
        PickerTriangle(layout.Previous, pointsLeft: true, _pickerHover == PickerOnPrevious);
        PickerTriangle(layout.Next, pointsLeft: false, _pickerHover == PickerOnNext);
        for (var i = 0; i < layout.Swatches.Length; i++)
        {
            var swatch = layout.Swatches[i];
            _ui.Image(_canvas, picker.Backgrounds[i], swatch);
            if (i == picker.Selected)
                _canvas.DrawRect(swatch.Grow(0.5f), _ui.Tint(PickerChosen), false, 1.1f);
            else
                _canvas.DrawRect(swatch, _ui.Tint(_pickerHover == i ? PickerHovered : PartyMenu.TextColor), false, _pickerHover == i ? 0.8f : 0.35f);
        }
        for (var i = 0; i < layout.Overlays.Length; i++)
        {
            var row = layout.Overlays[i];
            var box = new Rect2(row.Position.X + 4, row.Position.Y + (row.Size.Y - PickerBox) / 2, PickerBox, PickerBox);
            var hovered = _pickerHover == PickerOnOverlays - i;
            _canvas.DrawRect(box, _ui.Tint(hovered ? PickerHovered : PartyMenu.TextColor), false, hovered ? 0.8f : 0.5f);
            if (picker.Wears(i))
                _canvas.DrawRect(box.Grow(-1.7f), _ui.Tint(PickerChosen));
            _ui.Text(_canvas, font, picker.Overlays[i].Name, box.End.X + 5, (int)row.Position.Y, PartyMenu.TextColor);
            if (picker.Row == i + 1)
                _canvas.DrawRect(row, _ui.Tint(PickerChosen), false, 0.7f);
        }
    }

    private static readonly Rgba PickerChosen = new(214, 164, 58, 255), PickerHovered = new(150, 50, 30, 255);

    private void PickerTriangle(Rect2 box, bool pointsLeft, bool hovered)
    {
        var (tip, back) = pointsLeft ? (box.Position.X + 4, box.End.X - 4) : (box.End.X - 4, box.Position.X + 4);
        var middle = box.Position.Y + box.Size.Y / 2;
        _canvas.DrawColoredPolygon([new Vector2(tip, middle), new Vector2(back, middle - 6.5f), new Vector2(back, middle + 6.5f)],
            _ui.Tint(hovered ? PickerHovered : PartyMenu.TextColor));
    }
}
