using System;
using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>The drawing plan of a map's terrain and roads: one rectangle per image, grouped into bands of rows.</summary>
public static class TerrainQuads
{
    /// <summary>
    /// Turns <c>triangles</c>, sorted by row as <see cref="TerrainTriangles.Of"/> returns them, into rectangles and
    /// groups them in bands of <c>bandRows</c> rows (the last band may be shorter), so the painting order holds across
    /// bands. A band without a quad is left out. An image hangs from the triangle's position by its offsets and gets
    /// its layer from <c>layers</c>; a triangle the source draws nothing for gets no quad. Throws
    /// <see cref="ArgumentException"/> where a triangle's row is lower than its predecessor's or not below <c>rows</c>.
    /// </summary>
    public static List<List<ObjectQuad>> Bands(
        IReadOnlyList<TerrainTriangle> triangles,
        IGraphicsSource source,
        ImageLayers layers,
        int rows,
        int bandRows
    )
    {
        var bands = new List<List<ObjectQuad>>();
        var next = 0;
        var previousRow = 0;
        foreach (var band in TerrainTiling.Chunks(1, rows, bandRows))
        {
            var quads = new List<ObjectQuad>();
            while (next < triangles.Count && triangles[next].Row < band.Row + band.Height)
            {
                var triangle = triangles[next++];
                if (triangle.Row < previousRow)
                {
                    throw new ArgumentException("the triangles are not sorted by row", nameof(triangles));
                }

                previousRow = triangle.Row;
                if (source.Draw(triangle) is { } image)
                {
                    quads.Add(Quad(image, triangle.X, triangle.Y, layers));
                }
            }

            if (quads.Count > 0)
            {
                bands.Add(quads);
            }
        }

        if (next < triangles.Count)
        {
            throw new ArgumentException("a triangle's row lies below the last row of the map", nameof(triangles));
        }

        return bands;
    }

    /// <summary>The rectangles of the road <c>strips</c>, in order: each image hangs from the strip's position by its offsets. A strip the source draws nothing for gets none.</summary>
    public static List<ObjectQuad> Strips(IReadOnlyList<RoadStrip> strips, IGraphicsSource source, ImageLayers layers)
    {
        var quads = new List<ObjectQuad>(strips.Count);
        foreach (var strip in strips)
        {
            if (source.Draw(strip) is { } image)
            {
                quads.Add(Quad(image, strip.X, strip.Y, layers));
            }
        }

        return quads;
    }

    static ObjectQuad Quad(ObjectImage image, int x, int y, ImageLayers layers) =>
        new(x + image.OffsetX, y + image.OffsetY, image.Width, image.Height, layers.Of(image));
}
