using System;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Turns a click on the map view into the map node it lands on. The view draws the map at <see cref="Zoom.Scale"/>
/// around the <see cref="Camera"/> position, which sits at the centre of the view, and repeats it in
/// <see cref="Camera.Copies"/>. A node (column, row) is drawn at <see cref="Camera.NodePosition"/> lifted by
/// <see cref="TerrainTriangles.HeightStep"/> pixels for each unit of its height. The nodes of the copies are the
/// nodes of the map at columns and rows beyond its edges, so the pick wraps them back. The pick takes the node whose
/// drawn point lies nearest to the click, unless the click lands on a flag's sprite, which stands on its node and
/// reaches most of a row above it: then the flag's node. Stand-in: the original's own cursor rule is not recovered, so a click near
/// the edge of a triangle may pick another node than the original does.
/// </summary>
public static class MapPicking
{
    // The tallest lift of a node: the greatest height at 4 pixels a unit.
    private const int MaxLift = Map.HeightMask * TerrainTriangles.HeightStep;

    /// <summary>
    /// The node under a click. <paramref name="cameraX"/> and <paramref name="cameraY"/> are the camera position
    /// the view draws at (whole map pixels: the sub-pixel carry of a drag is not drawn), <paramref name="scale"/>
    /// the screen pixels per map pixel, and the click is at (<paramref name="screenX"/>, <paramref name="screenY"/>)
    /// in a view of <paramref name="viewWidth"/> by <paramref name="viewHeight"/> screen pixels. With
    /// <paramref name="flag"/>, the flag's sprite, a click inside the sprite of a flag on the map picks that flag's node;
    /// of two flags drawn there, the one drawn last: the one in the southern row, and within a row the eastern one.
    /// </summary>
    public static (int X, int Y) Pick(
        Map map,
        double cameraX,
        double cameraY,
        double scale,
        double viewWidth,
        double viewHeight,
        double screenX,
        double screenY,
        ObjectImage? flag = null
    )
    {
        var pointX = cameraX + (screenX - viewWidth / 2) / scale;
        var pointY = cameraY + (screenY - viewHeight / 2) / scale;
        var heights = map.Layer(MapLayer.Height);
        var width = map.Size.Width;
        var rows = map.Size.Height;

        // A node of row r is drawn between 20 r - MaxLift and 20 r, so its row is at or below the point's row.
        var firstRow = (int)Math.Floor(pointY / Camera.RowSpacing);
        var lastRow = firstRow + MaxLift / Camera.RowSpacing + 2;
        var objects = map.Layer(MapLayer.Object);
        (int X, int Y)? onFlag = null;
        var best = (X: 0, Y: 0);
        var bestDistance = double.MaxValue;
        var pickFlag = flag is { Width: > 0 };
        for (var row = firstRow; row <= lastRow; row++)
        {
            var nearestColumn = (int)Math.Floor((pointX + Camera.RowLean * row) / Camera.NodeSpacing);
            for (var column = nearestColumn - 1; column <= nearestColumn + 1; column++)
            {
                var wrappedColumn = ((column % width) + width) % width;
                var wrappedRow = ((row % rows) + rows) % rows;
                var (x, y) = Camera.NodePosition(column, row);
                y -= (heights[wrappedRow * width + wrappedColumn] & Map.HeightMask) * TerrainTriangles.HeightStep;
                if (
                    pickFlag
                    && (objects[wrappedRow * width + wrappedColumn] & 0x7f) == ObjectSprites.FlagCode
                    && pointX >= x + flag!.OffsetX
                    && pointX < x + flag.OffsetX + flag.Width
                    && pointY >= y + flag.OffsetY
                    && pointY < y + flag.OffsetY + flag.Height
                )
                {
                    onFlag = (wrappedColumn, wrappedRow);
                }

                var distance = (x - pointX) * (x - pointX) + (y - pointY) * (y - pointY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (wrappedColumn, wrappedRow);
                }
            }
        }

        return onFlag ?? best;
    }

    /// <summary>
    /// The screen point a click on a node aims at, the inverse of <see cref="Pick"/> for the same view: the node's
    /// drawn point, lifted by its height, in the copy of <see cref="Camera.Copies"/> that lies on the view and nearest
    /// its middle; of copies equally near, the first in that order. Null with the point in <paramref name="screenX"/>
    /// and <paramref name="screenY"/> when the click there picks the node; else the problem, with the point at 0: a
    /// node outside the map, a node with no copy on the view (<paramref name="barHeight"/> screen pixels above the view
    /// are under the button bar, which the problem names), or a node drawn on the same point as another, which the
    /// pick takes instead. The point is rounded to single precision, as a click sends it.
    /// </summary>
    public static string? Aim(
        Map map,
        double cameraX,
        double cameraY,
        double scale,
        double viewWidth,
        double viewHeight,
        double barHeight,
        int column,
        int row,
        out double screenX,
        out double screenY
    )
    {
        (screenX, screenY) = (0, 0);
        var width = map.Size.Width;
        var rows = map.Size.Height;
        if (column < 0 || column >= width || row < 0 || row >= rows)
            return $"tile {column},{row} is outside the map of {width} by {rows} tiles";

        var (x, y) = Camera.NodePosition(column, row);
        y -= (map.Layer(MapLayer.Height)[row * width + column] & Map.HeightMask) * TerrainTriangles.HeightStep;
        (double X, double Y)? best = null;
        var bestDistance = double.MaxValue;
        var underBar = false;
        foreach (var (copyX, copyY) in new Camera(map.Size).Copies())
        {
            var pointX = (x + copyX - cameraX) * scale + viewWidth / 2;
            var pointY = (y + copyY - cameraY) * scale + viewHeight / 2;
            if (pointX < 0 || pointX >= viewWidth)
                continue;
            if (pointY < 0 || pointY >= viewHeight)
            {
                underBar |= pointY < 0 && pointY >= -barHeight;
                continue;
            }

            var distance =
                (pointX - viewWidth / 2) * (pointX - viewWidth / 2)
                + (pointY - viewHeight / 2) * (pointY - viewHeight / 2);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = (pointX, pointY);
            }
        }

        if (best is not { } nearest)
            return underBar ? $"tile {column},{row} is under the button bar" : $"tile {column},{row} is not on screen";

        // A click sends the point in single precision, so the pick is checked at that rounded point.
        (double X, double Y) at = ((float)nearest.X, (float)nearest.Y);
        var picked = Pick(map, cameraX, cameraY, scale, viewWidth, viewHeight, at.X, at.Y);
        if (picked != (column, row))
            return $"tile {column},{row} is drawn on the same point as tile {picked.X},{picked.Y}";

        (screenX, screenY) = at;

        return null;
    }
}
