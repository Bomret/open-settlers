using System;
using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>One mask-sized rectangle to draw: its top-left corner and size in map pixels, and its array texture layers.</summary>
public readonly record struct TerrainQuad(int X, int Y, int Width, int Height, int MaskLayer, int TextureLayer);

/// <summary>The drawing plan of a map's triangles: one rectangle per triangle, grouped into bands of rows.</summary>
public static class TerrainQuads
{
    /// <summary>
    /// Turns <c>triangles</c>, sorted by row as <see cref="TerrainTriangles.Of"/> returns them, into rectangles and
    /// groups them in bands of <c>bandRows</c> rows (the last band may be shorter), so the painting order holds across
    /// bands. A band without a triangle is left out. A mask hangs from its anchor: centered on it and moved by the
    /// header's offset. Throws <see cref="ArgumentException"/> where a triangle's row is lower than its predecessor's.
    /// </summary>
    public static List<List<TerrainQuad>> Bands(
        IReadOnlyList<TerrainTriangle> triangles,
        IReadOnlyList<MaskImage> masks,
        int rows,
        int bandRows
    )
    {
        var bands = new List<List<TerrainQuad>>();
        var next = 0;
        var previousRow = 0;
        foreach (var band in TerrainTiling.Chunks(1, rows, bandRows))
        {
            var quads = new List<TerrainQuad>();
            while (next < triangles.Count && triangles[next].Row < band.Row + band.Height)
            {
                var triangle = triangles[next++];
                if (triangle.Row < previousRow)
                {
                    throw new ArgumentException("the triangles are not sorted by row", nameof(triangles));
                }

                previousRow = triangle.Row;
                var maskLayer = TerrainGraphics.MaskLayer(triangle.Mask);
                var mask = masks[maskLayer];
                quads.Add(
                    new TerrainQuad(
                        triangle.X - TerrainGraphics.LayerWidth / 2 + mask.OffsetX,
                        triangle.Y + mask.OffsetY,
                        mask.Width,
                        mask.Height,
                        maskLayer,
                        TerrainGraphics.TextureLayer(triangle.Texture)
                    )
                );
            }

            if (quads.Count > 0)
            {
                bands.Add(quads);
            }
        }

        return bands;
    }
}
