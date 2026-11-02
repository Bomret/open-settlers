using System;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>The palette as the lookup texture of the terrain shader.</summary>
public static class PaletteTexture
{
    /// <summary>
    /// The 256 colors as 256 x 1 pixels of four bytes (red, green, blue, opaque alpha), index order. The
    /// components are the palette's 6-bit values scaled to 8 bits as the asset browser scales them
    /// (<see cref="Palette.Rgba"/>).
    /// </summary>
    public static byte[] Rgba(Palette palette)
    {
        var bytes = new byte[Palette.ColorCount * 4];
        for (var i = 0; i < Palette.ColorCount; i++)
        {
            palette.Rgba(i).CopyTo(bytes.AsSpan(i * 4, 4));
        }

        return bytes;
    }
}
