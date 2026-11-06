using System;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>A rectangle of a frame in whole pixels: the left and top edges, the width, and the height.</summary>
public readonly record struct CropBox(int X, int Y, int Width, int Height);

/// <summary>
/// The part of a frame a play run's <c>shot &lt;name&gt; &lt;column&gt; &lt;row&gt;</c> step keeps: the screen bounds
/// of every tile within the radius of the aimed tile, with room above for the tallest object sprite. A tile's bounds
/// are its drawn point, lifted by its height, and half the spacing between nodes around it: 16 pixels west and east,
/// 10 north and south, at the zoom. The aimed tile is placed as a click on it is (<see cref="MapPicking.Aim(Map,
/// double, double, double, double, double, double, int, int, out double, out double)"/>), and every ring tile in the
/// same copy, so a box at a seam of the map never spans the canvas.
/// </summary>
public static class ShotCrop
{
    /// <summary>The largest side of an image Godot makes, in pixels.</summary>
    public const int MaxImageSide = 16384;

    /// <summary>
    /// Null with the box in <paramref name="box"/> when the tile can be aimed at; else the problem
    /// <see cref="MapPicking.Aim(Map, double, double, double, double, double, double, int, int, out double, out
    /// double)"/> gives, with an empty box. The view is the canvas of <paramref name="viewWidth"/> by
    /// <paramref name="viewHeight"/> screen pixels at (<paramref name="originX"/>, <paramref name="originY"/>) of the
    /// frame, with the button bar the <paramref name="originY"/> pixels above it. A tile is within
    /// <paramref name="radius"/> when that many steps or fewer to a neighbouring node reach it.
    /// <paramref name="headroom"/> map pixels above the box, at the zoom, leave room for an object sprite. The box is
    /// floored at the top and left, ceiled at the bottom and right, clipped to the canvas, and in frame coordinates.
    /// </summary>
    public static string? Box(
        Map map,
        double cameraX,
        double cameraY,
        double scale,
        double viewWidth,
        double viewHeight,
        double originX,
        double originY,
        int column,
        int row,
        int radius,
        int headroom,
        out CropBox box
    )
    {
        box = default;
        var problem = MapPicking.Aim(
            map,
            cameraX,
            cameraY,
            scale,
            viewWidth,
            viewHeight,
            originY,
            column,
            row,
            out _,
            out _,
            out var copy
        );
        if (problem is not null)
            return problem;

        var width = map.Size.Width;
        var rows = map.Size.Height;
        var heights = map.Layer(MapLayer.Height);
        var (left, top, right, bottom) = (double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
        for (var dr = -radius; dr <= radius; dr++)
        {
            for (var dc = -radius; dc <= radius; dc++)
            {
                // The neighbours of a node are one column or one row away, or one of each with the same sign.
                var steps =
                    Math.Sign(dc) == Math.Sign(dr) ? Math.Max(Math.Abs(dc), Math.Abs(dr)) : Math.Abs(dc) + Math.Abs(dr);
                if (steps > radius)
                    continue;

                var (c, r) = (column + dc, row + dr);
                var (x, y) = Camera.NodePosition(c, r);
                var wrapped = (((r % rows) + rows) % rows) * width + ((c % width) + width) % width;
                y -= (heights[wrapped] & Map.HeightMask) * TerrainTriangles.HeightStep;
                var screenX = (x + copy.X - cameraX) * scale + viewWidth / 2;
                var screenY = (y + copy.Y - cameraY) * scale + viewHeight / 2;
                left = Math.Min(left, screenX - Camera.NodeSpacing / 2 * scale);
                right = Math.Max(right, screenX + Camera.NodeSpacing / 2 * scale);
                top = Math.Min(top, screenY - Camera.RowSpacing / 2 * scale);
                bottom = Math.Max(bottom, screenY + Camera.RowSpacing / 2 * scale);
            }
        }

        top -= headroom * scale;
        var x0 = (int)Math.Max(0, Math.Floor(left));
        var y0 = (int)Math.Max(0, Math.Floor(top));
        var x1 = (int)Math.Min(Math.Floor(viewWidth), Math.Ceiling(right));
        var y1 = (int)Math.Min(Math.Floor(viewHeight), Math.Ceiling(bottom));
        box = new CropBox(x0 + (int)originX, y0 + (int)originY, x1 - x0, y1 - y0);

        return null;
    }

    /// <summary>
    /// Null when <paramref name="box"/> enlarged by <paramref name="scale"/> fits an image of at most
    /// <see cref="MaxImageSide"/> pixels on a side; else the problem naming both sizes.
    /// </summary>
    public static string? ScaleProblem(CropBox box, int scale)
    {
        var (width, height) = ((long)box.Width * scale, (long)box.Height * scale);

        return width <= MaxImageSide && height <= MaxImageSide
            ? null
            : $"the crop of {box.Width}x{box.Height} pixels at scale {scale} is {width}x{height}, more than {MaxImageSide} on a side";
    }
}
