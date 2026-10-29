using System;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>
/// A transparent sprite (<c>docs/rules/graphics-images.md</c>): a <see cref="GraphicHeader"/> and a
/// width x height grid of palette indices of which only some pixels are drawn.
/// </summary>
public sealed record Sprite
{
    /// <summary>The 10-byte header.</summary>
    public required GraphicHeader Header { get; init; }

    /// <summary>One palette index per pixel, row by row from the top, width x height bytes. A pixel that is not drawn holds 0.</summary>
    public required byte[] Indices { get; init; }

    /// <summary>One flag per pixel in the order of <see cref="Indices"/>: true where the program draws the pixel, false where it leaves the surface as it is.</summary>
    public required bool[] Drawn { get; init; }

    /// <summary>
    /// The sprite as RGBA, four bytes per pixel: a drawn pixel opaque in the palette's color for its index, or in
    /// the palette's color for <paramref name="oneColor"/> where the program draws the sprite in one color
    /// (<c>0x405880</c>); every other pixel fully transparent.
    /// </summary>
    public byte[] ToRgba(Palette palette, byte? oneColor = null)
    {
        var indices = Indices;
        if (oneColor is { } color)
        {
            indices = new byte[Indices.Length];
            for (var pixel = 0; pixel < indices.Length; pixel++)
            {
                indices[pixel] = Drawn[pixel] ? color : (byte)0;
            }
        }

        var rgba = IndexedImage.ToRgba(indices, palette);
        for (var pixel = 0; pixel < Drawn.Length; pixel++)
        {
            if (!Drawn[pixel])
            {
                rgba[pixel * 4 + 3] = 0;
            }
        }

        return rgba;
    }
}

/// <summary>The decoder of the transparent encoding that the program draws with <c>0x405590</c> and, in one color, with <c>0x405880</c>.</summary>
public static class SpriteFile
{
    /// <summary>
    /// Decodes an entry: after the header, each of the height rows is a sequence of runs, each run a skip byte, a
    /// count byte, and that many palette indices; a run whose count is 0 ends the row and its skip is ignored.
    /// Throws <see cref="InvalidDataException"/> for an entry shorter than its header, for a width or height of
    /// 0 (stand-in: nothing to draw), for a row whose runs end beyond the width, and for a stream that ends before
    /// its last row or inside a run. Bytes after the last row are ignored, as the program ignores them.
    /// </summary>
    public static Sprite Read(ReadOnlyMemory<byte> entry)
    {
        var graphic = GraphicFile.Read(entry);
        var header = graphic.Header;
        if (header.Width == 0 || header.Height == 0)
        {
            throw new InvalidDataException(
                $"the sprite is {header.Width} x {header.Height}: a width or height is 0, so there is nothing to draw"
            );
        }

        var body = graphic.Body.Span;
        var width = header.Width;
        var indices = new byte[width * header.Height];
        var drawn = new bool[indices.Length];
        var at = 0;
        for (var row = 0; row < header.Height; row++)
        {
            var x = 0;
            while (true)
            {
                if (at + 2 > body.Length)
                {
                    throw new InvalidDataException(
                        $"the stream of the sprite ends at body byte {body.Length} inside row {row} of {header.Height}, before the pair that ends the row"
                    );
                }

                var skip = body[at];
                var count = body[at + 1];
                at += 2;
                if (count == 0)
                {
                    break;
                }

                x += skip;
                if (x + count > width)
                {
                    throw new InvalidDataException(
                        $"row {row} of the sprite has a run that ends at pixel {x + count}, past the width {width}"
                    );
                }

                if (at + count > body.Length)
                {
                    throw new InvalidDataException(
                        $"a run of {count} pixels in row {row} of the sprite starts at body byte {at} and runs past the end of the entry at body byte {body.Length}"
                    );
                }

                body.Slice(at, count).CopyTo(indices.AsSpan(row * width + x));
                Array.Fill(drawn, true, row * width + x, count);
                at += count;
                x += count;
            }
        }

        return new Sprite
        {
            Header = header,
            Indices = indices,
            Drawn = drawn,
        };
    }
}
