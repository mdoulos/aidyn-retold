using AidynEnhanced.Core.Ui;
using Godot;

namespace AidynEnhanced.Views;

/// <summary>
/// A low-resolution screen that UI is drawn on and then scaled up to fill this control: the
/// original's 320 × 240, or taller for smaller UI, and as wide as the control's shape. Views that
/// draw UI go in <see cref="Canvas"/>, in its pixels, and lay themselves out by its size.
/// </summary>
/// <remarks>
/// The original draws its UI images pixel for pixel: they're texture rectangles with bilinear
/// filtering on, which at 1:1 samples each texel exactly, and only a scaled image, such as the
/// conversation box's gradient, is filtered. Under the Smooth and Video Out filters the canvas
/// filters linearly, as the console does, and the screen is drawn at its own size and scaled up
/// afterwards by <see cref="Filter"/>. Under Sharp it's drawn at the window's resolution
/// (<see cref="Oversampling"/>), each of its pixels as a square of the window's, where an image drawn
/// at its own size is magnified pixel for pixel and a scaled one is filtered (<see cref="UiPainter"/>).
/// <see cref="Lines"/> sets its height: 240 is the original's, and 480 draws everything half the
/// size, with room for twice as much. It's never narrower than 4:3; a narrower control shows it
/// letterboxed.
/// </remarks>
public partial class UiScreen : Control
{
    /// <summary>The original's screen.</summary>
    public const int Width = 320, Height = 240;

    // The canvas is premultiplied: alpha blending into a transparent target leaves colour times alpha.
    private const string ShaderCode = """
        shader_type canvas_item;
        render_mode blend_premul_alpha;

        uniform int mode = 1;

        vec4 texel(sampler2D screen, ivec2 at, ivec2 size) {
            return texelFetch(screen, clamp(at, ivec2(0), size - 1), 0);
        }

        void fragment() {
            ivec2 size = textureSize(TEXTURE, 0);
            vec2 at = UV * vec2(size);
            int y = int(at.y);
            if (mode == 0) {
                COLOR = texel(TEXTURE, ivec2(int(at.x), y), size);
            } else if (mode == 1) {
                int column = int(at.x * 2.0);
                int x = column / 2;
                vec4 left = texel(TEXTURE, ivec2(x, y), size);
                COLOR = column % 2 == 0 ? left : (left + texel(TEXTURE, ivec2(x + 1, y), size)) * 0.5;
            } else {
                COLOR = texture(TEXTURE, UV);
            }
        }
        """;

    private static readonly Shader Shader = new() { Code = ShaderCode };

    private readonly SubViewport _viewport = new()
    {
        Size = new Vector2I(Width, Height),
        TransparentBg = true,
        Disable3D = true,
        CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Linear,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
    };

    private readonly ShaderMaterial _material = new() { Shader = Shader };
    private readonly ColorRect _backdrop = new() { Name = "Backdrop", Size = new Vector2(Width, Height), Visible = false, MouseFilter = MouseFilterEnum.Ignore };
    private UiFilter _filter;
    private int _lines = Height;
    private Vector2I _size = new(Width, Height);

    public UiScreen()
    {
        Name = "UI screen";
        MouseFilter = MouseFilterEnum.Ignore;
        TextureFilter = TextureFilterEnum.Linear;
        Material = _material;
        Canvas = new Control { Name = "Canvas", Size = new Vector2(Width, Height), MouseFilter = MouseFilterEnum.Ignore };
        Canvas.AddChild(_backdrop);
        _viewport.AddChild(Canvas);
        AddChild(_viewport);
        Filter = UiFilter.Pixels;
    }

    /// <summary>What views draw in, in the screen's pixels.</summary>
    public Control Canvas { get; }

    /// <summary>How many pixels tall the screen is.</summary>
    public int Lines
    {
        get => _lines;
        set
        {
            _lines = Math.Max(value, 1);
            Fit();
        }
    }

    /// <summary>The screen's size in its pixels.</summary>
    public Vector2I CanvasSize => _size;

    /// <summary>
    /// How many of the window's pixels across and down each of the screen's is drawn with: under Sharp, the least whole number
    /// that fills the control's height on the window, and else 1. The views draw in the screen's pixels whatever it is; what draws
    /// a picture of its own for the screen, as the pause menu's scroll does, draws it this much finer.
    /// </summary>
    public int Oversampling { get; private set; } = 1;

