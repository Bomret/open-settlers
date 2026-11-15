using System;
using OpenSettlers.Formats;
using OpenSettlers.Import;

namespace OpenSettlers.Import.HistoryEdition;

/// <summary>The History Edition's sprites and masks as <see cref="ObjectImage"/>s.</summary>
public static class ObjectImages
{
    /// <summary>The palette index the shadow routine at <c>0x405b80</c> stores.</summary>
    public const int ShadowColor = 0x80;

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
    /// A shadow mask as an image: every covered pixel in the color of palette index <see cref="ShadowColor"/>, as
    /// <c>Gfx_BlitShadow</c> (<c>0x405b80</c>) stores it with no blending.
    /// </summary>
    public static ObjectImage Shadow(Mask mask, Palette palette)
    {
        var rgba = new byte[mask.Covered.Length * 4];
        for (var i = 0; i < mask.Covered.Length; i++)
        {
            if (mask.Covered[i])
            {
                palette.Rgba(ShadowColor).CopyTo(rgba.AsSpan(i * 4, 4));
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
