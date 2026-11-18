using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>The drawing plan of a map's terrain, roads, and border posts: one rectangle per image, the terrain grouped into bands of rows.</summary>
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

    /// <summary>
    /// The rectangles of the road pass over <c>map</c>: each edge east, south-east, and south of a node, the road strip
    /// where the node holds that road bit and else the border post where the owners differ (<see cref="RoadDraws"/>,
    /// <see cref="BorderDraws"/>). The order is the original's walk (<c>docs/rules/roads.md</c>, "The order") over the
    /// whole map: the half-columns of 16 pixels from west to east, a node's being twice its column less its row; for
    /// each, down the rows from row 0, the zigzag of the edges that lie in it (the south-east edge of a node on its west
    /// side, the south edge of a node on its east side), then the east edges of the nodes on its west side. Each image
    /// hangs from the term's position by its offsets. A term the source draws nothing for gets none.
    /// </summary>
    public static List<ObjectQuad> Edges(Map map, IGraphicsSource source, ImageLayers layers)
    {
        var quads = new List<ObjectQuad>();
        var width = map.Size.Width;
        var height = map.Size.Height;

        // The edge leaving the node of a half-column and row, where that node lies on the map.
        void Add(int half, int row, RoadDirection direction)
        {
            var column = (half + row) / 2;
            if (column < 0 || column >= width)
            {
                return;
            }

            if (map.At(column, row).Roads.Has((int)direction))
            {
                var strip = RoadDraws.Strip(map, column, row, direction);
                if (source.Draw(strip) is { } image)
                {
                    quads.Add(Quad(image, strip.X, strip.Y, layers));
                }
            }
            else if (BorderDraws.Post(map, column, row, direction) is { } post && source.Draw(post) is { } image)
            {
                quads.Add(Quad(image, post.X, post.Y, layers));
            }
        }

        // From the south edge of the node 0, height - 1 (half-column 1 - height) to the east edge of width - 1, 0.
        for (var half = -height; half <= 2 * (width - 1); half++)
        {
            for (var row = 0; row < height; row++)
            {
                if (((half + row) & 1) == 0)
                {
                    Add(half, row, RoadDirection.SouthEast);
                }
                else
                {
                    Add(half + 1, row, RoadDirection.South);
                }
            }

            // The nodes on the half-column lie in the rows of its parity.
            for (var row = half & 1; row < height; row += 2)
            {
                Add(half, row, RoadDirection.East);
            }
        }

        return quads;
    }

    static ObjectQuad Quad(ObjectImage image, int x, int y, ImageLayers layers) =>
        new()
        {
            X = x + image.OffsetX,
            Y = y + image.OffsetY,
            Width = image.Width,
            Height = image.Height,
            Layer = layers.Of(image),
        };
}
