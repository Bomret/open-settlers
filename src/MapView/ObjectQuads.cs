using System;
using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>One sprite-sized rectangle to draw: its top-left corner and size in map pixels, and its array texture layer.</summary>
public readonly record struct ObjectQuad(int X, int Y, int Width, int Height, int Layer);

/// <summary>The drawing plan of a map's objects: one rectangle per object, grouped into bands of rows.</summary>
public static class ObjectQuads
{
    /// <summary>
    /// Turns <c>draws</c>, in row-major order as <see cref="ObjectDraws.Of"/> returns them, into rectangles and
    /// groups them in bands of <c>bandRows</c> rows, the grouping <see cref="TerrainQuads.Bands"/> uses, so a band's
    /// objects paint after its terrain. Each sprite's shadow, where it has one, comes right before it, as
    /// <c>0x44da4d</c> draws the mask before the sprite. A sprite hangs from the draw position by its header offsets. A draw whose
    /// image is empty is left out, and so is a band without a draw.
    /// </summary>
    public static List<List<ObjectQuad>> Bands(
        IReadOnlyList<ObjectDraw> draws,
        IReadOnlyList<ObjectImage> images,
        int rows,
        int bandRows
    )
    {
        var bands = new List<List<ObjectQuad>>();
        var next = 0;
        foreach (var band in TerrainTiling.Chunks(1, rows, bandRows))
        {
            var quads = new List<ObjectQuad>();
            while (next < draws.Count && draws[next].Row < band.Row + band.Height)
            {
                var draw = draws[next++];
                var shadow = ObjectGraphics.ShadowLayer(draw.Entry);
                if (shadow < images.Count)
                {
                    Add(quads, draw, images, shadow);
                }

                Add(quads, draw, images, ObjectGraphics.Layer(draw.Entry));
            }

            if (quads.Count > 0)
            {
                bands.Add(quads);
            }
        }

        return bands;
    }

    static void Add(List<ObjectQuad> quads, ObjectDraw draw, IReadOnlyList<ObjectImage> images, int layer)
    {
        var image = images[layer];
        if (image.Width > 0 && image.Height > 0)
        {
            quads.Add(new ObjectQuad(draw.X + image.OffsetX, draw.Y + image.OffsetY, image.Width, image.Height, layer));
        }
    }
}
