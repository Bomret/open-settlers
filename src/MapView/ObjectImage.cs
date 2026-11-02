using System;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// An object sprite as the GPU takes it: the palette indices and, apart from them, the coverage. Index 0 is a real
/// color (<c>Palette</c>), so a pixel is transparent where its coverage is 0 and never because its index is 0.
/// </summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="OffsetX">The offset of the image's left edge from the draw position.</param>
/// <param name="OffsetY">The offset of the image's top edge from the draw position.</param>
/// <param name="Indices">One palette index per pixel, row by row; a pixel that is not drawn holds 0.</param>
/// <param name="Coverage">One byte per pixel in the order of <paramref name="Indices"/>: 255 where the program draws the pixel, 0 where it leaves the surface as it is.</param>
public sealed record ObjectImage(int Width, int Height, int OffsetX, int OffsetY, byte[] Indices, byte[] Coverage)
{
    /// <summary>An image with no pixel, for an entry the archive leaves out.</summary>
    public static ObjectImage Empty { get; } = new(0, 0, 0, 0, [], []);

    /// <summary>Splits a decoded sprite into indices and coverage.</summary>
    public static ObjectImage From(Sprite sprite)
    {
        var coverage = Array.ConvertAll(sprite.Drawn, drawn => drawn ? (byte)255 : (byte)0);

        return new ObjectImage(
            sprite.Header.Width,
            sprite.Header.Height,
            sprite.Header.OffsetX,
            sprite.Header.OffsetY,
            sprite.Indices,
            coverage
        );
    }
}
