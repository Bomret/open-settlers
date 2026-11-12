using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>The split that keeps the map's drawing within what a GPU holds.</summary>
public static class TerrainTiling
{
    /// <summary>
    /// Splits a map of <c>columns</c> by <c>rows</c> tiles into rectangles of at most <c>side</c> tiles a side, west
    /// to east and north to south. Each rectangle is one mesh, so no mesh holds more vertices than a GPU index
    /// buffer takes and the map's size is no limit. A <c>side</c> of zero or less throws
    /// <see cref="System.ArgumentOutOfRangeException"/>.
    /// </summary>
    public static List<(int Column, int Row, int Width, int Height)> Chunks(int columns, int rows, int side)
    {
        System.ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(side, 0);
        var chunks = new List<(int, int, int, int)>();
        for (var row = 0; row < rows; row += side)
        {
            for (var column = 0; column < columns; column += side)
            {
                chunks.Add((column, row, System.Math.Min(side, columns - column), System.Math.Min(side, rows - row)));
            }
        }

        return chunks;
    }
}
