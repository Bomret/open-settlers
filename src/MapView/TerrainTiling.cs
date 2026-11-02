using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>The limits that keep the map's drawing within what a GPU holds.</summary>
public static class TerrainTiling
{
    /// <summary>
    /// Splits a map of <c>columns</c> by <c>rows</c> tiles into rectangles of at most <c>side</c> tiles a side, west
    /// to east and north to south. Each rectangle is one mesh, so no mesh holds more vertices than a GPU index
    /// buffer takes and the map's size is no limit.
    /// </summary>
    public static List<(int Column, int Row, int Width, int Height)> Chunks(int columns, int rows, int side)
    {
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

    /// <summary>The number of entries from <c>first</c> to <c>last</c>, each its own layer of an array texture.</summary>
    public static int Layers(int first, int last) => last - first + 1;

    /// <summary>Whether an array texture of <c>count</c> layers is within <c>limit</c> layers.</summary>
    public static bool FitsLayerLimit(int count, int limit) => count <= limit;
}
