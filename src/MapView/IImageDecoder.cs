namespace OpenSettlers.MapView;

/// <summary>A decoded image: <see cref="Width"/> by <see cref="Height"/> pixels, four bytes (red, green, blue, alpha) each, row by row.</summary>
public sealed record DecodedImage
{
    /// <summary>The width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>The pixels, four bytes (red, green, blue, alpha) each, row by row.</summary>
    public required byte[] Rgba { get; init; }
}

/// <summary>
/// Reads one PNG file of a content set (ADR 0015). The view's adapter uses Godot's <c>Image.LoadFromFile</c>; a test
/// substitutes its own.
/// </summary>
public interface IImageDecoder
{
    /// <summary>The image the file at <paramref name="path"/> holds, or null where the file cannot be decoded.</summary>
    DecodedImage? Decode(string path);
}
