using System;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// An object sprite as the GPU takes it: four bytes per pixel (red, green, blue, alpha). The graphics load converts
/// palette indexes to colors, so a covered pixel is opaque, whatever its index: index 0 is a real color
/// (<c>Palette</c>). An uncovered pixel is fully transparent.
/// </summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="OffsetX">The offset of the image's left edge from the draw position.</param>
/// <param name="OffsetY">The offset of the image's top edge from the draw position.</param>
/// <param name="Rgba">Four bytes per pixel, row by row: the palette's color with alpha 255 where the program draws the pixel, and 0, 0, 0, 0 where it leaves the surface as it is.</param>
public sealed record ObjectImage(int Width, int Height, int OffsetX, int OffsetY, byte[] Rgba)
{
    /// <summary>An image with no pixel, for an entry the archive leaves out.</summary>
    public static ObjectImage Empty { get; } = new(0, 0, 0, 0, []);

    /// <summary>Converts a decoded sprite through <paramref name="palette"/>.</summary>
    public static ObjectImage From(Sprite sprite, Palette palette)
    {
        var rgba = new byte[sprite.Indices.Length * 4];
        for (var i = 0; i < sprite.Indices.Length; i++)
        {
            if (sprite.Drawn[i])
            {
                palette.Rgba(sprite.Indices[i]).CopyTo(rgba.AsSpan(i * 4, 4));
            }
        }

        return new ObjectImage(
            sprite.Header.Width,
            sprite.Header.Height,
            sprite.Header.OffsetX,
            sprite.Header.OffsetY,
            rgba
        );
    }

    /// <summary>
    /// A shadow mask as an image: every covered pixel in the color of palette index <see cref="Assets.MaskKind.ShadowColor"/>, as
    /// <c>Gfx_BlitShadow</c> (<c>0x405b80</c>) stores it with no blending.
    /// </summary>
    public static ObjectImage Shadow(Mask mask, Palette palette)
    {
        var rgba = new byte[mask.Covered.Length * 4];
        for (var i = 0; i < mask.Covered.Length; i++)
        {
            if (mask.Covered[i])
            {
                palette.Rgba(Assets.MaskKind.ShadowColor).CopyTo(rgba.AsSpan(i * 4, 4));
            }
        }

        return new ObjectImage(mask.Width, mask.Height, mask.Header.OffsetX, mask.Header.OffsetY, rgba);
    }
}
