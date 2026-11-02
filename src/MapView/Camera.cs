using System.Collections.Generic;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The place the map view looks at, in map pixels. The map is a torus (<c>Map_SetGeometry</c> at <c>0x42195c</c>
/// wraps the column and the row on their own, <c>docs/rules/map-form.md</c>). A node (column, row) lies at
/// <c>x = 32 * column - 16 * row</c>, <c>y = 20 * row</c> before its height lowers it by 4 pixels per unit, so the
/// map repeats after <see cref="PeriodX"/> pixels east and after the vector (-16 * rows, 20 * rows) south. The
/// position is kept in the one copy that starts at the map's north-west node: <c>0 &lt;= X &lt; PeriodX</c> and
/// <c>0 &lt;= Y &lt; PeriodY</c>.
/// </summary>
public sealed class Camera
{
    /// <summary>The pixels between two nodes of a row.</summary>
    public const int NodeSpacing = 32;

    /// <summary>The pixels from a node to the next row.</summary>
    public const int RowSpacing = 20;

    /// <summary>The pixels each row leans west.</summary>
    public const int RowLean = 16;

    readonly int _rows;

    /// <summary>A camera at the north-west node of a map of this size.</summary>
    public Camera(MapSize size)
    {
        _rows = size.Height;
        PeriodX = size.Width * NodeSpacing;
        PeriodY = size.Height * RowSpacing;
    }

    /// <summary>The width of the map in pixels.</summary>
    public int PeriodX { get; }

    /// <summary>The height of the map in pixels.</summary>
    public int PeriodY { get; }

    /// <summary>The west-east position, 0 to <see cref="PeriodX"/> - 1.</summary>
    public int X { get; private set; }

    /// <summary>The north-south position, 0 to <see cref="PeriodY"/> - 1.</summary>
    public int Y { get; private set; }

    /// <summary>The pixel position of a node's column and row, before its height.</summary>
    public static (int X, int Y) NodePosition(int column, int row) =>
        (column * NodeSpacing - row * RowLean, row * RowSpacing);

    /// <summary>Moves by <c>dx</c> and <c>dy</c> pixels and wraps at the edges.</summary>
    public void Move(int dx, int dy) => Set((long)X + dx, (long)Y + dy);

    /// <summary>Puts the camera on a node.</summary>
    public void CenterOn(int column, int row)
    {
        var (x, y) = NodePosition(column, row);
        Set(x, y);
    }

    /// <summary>
    /// The offsets, in pixels, at which to draw the map so the copies around the camera tile the plane. Each row of
    /// copies leans west by 16 pixels times the rows, at most half of <see cref="PeriodX"/> on every offered size.
    /// The copies cover a view of up to two maps each way around the camera (<see cref="Zoom.Fit"/>).
    /// </summary>
    public IEnumerable<(int X, int Y)> Copies()
    {
        for (var j = -2; j <= 2; j++)
        {
            for (var i = -3; i <= 3; i++)
            {
                yield return (i * PeriodX - j * RowLean * _rows, j * PeriodY);
            }
        }
    }

    void Set(long x, long y)
    {
        var rowsAround = System.Math.DivRem(y, PeriodY, out var remainder);
        if (remainder < 0)
        {
            remainder += PeriodY;
            rowsAround--;
        }

        // A whole map south is the same place 16 pixels times the rows further east.
        x += rowsAround * RowLean * _rows;
        x %= PeriodX;
        if (x < 0)
        {
            x += PeriodX;
        }

        X = (int)x;
        Y = (int)remainder;
    }
}
