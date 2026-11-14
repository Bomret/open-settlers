using System;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// An object sprite as the GPU takes it: four bytes per pixel (red, green, blue, alpha). The graphics load converts
/// palette indexes to colors, so a covered pixel is opaque, whatever its index: index 0 is a real color
/// (<c>Palette</c>). An uncovered pixel is fully transparent.
/// </summary>
public sealed record ObjectImage
{
    /// <summary>The width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>The offset of the image's left edge from the draw position.</summary>
    public required int OffsetX { get; init; }

    /// <summary>The offset of the image's top edge from the draw position.</summary>
    public required int OffsetY { get; init; }

    /// <summary>Four bytes per pixel, row by row: the palette's color with alpha 255 where the program draws the pixel, and 0, 0, 0, 0 where it leaves the surface as it is.</summary>
    public required byte[] Rgba { get; init; }

    /// <summary>An image with no pixel, for an entry the archive leaves out.</summary>
    public static ObjectImage Empty { get; } =
        new()
        {
            Width = 0,
            Height = 0,
            OffsetX = 0,
            OffsetY = 0,
            Rgba = [],
        };

    /// <summary>
    /// The image with its pixel rows laid out in a <paramref name="width"/> x <paramref name="height"/> layer, at the
    /// top left; <see cref="Width"/>, <see cref="Height"/> and the offsets stay the image's own, and the rest is
    /// transparent. An array texture takes images of one size.
    /// </summary>
    public ObjectImage Padded(int width, int height)
    {
        var padded = new byte[width * height * 4];
        for (var y = 0; y < Height; y++)
        {
            Array.Copy(Rgba, y * Width * 4, padded, y * width * 4, Width * 4);
        }

        return this with
        {
            Rgba = padded,
        };
    }

    /// <summary>
    /// The part of the image a growing draw shows (<c>Gfx_DrawSpriteGrowing</c>, <c>0x406350</c>): the bottom <c>(fraction * height >> 16) + 1</c>
    /// rows, the top of the clip moved up from the bottom row by that fraction of the height. A fraction of 0xffff shows the whole image.
    /// </summary>
    /// <param name="fraction">How much shows, in 65536ths of the height from the bottom up.</param>
    public ObjectImage Grown(int fraction)
    {
        var rows = Math.Min(Height, (fraction * Height >> 16) + 1);
        if (rows >= Height)
        {
            return this;
        }

        var cut = Height - rows;

        return this with
        {
            Height = rows,
            OffsetY = OffsetY + cut,
            Rgba = Rgba[(cut * Width * 4)..],
        };
    }

    /// <summary>
    /// This image with <paramref name="above"/> drawn over it: an image that covers the union of both, where a pixel <paramref name="above"/>
    /// covers shows it and any other shows this image's.
    /// </summary>
    public ObjectImage Over(ObjectImage above)
    {
        var left = Math.Min(OffsetX, above.OffsetX);
        var top = Math.Min(OffsetY, above.OffsetY);
        var width = Math.Max(OffsetX + Width, above.OffsetX + above.Width) - left;
        var height = Math.Max(OffsetY + Height, above.OffsetY + above.Height) - top;
        var rgba = new byte[width * height * 4];
        Blit(this, rgba);
        Blit(above, rgba);

        return new ObjectImage
        {
            Width = width,
            Height = height,
            OffsetX = left,
            OffsetY = top,
            Rgba = rgba,
        };

        void Blit(ObjectImage image, byte[] target)
        {
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var from = (y * image.Width + x) * 4;
                    if (image.Rgba[from + 3] != 0)
                    {
                        var to = ((image.OffsetY - top + y) * width + image.OffsetX - left + x) * 4;
                        Array.Copy(image.Rgba, from, target, to, 4);
                    }
                }
            }
        }
    }

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

        return new ObjectImage
        {
            Width = sprite.Header.Width,
            Height = sprite.Header.Height,
            OffsetX = sprite.Header.OffsetX,
            OffsetY = sprite.Header.OffsetY,
            Rgba = rgba,
        };
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

        return new ObjectImage
        {
            Width = mask.Width,
            Height = mask.Height,
            OffsetX = mask.Header.OffsetX,
            OffsetY = mask.Header.OffsetY,
            Rgba = rgba,
        };
    }
}
