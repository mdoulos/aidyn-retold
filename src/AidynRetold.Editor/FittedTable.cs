using Godot;

namespace AidynRetold.Editor;

/// <summary>
/// A short table in a card or a box, exactly as high as the rows it shows, up to a number of them, past which it scrolls. A tree
/// draws each row's rule on the row's last pixel and lays itself out a little taller than its rows, so this cuts it a pixel short of
/// its last row: the card's or the box's border is that row's rule, with nothing under it. The cut follows the table's own rounded
/// foot, as it masks the tree by its own shape (<c>TableFoot</c> in a card, <c>TableInBox</c> in a <c>ListBox</c>).
/// </summary>
public partial class FittedTable : Panel
{
    private readonly Tree _tree;
    private readonly int _max;

    /// <param name="max">The most rows it's as high as; a table with more scrolls.</param>
    /// <param name="inBox">Whether it stands in a box of its own (<c>ListBox</c>), rounded all round, rather than at a card's foot.</param>
    public FittedTable(Tree tree, int max, bool inBox = false)
    {
        (_tree, _max) = (tree, max);
        ThemeTypeVariation = inBox ? "TableInBox" : "TableFoot";
        ClipContents = true;
        ClipChildren = ClipChildrenMode.AndDraw;
        tree.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(tree);
        Fit();
        // A branch opened or folded changes how many rows show.
        tree.ItemCollapsed += _ => Fit();
    }

    public Tree Tree => _tree;

    private void Fit()
    {
        var rows = EditorStyle.VisibleRows(_tree);
        var scrolls = rows > _max;
        _tree.ScrollVerticalEnabled = scrolls;
        CustomMinimumSize = new Vector2(0, Math.Max(Math.Min(rows, _max) * EditorStyle.TableRow - (scrolls ? 0 : 1), 0));
    }
}
