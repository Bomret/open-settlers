namespace OpenSettlers.MapView;

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
}