    public UiFilter Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            Fit();
        }
    }

    /// <summary>What's drawn behind the UI on the screen, if anything: the editor shows it over a plain colour.</summary>
    public Color Backdrop
    {
        get => _backdrop.Color;
        set
        {
            _backdrop.Color = value;
            _backdrop.Visible = value.A > 0;
        }
    }

    /// <summary>Where the screen is drawn: as large as fits, centred on whole pixels, so a screen drawn at the window's resolution is drawn 1:1.</summary>
    public Rect2 ScreenRect
    {
        get
        {
            var canvas = (Vector2)CanvasSize;
            var scale = Math.Min(Size.X / canvas.X, Size.Y / canvas.Y);
            var size = canvas * scale;
            return new Rect2(((Size - size) / 2).Round(), size);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized || what == NotificationEnterTree)
            Fit();
    }

    /// <summary>
    /// Sizes the screen to <see cref="Lines"/> and this control's shape, and its viewport to <see cref="Oversampling"/> times that,
    /// its 2D drawn at the screen's size stretched over it. Drawn finer than the window, it's shrunk to fit smoothly.
    /// </summary>
    private void Fit()
    {
        var minimum = (int)Math.Round(_lines * 4 / 3.0);
        var width = Size.Y > 0 ? Math.Max(minimum, (int)Math.Round(_lines * Size.X / Size.Y)) : minimum;
        var size = new Vector2I(width, _lines);
        var window = IsInsideTree() ? Size.Y * GetGlobalTransformWithCanvas().Scale.Y : Size.Y;
        Oversampling = _filter == UiFilter.Pixels ? Math.Max(1, (int)Math.Ceiling(window / _lines - 0.01f)) : 1;
        if (_size != size || _viewport.Size != size * Oversampling)
        {
            _size = size;
            _viewport.Size = size * Oversampling;
            _viewport.Size2DOverride = size;
            _viewport.Size2DOverrideStretch = true;
            Canvas.Size = size;
            _backdrop.Size = size;
        }
        _viewport.CanvasItemDefaultTextureFilter = Oversampling > 1 ? Viewport.DefaultCanvasItemTextureFilter.Nearest : Viewport.DefaultCanvasItemTextureFilter.Linear;
        _material.SetShaderParameter("mode", (int)(Oversampling > 1 ? UiFilter.Smooth : _filter));
        QueueRedraw();
    }

    /// <summary>
    /// Whether the mouse reaches the views on the screen, its position mapped into the screen's
    /// pixels. Off by default, so the screen doesn't take the mouse from what's under it. On, it passes rather than stops: Godot
    /// stops every pointer event at a control that stops whether or not it was taken, and what the views pass over is the game's.
    /// </summary>
    public bool ForwardsMouse
    {
        get => MouseFilter != MouseFilterEnum.Ignore;
        set => MouseFilter = value ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// A click on a row of a view on the screen, which the view has put the cursor on (<see cref="Click"/>): the button it presses, A,
    /// for whoever plays the menu to send on as if pressed. A click elsewhere presses nothing.
    /// </summary>
    public event Action<MenuAction>? Clicked;

    /// <summary>A view on the screen was clicked on a row (<see cref="Clicked"/>).</summary>
    public void Click(MenuAction action) => Clicked?.Invoke(action);

    /// <summary>The screen a view draws on, for it to report a click by (<see cref="Click"/>), or null.</summary>
    public static UiScreen? Of(Node view) => view.GetViewport()?.GetParent() as UiScreen;

    /// <summary>A point in the screen's own coordinates as the screen's pixels, or null off it.</summary>
    public Vector2? ToCanvas(Vector2 local)
    {
        var rect = ScreenRect;
        if (rect.Size.X <= 0 || rect.Size.Y <= 0 || !rect.HasPoint(local))
            return null;
        return (local - rect.Position) / rect.Size * (Vector2)CanvasSize;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouse mouse)
            return;
        var rect = ScreenRect;
        if (rect.Size.X <= 0 || rect.Size.Y <= 0)
            return;
        var copy = (InputEventMouse)mouse.Duplicate();
        copy.Position = (mouse.Position - rect.Position) / rect.Size * (Vector2)CanvasSize;
        copy.GlobalPosition = copy.Position;
        _viewport.PushInput(copy, true);
        // Only what the screen's views took stops here: a click or the wheel they pass over goes on to the game's unhandled input,
        // where the Controls page's prompt binds it and a menu control bound to it acts.
        if (_viewport.IsInputHandled())
            AcceptEvent();
    }

    /// <summary>The screen as it's drawn, in its own pixels, before it's scaled up: drawn finer, each square's top-left pixel.</summary>
    public Image Capture()
    {
        var image = _viewport.GetTexture().GetImage();
        if (Oversampling > 1)
            image.Resize(_size.X, _size.Y, Image.Interpolation.Nearest);
        return image;
    }

    public override void _Draw() => DrawTextureRect(_viewport.GetTexture(), ScreenRect, false);
}
