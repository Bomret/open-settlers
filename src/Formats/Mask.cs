using System;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>
/// A mask: a graphic that holds only runs of skipped and covered pixels and no pixel colors
/// (<c>docs/rules/graphics-images.md</c>). The program fills the covered pixels with one color, or with a texture.
/// </summary>
public sealed record Mask
{
    /// <summary>The 10-byte header.</summary>
    public required GraphicHeader Header { get; init; }

    /// <summary>The width in pixels.</summary>
    public int Width => Header.Width;

    /// <summary>The height in pixels.</summary>
    public int Height => Header.Height;

    /// <summary>One flag per pixel, row by row from the top, <see cref="Width"/> x <see cref="Height"/>: true where the mask covers the pixel.</summary>
    public required bool[] Covered { get; init; }

    /// <summary>
    /// The mask as RGBA, four bytes per pixel in pixel order: the covered pixels in the palette color <c>color</c>
    /// with alpha 255, every other pixel with alpha 0.
    /// </summary>
    public byte[] ToRgba(Palette palette, int color)
    {
        var indices = new byte[Covered.Length];
        Array.Fill(indices, (byte)color);
        var rgba = IndexedImage.ToRgba(indices, palette);
        for (var i = 0; i < Covered.Length; i++)
        {
            if (!Covered[i])
            {
                rgba[i * 4 + 3] = 0;
            }
        }

        return rgba;
    }
}

/// <summary>The mask reader (<c>docs/rules/graphics-images.md</c>).</summary>
public static class MaskFile
{
    /// <summary>
    /// Reads an entry as a mask. Throws <see cref="InvalidDataException"/> with its reason, naming the row where
    /// there is one, for an entry shorter than its header, a width or height of 0 (stand-in: nothing to draw), a
    /// height that needs more rows than the entry holds, a width x height of more pixels than the engine's image holds (<see cref="IndexedImage.MaxPixels"/>; both before
    /// anything is allocated), a run that ends past the width, and a stream that ends before every row has its end
    /// pair. The bytes after the last row are ignored, as the
    /// program, which stops after the height's rows, ignores them.
    /// </summary>
    public static Mask Read(ReadOnlyMemory<byte> entry)
    {
        var graphic = GraphicFile.Read(entry);
        var header = graphic.Header;
        if (header.Width == 0 || header.Height == 0)
        {
            throw new InvalidDataException(
                $"the mask is {header.Width} x {header.Height}: a width or height is 0, so there is nothing to draw"
            );
        }

        var body = graphic.Body.Span;
        if (header.Height > body.Length / 2)
        {
            throw new InvalidDataException(
                $"the mask is {header.Width} x {header.Height} but only {body.Length} bytes follow the header, too few bytes for {header.Height} rows that each need at least an end pair"
            );
        }

        var area = (long)header.Width * header.Height;
        // Stand-in bound: the mask must fit the engine's image.
        if (area > IndexedImage.MaxPixels)
        {
            throw new InvalidDataException(
                $"the mask is {header.Width} x {header.Height}, {area} pixels, more than the {IndexedImage.MaxPixels} pixels an image holds"
            );
        }

        var covered = new bool[area];
        var position = 0;
        for (var row = 0; row < header.Height; row++)
        {
            var x = 0;
            while (true)
            {
                if (position + 2 > body.Length)
                {
                    throw new InvalidDataException(
                        $"row {row} of the mask runs past the end of the entry: the stream ends at byte {GraphicHeader.Size + body.Length} before the row's end pair"
                    );
                }

                var skip = body[position];
                var cover = body[position + 1];
                position += 2;
                if (cover == 0)
                {
                    break;
                }

                var start = x + skip;
                x = start + cover;
                if (x > header.Width)
                {
                    throw new InvalidDataException(
                        $"a run in row {row} of the mask ends at column {x}, past the width {header.Width}"
                    );
                }

                Array.Fill(covered, true, row * header.Width + start, cover);
            }
        }

        return new Mask { Header = header, Covered = covered };
    }
}
