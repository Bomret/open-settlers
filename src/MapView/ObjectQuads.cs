using System;
using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>
/// One sprite-sized rectangle to draw: its top-left corner and size in map pixels, its array texture layer, and
/// whether that layer is one of the shadow array (<see cref="ObjectGraphics.ShadowImages"/>) rather than the sprite array.
/// </summary>
public readonly record struct ObjectQuad(int X, int Y, int Width, int Height, int Layer, bool Shadow = false);

/// <summary>The drawing plan of a map's objects: one rectangle per object, grouped into bands of rows.</summary>
public static class ObjectQuads
{
    /// <summary>
    /// Turns <c>draws</c>, in row-major order as <see cref="ObjectDraws.Of"/> returns them, into rectangles and
    /// groups them in bands of <c>bandRows</c> rows, the grouping <see cref="TerrainQuads.Bands"/> uses, so a band's
    /// objects paint after its terrain. Each sprite's shadow from <c>shadows</c>, where it has one, comes right before it, as
    /// <c>0x44da4d</c> draws the mask before the sprite. A sprite hangs from the draw position by its header offsets. A draw whose
    /// image is empty is left out, and so is a band without a draw.
    /// </summary>
    public static List<List<ObjectQuad>> Bands(
        IReadOnlyList<ObjectDraw> draws,
        IReadOnlyList<ObjectImage> images,
        IReadOnlyList<ObjectImage> shadows,
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
                Add(quads, draw, shadows, ObjectGraphics.ShadowLayer(draw.Entry), true);
                Add(quads, draw, images, ObjectGraphics.Layer(draw.Entry), false);
            }

            if (quads.Count > 0)
            {
                bands.Add(quads);
            }
        }

        return bands;
    }

    static void Add(List<ObjectQuad> quads, ObjectDraw draw, IReadOnlyList<ObjectImage> images, int layer, bool shadow)
    {
        var image = images[layer];
        if (image.Width > 0 && image.Height > 0)
        {
            quads.Add(
                new ObjectQuad(draw.X + image.OffsetX, draw.Y + image.OffsetY, image.Width, image.Height, layer, shadow)
            );
        }
    }
}
