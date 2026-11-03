using Godot;

namespace AidynRetold.Editor;

/// <summary>The colours of one of the editor's modes, dark or light, which <see cref="EditorTheme"/> builds its theme from.</summary>
public sealed record Palette(
    Color Background, Color Surface, Color Raised, Color Band, Color Border, Color Text, Color Dim, Color Accent, Color OnAccent,
    Color Danger, Color Field, Color Hover, Color Selection, Color Changed, Color Problem, Color Good, Color Viewport, Color Backdrop);

/// <summary>
/// The editor's theme, dark by default or light, one look in both: thin borders and small radii, a band under each dialog's title,
/// filled primary buttons and outlined others, and its own drawn icons, so the check boxes and arrows suit either. The record
/// pages name what they show by type variations (<c>DimLabel</c>, <c>Chip</c>, <c>PrimaryButton</c> and the rest), so a change of
/// mode reaches every page; what's drawn by code reads <see cref="Current"/>. The mode is kept in <c>user://editor.cfg</c>.
/// </summary>
public static class EditorTheme
{
    public static readonly Palette Dark = new(
        Background: new(0.13f, 0.14f, 0.16f), Surface: new(0.17f, 0.18f, 0.21f), Raised: new(0.22f, 0.235f, 0.27f), Band: new(0.2f, 0.215f, 0.25f),
        Border: new(0.32f, 0.34f, 0.39f), Text: new(0.9f, 0.91f, 0.93f), Dim: new(0.62f, 0.64f, 0.68f), Accent: new(0.29f, 0.56f, 0.85f),
        OnAccent: new(1, 1, 1), Danger: new(0.75f, 0.26f, 0.25f), Field: new(0.12f, 0.13f, 0.15f), Hover: new(0.27f, 0.29f, 0.33f),
        Selection: new(0.24f, 0.38f, 0.56f), Changed: new(1f, 0.75f, 0.4f), Problem: new(1f, 0.5f, 0.45f), Good: new(0.55f, 0.85f, 0.55f),
        Viewport: new(0.16f, 0.17f, 0.2f), Backdrop: new(0, 0, 0, 0.45f));

    public static readonly Palette Light = new(
        Background: new(0.94f, 0.945f, 0.95f), Surface: new(0.985f, 0.985f, 0.99f), Raised: new(1, 1, 1), Band: new(0.965f, 0.968f, 0.97f),
        Border: new(0.8f, 0.81f, 0.83f), Text: new(0.11f, 0.14f, 0.15f), Dim: new(0.4f, 0.42f, 0.46f), Accent: new(0.13f, 0.44f, 0.69f),
        OnAccent: new(1, 1, 1), Danger: new(0.7f, 0.18f, 0.18f), Field: new(1, 1, 1), Hover: new(0.92f, 0.93f, 0.95f),
        Selection: new(0.8f, 0.88f, 0.96f), Changed: new(0.72f, 0.42f, 0.05f), Problem: new(0.75f, 0.2f, 0.18f), Good: new(0.2f, 0.55f, 0.25f),
        Viewport: new(0.78f, 0.8f, 0.83f), Backdrop: new(0, 0, 0, 0.35f));

    private const string Config = "user://editor.cfg";

    public static bool IsLight { get; private set; }

    public static Palette Current => IsLight ? Light : Dark;

    /// <summary>The mode changed, after the theme was set: what's drawn by code draws itself again.</summary>
    public static event Action? Changed;

    /// <summary>The mode last chosen, light or not.</summary>
    public static bool SavedLight()
    {
        var config = new ConfigFile();
        return config.Load(Config) == Error.Ok && config.GetValue("view", "light", false).AsBool();
    }

    private static readonly List<WeakReference<Godot.Environment>> Viewports = [];

