using System;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// Turns a click on the map view into the map node it lands on. The view draws the map at <see cref="Zoom.Scale"/>
/// around the <see cref="Camera"/> position, which sits at the centre of the view, and repeats it in
/// <see cref="Camera.Copies"/>. A node (column, row) is drawn at <see cref="Camera.NodePosition"/> lifted by
/// <see cref="TerrainTriangles.HeightStep"/> pixels for each unit of its height. The nodes of the copies are the
/// nodes of the map at columns and rows beyond its edges, so the pick wraps them back. The pick takes the node whose
/// drawn point lies nearest to the click, a stand-in for the original's own cursor rule, which is not recovered.
/// </summary>
public static class MapPicking
{
    // The tallest lift of a node: height 31 at 4 pixels a unit.
    private const int MaxLift = 31 * TerrainTriangles.HeightStep;

    /// <summary>
    /// The node under a click. <paramref name="cameraX"/> and <paramref name="cameraY"/> are the camera position
    /// the view draws at (whole map pixels: the sub-pixel carry of a drag is not drawn), <paramref name="scale"/>
    /// the screen pixels per map pixel, and the click is at (<paramref name="screenX"/>, <paramref name="screenY"/>)
    /// in a view of <paramref name="viewWidth"/> by <paramref name="viewHeight"/> screen pixels.
    /// </summary>
    public static (int X, int Y) Pick(
        Map map,
        double cameraX,
        double cameraY,
        double scale,
        double viewWidth,
        double viewHeight,
        double screenX,
        double screenY
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
        var best = (X: 0, Y: 0);
        var bestDistance = double.MaxValue;
        for (var row = firstRow; row <= lastRow; row++)
        {
            var nearestColumn = (int)Math.Floor((pointX + Camera.RowLean * row) / Camera.NodeSpacing);
            for (var column = nearestColumn; column <= nearestColumn + 1; column++)
            {
                var wrappedColumn = ((column % width) + width) % width;
                var wrappedRow = ((row % rows) + rows) % rows;
                var (x, y) = Camera.NodePosition(column, row);
                y -= (heights[wrappedRow * width + wrappedColumn] & 31) * TerrainTriangles.HeightStep;
                var distance = (x - pointX) * (x - pointX) + (y - pointY) * (y - pointY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (wrappedColumn, wrappedRow);
                }
            }
        }

        return best;
    }
}
