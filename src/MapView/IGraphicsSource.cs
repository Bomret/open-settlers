namespace OpenSettlers.MapView;

/// <summary>
/// What a source draws for an object term: the image, and the shadow drawn right before it. Either is null where the
/// source draws none of it (a source that draws no shadow answers with the image alone).
/// </summary>
public sealed record ObjectDrawing
{
    /// <summary>The image; null where the source draws none.</summary>
    public required ObjectImage? Image { get; init; }

    /// <summary>The shadow drawn right before the image; null where the source draws none.</summary>
    public required ObjectImage? Shadow { get; init; }
}

/// <summary>
/// The ripple of water as a source draws it: the image, and the color of the surface it shows over. The ripple paints
/// only where the screen under it shows the surface color.
/// </summary>
public sealed record RippleImage
{
    /// <summary>The image of the ripple.</summary>
    public required ObjectImage Image { get; init; }

    /// <summary>The color of the surface the ripple shows over, as four bytes (red, green, blue, alpha).</summary>
    public required byte[] Surface { get; init; }
}

/// <summary>
/// Where the map view gets the image of each draw term (ADR 0014, <c>CONTEXT.md</c> "Graphics source"). A source
/// answers a term with an RGBA image whose offsets hang it from the term's position, or with null for what it does not
/// draw. A term holds no mask, texture, archive, or entry number (INV-0010). A source returns the same instance for
/// terms that draw the same image, so <see cref="ImageLayers"/> loads it once.
/// </summary>
public interface IGraphicsSource
{
    /// <summary>The image of a terrain triangle, or null where the source draws none.</summary>
    ObjectImage? Draw(TerrainTriangle triangle);

    /// <summary>The image of a road strip, or null where the source draws none.</summary>
    ObjectImage? Draw(RoadStrip strip);

    /// <summary>The image and shadow of a map object, or null where the source draws neither.</summary>
    ObjectDrawing? Draw(MapObjectDraw draw);

    /// <summary>The image and shadow of a building, or null where the source draws neither.</summary>
    ObjectDrawing? Draw(BuildingDraw draw);

    /// <summary>The image and shadow of a good at a flag, or null where the source draws neither.</summary>
    ObjectDrawing? Draw(GoodAtFlag draw);

    /// <summary>
    /// The image and shadow of one part of a settler, or null where the source draws neither (it has no image for the
    /// settler's job, activity, or step). Only a figure has a shadow.
    /// </summary>
    ObjectDrawing? Draw(SettlerDraw draw);

    /// <summary>The ripple of a water tile, or null where the source draws none.</summary>
    RippleImage? Draw(WaterRipple ripple);

    /// <summary>
    /// The height in map pixels of the tallest image the source draws for an object, 0 where it draws none. A play run's
    /// cropped shot leaves this much room above its tiles.
    /// </summary>
    int ObjectHeadroom { get; }
}