    /// <summary>
    /// A 3D view's environment, whose background is the mode's <see cref="Palette.Viewport"/> now and whenever the mode changes.
    /// Returns it, for an initialiser.
    /// </summary>
    public static Godot.Environment Viewport(Godot.Environment environment)
    {
        environment.BackgroundColor = Current.Viewport;
        Viewports.RemoveAll(w => !w.TryGetTarget(out var e) || !GodotObject.IsInstanceValid(e));
        Viewports.Add(new WeakReference<Godot.Environment>(environment));
        return environment;
    }

    /// <summary>
    /// Sets <paramref name="window"/>'s theme, which its controls and dialogs take, to the mode's, and keeps the choice when
    /// <paramref name="keep"/> (not for a run's <c>--light</c>).
    /// </summary>
    public static void Use(Window window, bool light, bool keep = true)
    {
        IsLight = light;
        window.Theme = Build(Current);
        RenderingServer.SetDefaultClearColor(Current.Background);
        foreach (var w in Viewports)
        {
            if (w.TryGetTarget(out var environment) && GodotObject.IsInstanceValid(environment))
                environment.BackgroundColor = Current.Viewport;
        }
        if (keep)
        {
            var config = new ConfigFile();
            config.Load(Config);
            config.SetValue("view", "light", light);
            config.Save(Config);
        }
        Changed?.Invoke();
    }

    public static Theme Build(Palette p)
    {
        var t = new Theme();
        var bold = new FontVariation { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.6f };

        // Panels and the page.
        t.SetStylebox("panel", "Panel", Box(p.Surface));
        t.SetStylebox("panel", "PanelContainer", Box(p.Surface));
        t.SetColor("font_color", "Label", p.Text);
        t.SetColor("font_shadow_color", "Label", Colors.Transparent);

        // Buttons: outlined, a primary filled with the accent, a dangerous one with red.
        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            t.SetStylebox("normal", type, Box(p.Raised, p.Border, 3, 1, 10, 4));
            t.SetStylebox("hover", type, Box(p.Hover, p.Border, 3, 1, 10, 4));
            t.SetStylebox("pressed", type, Box(p.Selection, p.Accent, 3, 1, 10, 4));
            t.SetStylebox("disabled", type, Box(p.Surface, p.Border with { A = 0.5f }, 3, 1, 10, 4));
            t.SetStylebox("focus", type, Box(Colors.Transparent, p.Accent, 3, 1, 10, 4));
            t.SetColor("font_color", type, p.Text);
            t.SetColor("font_hover_color", type, p.Text);
            t.SetColor("font_pressed_color", type, p.Text);
            t.SetColor("font_hover_pressed_color", type, p.Text);
            t.SetColor("font_focus_color", type, p.Text);
            t.SetColor("font_disabled_color", type, p.Dim with { A = 0.6f });
        }
        t.SetIcon("arrow", "OptionButton", Chevron(p.Dim, down: true));
        Variation(t, "PrimaryButton", "Button", p.Accent, p.Accent.Lightened(0.12f), p.Accent.Darkened(0.15f), p.OnAccent);
        Variation(t, "DangerButton", "Button", p.Danger, p.Danger.Lightened(0.12f), p.Danger.Darkened(0.15f), p.OnAccent);
        t.SetTypeVariation("FlatButton", "Button");
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            t.SetStylebox(state, "FlatButton", Box(state == "hover" ? p.Hover : Colors.Transparent, null, 3, 0, 4, 2));
        t.SetColor("font_color", "LinkButton", p.Accent);
        t.SetColor("font_hover_color", "LinkButton", p.Accent.Lightened(0.2f));
        t.SetColor("font_pressed_color", "LinkButton", p.Accent.Darkened(0.2f));

