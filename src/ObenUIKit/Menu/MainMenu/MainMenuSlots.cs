namespace ObenUIKit.Menu;

/// <summary>
/// Where mod buttons go on the main menu and how the keyboard moves between them. The game's buttons are a
/// fixed 2x3 grid; mod buttons fill rows of three under it, in the grid's columns, left to right. Plain
/// arithmetic on slot numbers, so it is unit tested on its own.
/// </summary>
internal static class MainMenuSlots
{
    public const int Columns = 3;

    /// <summary>Slot <paramref name="index"/> of the mod rows: row 0 is the first row under the game's grid.</summary>
    public static (int Row, int Column) Place(int index) => (index / Columns, index % Columns);

    public static int Rows(int count) => (count + Columns - 1) / Columns;

    /// <summary>
    /// Down from the game's bottom row (its column <paramref name="column"/>, 0 to 2): the mod button below
    /// it, or the last one of the first row when that row is short. -1 when there are no mod buttons.
    /// </summary>
    public static int FromGrid(int column, int count) => count == 0 ? -1 : Clamp(0, column, count);

    /// <summary>
    /// Up, down, left or right from mod button <paramref name="index"/>. Returns the next mod button, -1 to
    /// stay where it is, or -2 for "go up into the game's grid" (see <see cref="GridColumnAbove"/>).
    /// </summary>
    public static int Move(int index, int count, int dx, int dy)
    {
        if (index < 0 || index >= count)
            return -1;
        var (row, column) = Place(index);
        if (dy < 0)
            return row == 0 ? -2 : Clamp(row - 1, column, count);
        if (dy > 0)
            return row + 1 < Rows(count) ? Clamp(row + 1, column, count) : -1;
        int next = column + dx;
        if (next < 0 || next >= Columns)
            return -1;
        int target = row * Columns + next;
        return target < count ? target : -1;
    }

    /// <summary>The column of the game's bottom row right above mod button <paramref name="index"/>.</summary>
    public static int GridColumnAbove(int index) => Place(index).Column;

    // The button in that row and column, or the last one of a short row.
    private static int Clamp(int row, int column, int count)
    {
        int index = row * Columns + column;
        return index < count ? index : count - 1;
    }
}
