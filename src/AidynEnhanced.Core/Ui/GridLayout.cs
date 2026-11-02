namespace AidynEnhanced.Core.Ui;

/// <summary>
/// Where a menu's grid sits in the 320 × 240 frame: its square cells, so many to a line, from its bounds' top left, as many lines as
/// the bounds hold. The views draw a grid and test the mouse against it from one of these, and the core sizes the grid's list from
/// it (<see cref="MenuList{TRow}.Columns"/>, <see cref="MenuList{TRow}.Visible"/>), so it scrolls with no view.
/// </summary>
/// <param name="Bounds">Its place: the cells from its top left, and as many lines as its height holds.</param>
/// <param name="Cell">How wide and tall a cell is.</param>
/// <param name="Columns">How many cells to a line.</param>
public readonly record struct GridLayout(ScreenRect Bounds, int Cell, int Columns)
{
    /// <summary>How many lines show at once.</summary>
    public int Lines => (Bounds.Bottom - Bounds.Top) / Cell;

    /// <summary>How many cells show at once.</summary>
    public int Visible => Lines * Columns;

    /// <summary>The top left of the cell so far into the cells in view, from the first.</summary>
    public (int X, int Y) CellAt(int shown) => (Bounds.Left + shown % Columns * Cell, Bounds.Top + shown / Columns * Cell);

    /// <summary>The same grid moved <paramref name="by"/> across, as it slides in or out.</summary>
    public GridLayout Shifted(int by) => this with { Bounds = new ScreenRect(Bounds.Left + by, Bounds.Top, Bounds.Right + by, Bounds.Bottom) };

    /// <summary>Makes a list a grid of this one's lines.</summary>
    public void Apply<TRow>(MenuList<TRow> list)
    {
        list.Columns = Columns;
        list.Visible = Visible;
    }

    /// <summary>
    /// The row of <paramref name="list"/> in the cell under a point in the frame, if the point is on a cell in view and the list has a row
    /// there.
    /// </summary>
    public int? RowAt<TRow>(MenuList<TRow> list, float x, float y)
    {
        if (x < Bounds.Left || x >= Bounds.Left + Columns * Cell || y < Bounds.Top || y >= Bounds.Top + Lines * Cell)
            return null;
        var row = list.First + (int)(y - Bounds.Top) / Cell * Columns + (int)(x - Bounds.Left) / Cell;
        return row < list.Rows.Count ? row : null;
    }

    /// <summary>
    /// Where a step of the cursor goes in a grid of <paramref name="count"/> rows, or null where it leaves the grid: across the line
    /// (<paramref name="across"/> ±1), off the line's either end; or up and down a line (<paramref name="down"/> ±1), wrapping from
    /// the last line to the first and back in the same column, and onto the last row where the line below is short of the column.
    /// </summary>
    public static int? Step(int cursor, int count, int columns, int across, int down)
    {
        if (count == 0)
            return null;
        if (across != 0)
        {
            var column = cursor % columns + across;
            return column >= 0 && column < columns && cursor + across < count ? cursor + across : null;
        }
        var lines = (count + columns - 1) / columns;
        var line = cursor / columns + down;
        if (line < 0)
            line = lines - 1;
        else if (line >= lines)
            line = 0;
        return Math.Min(line * columns + cursor % columns, count - 1);
    }
}
