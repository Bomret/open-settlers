using System.Collections.Generic;

namespace OpenSettlers.MapView;

/// <summary>One image-sized rectangle to draw: its top-left corner and size in map pixels, and its array texture layer.</summary>
public readonly record struct ObjectQuad
{
    /// <summary>The left edge in map pixels.</summary>
    public required int X { get; init; }

    /// <summary>The top edge in map pixels.</summary>
    public required int Y { get; init; }

    /// <summary>The width in map pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The height in map pixels.</summary>
    public required int Height { get; init; }

    /// <summary>The layer of the array texture.</summary>
    public required int Layer { get; init; }
}

/// <summary>Which images a <see cref="SpriteRun"/> draws from: the map objects' or the settlers'.</summary>
public enum RunKind
{
    /// <summary>The map objects, the buildings, and the goods at flags.</summary>
    Objects,

    /// <summary>The walking settlers.</summary>
    Settlers,
}

/// <summary>Rectangles of one kind that paint one after the other, their layers from that kind's images.</summary>
public sealed record SpriteRun
{
    /// <summary>Whose images the layers name.</summary>
    public required RunKind Kind { get; init; }

    /// <summary>The rectangles in painting order.</summary>
    public required List<ObjectQuad> Quads { get; init; }
}

/// <summary>The drawing plan of a map's objects: one rectangle per image the source draws, grouped into bands of rows.</summary>
public static class ObjectQuads
{
    /// <summary>
    /// Asks <c>source</c> for each of <c>terms</c>, in the row-major order <see cref="ObjectDraws.Of"/> returns them,
    /// and groups the rectangles in bands of <c>bandRows</c> rows, the grouping <see cref="TerrainQuads.Bands"/> uses, so
    /// a band's objects paint after its terrain. An object's shadow, where the source answers with one, comes right
    /// before its image, as <c>0x44da4d</c> draws the mask before the sprite. An image hangs from the term's position by
    /// its offsets and gets its layer from <c>layers</c>. A term the source draws nothing for, and an image with no
    /// pixel, get no quad and no layer, and a band without a quad is left out.
    /// </summary>
    public static List<List<ObjectQuad>> Bands(
        IReadOnlyList<ObjectTerm> terms,
        IGraphicsSource source,
        ImageLayers layers,
        int rows,
        int bandRows
    )
    {
        var bands = new List<List<ObjectQuad>>();
        var next = 0;
        foreach (var band in TerrainTiling.Chunks(1, rows, bandRows))
        {
            var quads = new List<ObjectQuad>();
            while (next < terms.Count && terms[next].Row < band.Row + band.Height)
            {
                var term = terms[next++];
                if (term.Ask(source) is { } drawing)
                {
                    Add(quads, term.X, term.Y, drawing.Shadow, layers);
                    Add(quads, term.X, term.Y, drawing.Image, layers);
                }
            }

            if (quads.Count > 0)
            {
                bands.Add(quads);
            }
        }

        return bands;
    }

    /// <summary>
    /// The objects and the settlers in the order <c>0x44d3ae</c> paints them: row by row from the top, each row's
    /// <c>objects</c> and then its <c>settlers</c> (<c>docs/rules/build-sites.md</c>, "The draw order"). A settler a
    /// row above a building is painted over by it; a settler in its row paints over it. Both lists come ordered by row.
    /// Neighboring terms of one kind join one run, and a run ends at the end of its band of <c>bandRows</c> rows, so no
    /// mesh grows with the map. The rectangles come as in <see cref="Bands"/>, the objects' layers from
    /// <c>objectLayers</c> and the settlers' from <c>settlerLayers</c>. A term above the top row falls in the first band;
    /// a term the source draws nothing for starts no run.
    /// </summary>
    public static List<SpriteRun> Runs(
        IReadOnlyList<ObjectTerm> objects,
        IReadOnlyList<ObjectTerm> settlers,
        IGraphicsSource source,
        ImageLayers objectLayers,
        ImageLayers settlerLayers,
        int rows,
        int bandRows
    )
    {
        var runs = new List<SpriteRun>();
        var nextObject = 0;
        var nextSettler = 0;
        foreach (var band in TerrainTiling.Chunks(1, rows, bandRows))
        {
            var end = band.Row + band.Height;
            SpriteRun? run = null;
            while (true)
            {
                var objectDue = nextObject < objects.Count && objects[nextObject].Row < end;
                var settlerDue = nextSettler < settlers.Count && settlers[nextSettler].Row < end;
                if (!objectDue && !settlerDue)
                {
                    break;
                }

                // A row's objects come before its settlers.
                var takeObject = objectDue && (!settlerDue || objects[nextObject].Row <= settlers[nextSettler].Row);
                var (term, kind, layers) = takeObject
                    ? (objects[nextObject++], RunKind.Objects, objectLayers)
                    : (settlers[nextSettler++], RunKind.Settlers, settlerLayers);
                if (term.Ask(source) is not { } drawing)
                {
                    continue;
                }

                var quads = new List<ObjectQuad>();
                Add(quads, term.X, term.Y, drawing.Shadow, layers);
                Add(quads, term.X, term.Y, drawing.Image, layers);
                if (quads.Count == 0)
                {
                    continue;
                }

                if (run is null || run.Kind != kind)
                {
                    run = new SpriteRun { Kind = kind, Quads = [] };
                    runs.Add(run);
                }

                run.Quads.AddRange(quads);
            }
        }

        return runs;
    }

    /// <summary>
    /// The rectangles of <c>terms</c> in the order they come, with no banding: the build markers, which the view draws as
    /// one mesh. A term's shadow, where the source answers with one, comes right before its image. A term the source draws
    /// nothing for gets no quad and no layer.
    /// </summary>
    public static List<ObjectQuad> Flat(IReadOnlyList<ObjectTerm> terms, IGraphicsSource source, ImageLayers layers)
    {
        var quads = new List<ObjectQuad>();
        foreach (var term in terms)
        {
            if (term.Ask(source) is { } drawing)
            {
                Add(quads, term.X, term.Y, drawing.Shadow, layers);
                Add(quads, term.X, term.Y, drawing.Image, layers);
            }
        }

        return quads;
    }

    /// <summary>
    /// The rectangles of the water <c>ripples</c> the source draws, in order, and the surface color they show over (null
    /// where the source draws none). They take their layers from <c>layers</c>, which holds the ripples' images alone.
    /// </summary>
    public static (List<ObjectQuad> Quads, byte[]? Surface) Ripples(
        IReadOnlyList<WaterRipple> ripples,
        IGraphicsSource source,
        ImageLayers layers
    )
    {
        var quads = new List<ObjectQuad>();
        byte[]? surface = null;
        foreach (var ripple in ripples)
        {
            if (source.Draw(ripple) is { } drawn)
            {
                surface ??= drawn.Surface;
                Add(quads, ripple.X, ripple.Y, drawn.Image, layers);
            }
        }

        return (quads, surface);
    }

    static void Add(List<ObjectQuad> quads, int x, int y, ObjectImage? image, ImageLayers layers)
    {
        if (image is { Width: > 0, Height: > 0 })
        {
            quads.Add(
                new ObjectQuad
                {
                    X = x + image.OffsetX,
                    Y = y + image.OffsetY,
                    Width = image.Width,
                    Height = image.Height,
                    Layer = layers.Of(image),
                }
            );
        }
    }
}