        // Check boxes, radio buttons and the switch, drawn for the mode.
        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled", "hover_pressed" })
                t.SetStylebox(state, type, Box(state is "hover" or "hover_pressed" ? p.Hover : Colors.Transparent, null, 3, 0, 4, 2));
            t.SetColor("font_color", type, p.Text);
            t.SetColor("font_hover_color", type, p.Text);
            t.SetColor("font_pressed_color", type, p.Text);
            t.SetColor("font_hover_pressed_color", type, p.Text);
            t.SetColor("font_focus_color", type, p.Text);
            t.SetColor("font_disabled_color", type, p.Dim with { A = 0.6f });
        }
        t.SetIcon("unchecked", "CheckBox", CheckIcon(p, false, false));
        t.SetIcon("checked", "CheckBox", CheckIcon(p, true, false));
        t.SetIcon("radio_unchecked", "CheckBox", CheckIcon(p, false, true));
        t.SetIcon("radio_checked", "CheckBox", CheckIcon(p, true, true));
        t.SetIcon("unchecked_disabled", "CheckBox", CheckIcon(p with { Text = p.Dim with { A = 0.5f }, Accent = p.Dim }, false, false));
        t.SetIcon("checked_disabled", "CheckBox", CheckIcon(p with { Text = p.Dim with { A = 0.5f }, Accent = p.Dim }, true, false));
        t.SetIcon("unchecked", "CheckButton", Switch(p, false));
        t.SetIcon("checked", "CheckButton", Switch(p, true));

        // Fields.
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            t.SetStylebox("normal", type, Box(p.Field, p.Border, 3, 1, 8, 4));
            t.SetStylebox("focus", type, Box(Colors.Transparent, p.Accent, 3, 1, 8, 4));
            t.SetStylebox("read_only", type, Box(p.Surface, p.Border, 3, 1, 8, 4));
            t.SetColor("font_color", type, p.Text);
            t.SetColor("font_placeholder_color", type, p.Dim);
            t.SetColor("caret_color", type, p.Text);
            t.SetColor("selection_color", type, p.Selection);
            t.SetColor("font_readonly_color", type, p.Dim);
        }

        // Lists and trees.
        t.SetStylebox("panel", "Tree", Box(p.Field, p.Border, 3, 1, 2, 2));
        t.SetStylebox("focus", "Tree", Box(Colors.Transparent, p.Accent, 3, 1));
        t.SetStylebox("selected", "Tree", Box(p.Selection));
        t.SetStylebox("selected_focus", "Tree", Box(p.Selection));
        t.SetStylebox("hovered", "Tree", Box(p.Hover));
        t.SetStylebox("cursor", "Tree", Box(Colors.Transparent, p.Accent, 0, 1));
        t.SetStylebox("cursor_unfocused", "Tree", Box(Colors.Transparent, p.Border, 0, 1));
        foreach (var state in new[] { "title_button_normal", "title_button_hover", "title_button_pressed" })
            t.SetStylebox(state, "Tree", Box(state == "title_button_hover" ? p.Hover : p.Band, p.Border, 0, 0, 6, 4, bottom: 1));
        t.SetColor("font_color", "Tree", p.Text);
        t.SetColor("font_selected_color", "Tree", p.Text);
        t.SetColor("title_button_color", "Tree", p.Text);
        t.SetColor("guide_color", "Tree", p.Border with { A = 0.5f });
        t.SetColor("relationship_line_color", "Tree", p.Border);
        t.SetColor("children_hl_line_color", "Tree", p.Border);
        t.SetColor("parent_hl_line_color", "Tree", p.Border);
        t.SetIcon("arrow", "Tree", Chevron(p.Dim, down: true));
        t.SetIcon("arrow_collapsed", "Tree", Chevron(p.Dim, down: false));
        t.SetStylebox("panel", "PopupMenu", Box(p.Raised, p.Border, 4, 1, 4, 4));
        t.SetStylebox("hover", "PopupMenu", Box(p.Selection, null, 3));
        t.SetColor("font_color", "PopupMenu", p.Text);
        t.SetColor("font_hover_color", "PopupMenu", p.Text);
        t.SetColor("font_disabled_color", "PopupMenu", p.Dim);
        t.SetColor("font_separator_color", "PopupMenu", p.Dim);
        t.SetStylebox("separator", "PopupMenu", Line(p.Border, vertical: false));
        t.SetIcon("checked", "PopupMenu", CheckIcon(p, true, false));
        t.SetIcon("unchecked", "PopupMenu", CheckIcon(p, false, false));
        t.SetIcon("radio_checked", "PopupMenu", CheckIcon(p, true, true));
        t.SetIcon("radio_unchecked", "PopupMenu", CheckIcon(p, false, true));
        t.SetIcon("submenu", "PopupMenu", Chevron(p.Dim, down: false));

        // Tabs: the selected one underlined with the accent.
        foreach (var type in new[] { "TabBar", "TabContainer" })
        {
            t.SetStylebox("tab_selected", type, Box(p.Surface, p.Accent, 0, 0, 12, 5, bottom: 2));
            t.SetStylebox("tab_unselected", type, Box(Colors.Transparent, null, 0, 0, 12, 5));
            t.SetStylebox("tab_hovered", type, Box(p.Hover, null, 0, 0, 12, 5));
            t.SetStylebox("tab_disabled", type, Box(Colors.Transparent, null, 0, 0, 12, 5));
            t.SetStylebox("tab_focus", type, Box(Colors.Transparent, p.Accent, 0, 1));
            t.SetColor("font_selected_color", type, p.Text);
            t.SetColor("font_unselected_color", type, p.Dim);
            t.SetColor("font_hovered_color", type, p.Text);
        }
        t.SetStylebox("panel", "TabContainer", Box(p.Surface));

        // Scroll bars, splits and separators.
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", type, Box(Colors.Transparent, null, 0, 0, 2, 2));
            t.SetStylebox("scroll_focus", type, Box(Colors.Transparent, null, 0, 0, 2, 2));
            t.SetStylebox("grabber", type, Box(p.Border, null, 4));
            t.SetStylebox("grabber_highlight", type, Box(p.Dim, null, 4));
            t.SetStylebox("grabber_pressed", type, Box(p.Accent, null, 4));
        }
        t.SetStylebox("separator", "HSeparator", Line(p.Border, vertical: false));
        t.SetStylebox("separator", "VSeparator", Line(p.Border, vertical: true));
        t.SetStylebox("panel", "TooltipPanel", Box(p.Raised, p.Border, 3, 1, 8, 5));
        t.SetColor("font_color", "TooltipLabel", p.Text);
        t.SetStylebox("embedded_border", "Window", Box(p.Surface, p.Border, 4, 1, 0, 0));
        t.SetColor("title_color", "Window", p.Text);

        // The states graph.
        t.SetStylebox("panel", "GraphEdit", Box(p.Field));
        t.SetColor("grid_major", "GraphEdit", p.Border with { A = 0.4f });
        t.SetColor("grid_minor", "GraphEdit", p.Border with { A = 0.2f });
        t.SetStylebox("panel", "GraphNode", Box(p.Surface, p.Border, 0, 1, 10, 4, top: 0, bottomRadius: 4));
        t.SetStylebox("panel_selected", "GraphNode", Box(p.Surface, p.Accent, 0, 2, 10, 4, top: 0, bottomRadius: 4));
        t.SetStylebox("titlebar", "GraphNode", Box(p.Band, p.Border, 0, 1, 10, 4, topRadius: 4));
        t.SetStylebox("titlebar_selected", "GraphNode", Box(p.Selection, p.Accent, 0, 2, 10, 4, topRadius: 4));
        t.SetColor("font_color", "GraphNodeTitleLabel", p.Text);

        // What the pages name by variation.
        Label(t, "DimLabel", p.Dim);
        Label(t, "ChangedLabel", p.Changed);
        Label(t, "ProblemLabel", p.Problem);
        Label(t, "GoodLabel", p.Good);
        Label(t, "TitleLabel", p.Text, 20, bold);
        Label(t, "DialogTitleLabel", p.Text, 15, bold);
        Label(t, "ChipLabel", p.Text, 13);
        Label(t, "ChangedChipLabel", p.Changed, 13);
        t.SetTypeVariation("Chip", "PanelContainer");
        t.SetStylebox("panel", "Chip", Box(p.Raised, p.Border, 4, 1, 7, 1));
        t.SetTypeVariation("ChangedChip", "PanelContainer");
        t.SetStylebox("panel", "ChangedChip", Box(p.Raised, p.Changed, 4, 1, 7, 1));
        t.SetTypeVariation("Toolbar", "PanelContainer");
        t.SetStylebox("panel", "Toolbar", Box(p.Band, p.Border, 0, 0, 8, 4, bottom: 1));
        t.SetTypeVariation("DialogBox", "PanelContainer");
        var dialog = Box(p.Surface, p.Accent, 4, 1);
        dialog.ShadowColor = new Color(0, 0, 0, 0.25f);
        dialog.ShadowSize = 24;
        dialog.ShadowOffset = new Vector2(0, 8);
        t.SetStylebox("panel", "DialogBox", dialog);
        t.SetTypeVariation("DialogTitle", "PanelContainer");
        t.SetStylebox("panel", "DialogTitle", Box(p.Band, p.Border, 0, 0, 14, 10, bottom: 1, topRadius: 4));
        t.SetTypeVariation("SectionTitle", "Button");
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" })
            t.SetStylebox(state, "SectionTitle", Box(state is "hover" or "hover_pressed" ? p.Hover : Colors.Transparent, null, 3, 0, 4, 3));
        t.SetFontSize("font_size", "SectionTitle", 15);
        t.SetFont("font", "SectionTitle", bold);
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            t.SetColor(c, "SectionTitle", p.Text);
        return t;
    }

    private static void Label(Theme t, string name, Color color, int? size = null, Font? font = null)
    {
        t.SetTypeVariation(name, "Label");
        t.SetColor("font_color", name, color);
        if (size is { } s)
            t.SetFontSize("font_size", name, s);
        if (font is not null)
            t.SetFont("font", name, font);
    }

    private static void Variation(Theme t, string name, string type, Color fill, Color hover, Color pressed, Color text)
    {
        t.SetTypeVariation(name, type);
        t.SetStylebox("normal", name, Box(fill, fill, 3, 1, 12, 4));
        t.SetStylebox("hover", name, Box(hover, hover, 3, 1, 12, 4));
        t.SetStylebox("pressed", name, Box(pressed, pressed, 3, 1, 12, 4));
        t.SetStylebox("focus", name, Box(Colors.Transparent, text with { A = 0.8f }, 3, 1, 12, 4));
        t.SetStylebox("disabled", name, Box(fill with { A = 0.45f }, null, 3, 0, 12, 4));
        foreach (var c in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            t.SetColor(c, name, text);
        t.SetColor("font_disabled_color", name, text with { A = 0.6f });
    }

    /// <summary>A flat box: its fill, its border all round or at its top or bottom alone, its radius and its margins.</summary>
    private static StyleBoxFlat Box(Color fill, Color? border = null, int radius = 0, int width = 0, float marginX = 0, float marginY = 0,
        int? top = null, int? bottom = null, int? topRadius = null, int? bottomRadius = null)
    {
        var box = new StyleBoxFlat { BgColor = fill, ContentMarginLeft = marginX, ContentMarginRight = marginX, ContentMarginTop = marginY, ContentMarginBottom = marginY };
        // A border of width all round, its top's or bottom's own where given.
        if (border is { } b)
        {
            box.BorderColor = b;
            box.BorderWidthLeft = box.BorderWidthRight = width;
            box.BorderWidthTop = top ?? width;
            box.BorderWidthBottom = bottom ?? width;
        }
        var upper = topRadius ?? radius;
        var lower = bottomRadius ?? radius;
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = upper;
        box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = lower;
        return box;
    }

    private static StyleBoxLine Line(Color color, bool vertical) => new() { Color = color, Thickness = 1, Vertical = vertical };

    // ---- Icons, drawn for the mode by their shapes' distances ------------------------------------------------------------------

    private static ImageTexture Draw(int width, int height, Func<Vector2, Color> pixel)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                image.SetPixel(x, y, pixel(new Vector2(x + 0.5f, y + 0.5f)));
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static float Cover(float distance) => Math.Clamp(0.5f - distance, 0, 1);

    private static float RoundRect(Vector2 p, Vector2 centre, Vector2 half, float radius)
    {
        var q = (p - centre).Abs() - half + new Vector2(radius, radius);
        return new Vector2(Math.Max(q.X, 0), Math.Max(q.Y, 0)).Length() + Math.Min(Math.Max(q.X, q.Y), 0) - radius;
    }

    private static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        var pa = p - a;
        var ba = b - a;
        var h = Math.Clamp(pa.Dot(ba) / ba.Dot(ba), 0, 1);
        return (pa - ba * h).Length();
    }

    private static Color Over(Color under, Color over, float cover)
    {
        var a = over.A * cover;
        var outA = a + under.A * (1 - a);
        return outA <= 0 ? Colors.Transparent
            : new Color((over.R * a + under.R * under.A * (1 - a)) / outA, (over.G * a + under.G * under.A * (1 - a)) / outA,
                (over.B * a + under.B * under.A * (1 - a)) / outA, outA);
    }

    /// <summary>A check box or a radio button, 16 square: an outline, or filled with the accent and marked.</summary>
    private static ImageTexture CheckIcon(Palette p, bool on, bool radio) => Draw(16, 16, at =>
    {
        var c = new Vector2(8, 8);
        var shape = radio ? (at - c).Length() - 6.5f : RoundRect(at, c, new Vector2(6.5f, 6.5f), 2);
        var color = Colors.Transparent;
        if (on)
        {
            color = Over(color, p.Accent, Cover(shape));
            var mark = radio ? (at - c).Length() - 2.8f : Math.Min(Segment(at, new Vector2(4.5f, 8.3f), new Vector2(7, 10.8f)), Segment(at, new Vector2(7, 10.8f), new Vector2(11.6f, 5.2f))) - 1f;
            color = Over(color, p.OnAccent, Cover(mark));
        }
        else
        {
            color = Over(color, p.Field, Cover(shape));
            color = Over(color, p.Dim, Cover(Math.Abs(shape + 0.6f) - 0.6f));
        }
        return color;
    });

    /// <summary>A switch, 34 by 18: its track the accent when on, its knob at the end it's at.</summary>
    private static ImageTexture Switch(Palette p, bool on) => Draw(34, 18, at =>
    {
        var track = RoundRect(at, new Vector2(17, 9), new Vector2(15.5f, 7.5f), 7.5f);
        var knob = (at - new Vector2(on ? 25 : 9, 9)).Length() - 5.5f;
        var color = Over(Colors.Transparent, on ? p.Accent : p.Border, Cover(track));
        return Over(color, on ? p.OnAccent : p.Raised, Cover(knob));
    });

    /// <summary>A chevron, 12 square, pointing down or right.</summary>
    private static ImageTexture Chevron(Color color, bool down) => Draw(12, 12, at =>
    {
        var (a, b, c) = down ? (new Vector2(3, 4.5f), new Vector2(6, 7.5f), new Vector2(9, 4.5f)) : (new Vector2(4.5f, 3), new Vector2(7.5f, 6), new Vector2(4.5f, 9));
        return Over(Colors.Transparent, color, Cover(Math.Min(Segment(at, a, b), Segment(at, b, c)) - 0.8f));
    });
}
