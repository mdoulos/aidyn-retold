using Godot;

namespace AidynRetold.Views;

/// <summary>Whose glove the cursor is: leather, or the metal gauntlet.</summary>
public enum CursorHand
{
    Leather,
    Metal,
}

/// <summary>
/// The mouse cursor as Alaron's glove, the user's art and design (Nov 6, 2026): a pointing hand, the same with a ring at its fingertip
/// for a moment as the left button goes down (<see cref="Click"/>), an open hand over what can be dragged and a fist while it's held, in leather
/// or in metal (<see cref="CursorHand"/>), as the game says each frame (<see cref="Show"/>). They fill Godot's Arrow, PointingHand and
/// Drag shapes, which the views pick between (<c>PartyTabView.CursorOver</c>). The art is 250 square (<c>acre:image/cursor_leather</c>,
/// <c>cursor_leather_click</c>, <c>cursor_leather_grab</c>, <c>cursor_leather_grabbed</c>, and the metal four); it's drawn small, as
/// Baldur's Gate 3's is, at the size the player's computer draws its own cursor (<see cref="Size"/>). Without the ACRE plugin the
/// cursor is the system's.
/// </summary>
public static class MouseCursors
{
    /// <summary>The left button went down, which the game notes from its events: the pointing hand shows its ring for <see cref="ClickFlash"/>.</summary>
    public static void Click() => _clickUntil = Time.GetTicksMsec() + ClickFlash;

    /// <summary>How long the ring shows after a press, in milliseconds: a flash, not the press's length, the user's choice (Nov 6, 2026).</summary>
    private const ulong ClickFlash = 120;

    private static ulong _clickUntil;

    /// <summary>Whether the pointing hand shows its ring now.</summary>
    private static bool Pressed => Time.GetTicksMsec() < _clickUntil;

    private static CursorHand? _hand;
    private static bool _pressed;
    private static int? _size;
    private static readonly Dictionary<string, (ImageTexture Texture, Vector2 Hotspot)?> Cursors = [];

    /// <summary>Puts the hand's cursors up, if they aren't already, for the hand and whether the button is down.</summary>
    public static void Show(CursorHand hand)
    {
        if (_hand == hand && _pressed == Pressed)
            return;
        (_hand, _pressed) = (hand, Pressed);
        var name = hand == CursorHand.Metal ? "metal" : "leather";
        Set(Input.CursorShape.Arrow, $"cursor_{name}{(Pressed ? "_click" : "")}", $"cursor_{name}");
        Set(Input.CursorShape.PointingHand, $"cursor_{name}_grab", null);
        Set(Input.CursorShape.Drag, $"cursor_{name}_grabbed", null);
    }

    /// <summary>
    /// A shape's cursor: the art's image at the cursor's size, its hotspot the fingertip of <paramref name="tipOf"/> (the plain pointing
    /// hand's, which the click's ring sits above) or, with none, the hand's middle.
    /// </summary>
    private static void Set(Input.CursorShape shape, string key, string? tipOf)
    {
        if (Cursor(key, tipOf) is not var (texture, hotspot))
            return;
        Input.SetCustomMouseCursor(texture, shape, hotspot);
    }

    private static (ImageTexture, Vector2)? Cursor(string key, string? tipOf)
    {
        if (Cursors.TryGetValue(key, out var made))
            return made;
        (ImageTexture, Vector2)? cursor = null;
        if (UiImages.ArtImage(key).Image?.Duplicate() is Image image && image.GetWidth() > 0)
        {
            var size = Size;
            var scale = (float)size / image.GetWidth();
            var hotspot = (tipOf is null ? Middle(image) : Fingertip(UiImages.ArtImage(tipOf).Image ?? image)) * scale;
            image.Resize(size, size, Image.Interpolation.Lanczos);
            cursor = (ImageTexture.CreateFromImage(image), hotspot.Round());
        }
        return Cursors[key] = cursor;
    }

    /// <summary>The fingertip: the middle of the topmost painted row.</summary>
    private static Vector2 Fingertip(Image image)
    {
        for (var y = 0; y < image.GetHeight(); y++)
        {
            var painted = Enumerable.Range(0, image.GetWidth()).Where(x => image.GetPixel(x, y).A > 0.5f).ToList();
            if (painted.Count > 0)
                return new Vector2((painted.Min() + painted.Max()) / 2f, y);
        }
        return Vector2.Zero;
    }

    /// <summary>The middle of what's painted.</summary>
    private static Vector2 Middle(Image image)
    {
        var box = image.GetUsedRect();
        return box.Size.X > 0 ? (Vector2)box.Position + (Vector2)box.Size / 2 : (Vector2)image.GetSize() / 2;
    }

    /// <summary>
    /// How big the cursor is, in the screen's pixels: the size the player's computer draws its own at (Windows' cursor size setting,
    /// 32 by default, up to 256), scaled with the screen, as the system's cursors are; 32 elsewhere, or where the setting can't be read.
    /// </summary>
    private static int Size
    {
        get
        {
            if (_size is { } size)
                return size;
            var set = 32;
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Cursors", "CursorBaseSize", null) is int read && read > 0)
                        set = read;
                }
                catch (Exception e) when (e is System.Security.SecurityException or IOException)
                {
                }
            }
            var dpi = DisplayServer.ScreenGetDpi();
            return (_size = Math.Clamp((int)Math.Round(set * (dpi > 0 ? dpi / 96f : 1)), 16, 256)).Value;
        }
    }
}
