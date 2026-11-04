using AidynRetold.Core.Ui;
using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// How a page's UI screen is shown, to compare with the console: the filter that scales it up, how
/// large the preview is, the window's shape and how many lines the screen has.
/// </summary>
public partial class UiScreenOptions : HFlowContainer
{
    private readonly UiScreen _screen;
    private readonly OptionButton _filter, _aspect, _lines;
    // None where the page sizes the screen itself, as a script's page fits its box to its width.
    private readonly OptionButton? _zoom;
    private int _scale = 1;
    private bool _wide;

    /// <param name="shape">What the window's shape changes, for its tooltip.</param>
    /// <param name="zoom">Whether it offers how large the preview is, and sizes the screen to it: not where the page sizes it.</param>
    public UiScreenOptions(UiScreen screen, string shape, bool zoom = true)
    {
        _screen = screen;
        // The screen keeps the shape chosen, rather than the page's.
        _screen.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _screen.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        AddThemeConstantOverride("h_separation", 6);
        AddChild(_filter = Choice("How the UI screen is scaled up to the window.",
            [((int)UiFilter.VideoOut, "As the console outputs it"), ((int)UiFilter.Pixels, "Sharp pixels"), ((int)UiFilter.Smooth, "Smooth")],
            id => _screen.Filter = (UiFilter)id));
        if (zoom)
        {
            AddChild(_zoom = Choice("How large the preview is.", [(1, "1×"), (2, "2×"), (3, "3×")], id =>
            {
                _scale = id;
                Fit();
            }));
        }
        AddChild(_aspect = Choice($"The window's shape: {shape}", [(0, "4:3"), (1, "16:9")], id =>
        {
            _wide = id == 1;
            Fit();
        }));
        AddChild(_lines = Choice("How many pixels tall the UI screen is: 240 is the original's, and more draws the UI smaller.",
            [(240, "240 lines"), (360, "360 lines"), (480, "480 lines")], id => _screen.Lines = id));
        // The choices say what the screen draws to begin with: its own filter and lines.
        Show(_filter, (int)screen.Filter);
        Show(_lines, screen.Lines);
        Fit();
    }

    private static void Show(OptionButton choice, int id)
    {
        if (choice.GetItemIndex(id) is var i and >= 0)
            choice.Select(i);
    }

    /// <summary>Whether the window's shape is 16:9 rather than 4:3.</summary>
    public bool Wide => _wide;

    /// <summary>Sets the options (<c>--zoom</c>, <c>--ui-filter</c>, <c>--aspect</c>, <c>--ui-lines</c>). Ones not offered are ignored.</summary>
    public void Set(int? zoom, UiFilter? filter, bool? wide, int? lines)
    {
        Pick(_zoom, zoom);
        Pick(_filter, (int?)filter);
        Pick(_aspect, wide is { } w ? (w ? 1 : 0) : null);
        Pick(_lines, lines);

        static void Pick(OptionButton? choice, int? id)
        {
            if (choice is not null && id is { } value && choice.GetItemIndex(value) is var i and >= 0)
            {
                choice.Select(i);
                choice.EmitSignal(OptionButton.SignalName.ItemSelected, i);
            }
        }
    }

    private void Fit()
    {
        if (_zoom is not null)
            _screen.CustomMinimumSize = new Vector2(_wide ? MathF.Round(UiScreen.Height * 16 / 9f) : UiScreen.Width, UiScreen.Height) * _scale;
    }

    private static OptionButton Choice(string tooltip, (int Id, string Name)[] items, Action<int> apply)
    {
        var choice = new OptionButton { TooltipText = tooltip };
        foreach (var (id, name) in items)
            choice.AddItem(name, id);
        choice.ItemSelected += i => apply(choice.GetItemId((int)i));
        return choice;
    }
}
