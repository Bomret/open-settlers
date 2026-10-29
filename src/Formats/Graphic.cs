using System;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>
/// The 10-byte header every graphics entry starts with (<c>docs/rules/graphics-images.md</c>): two signed bytes
/// that no drawing routine reads, the width and the height in pixels, and the signed offsets of the image from
/// the position it is drawn at.
/// </summary>
public sealed record GraphicHeader
{
    /// <summary>The size of the header in bytes; the pixel data starts after it.</summary>
    public const int Size = 10;

    /// <summary>The first signed byte. <c>0x406610</c> reads it in an entry of 1850 to 2390 as the horizontal offset of the second figure (from the base 3150, <c>0x4066d2</c>); no other routine that draws an entry reads it. It is 1 in every raw image of the reference install, which fits a kind tag (inferred).</summary>
    public required sbyte First { get; init; }

    /// <summary>The second signed byte. <c>0x406610</c> reads it in an entry of 1850 to 2390 as the vertical offset of that figure (<c>0x4066e4</c>); no other routine that draws an entry reads it.</summary>
    public required sbyte Second { get; init; }

    /// <summary>The width in pixels.</summary>
    public required ushort Width { get; init; }

    /// <summary>The height in pixels.</summary>
    public required ushort Height { get; init; }

    /// <summary>The offset of the image's left edge from the draw position; negative moves it left.</summary>
    public required short OffsetX { get; init; }

    /// <summary>The offset of the image's top edge from the draw position; negative moves it up.</summary>
    public required short OffsetY { get; init; }
}

/// <summary>
/// A graphics entry split into its header and the body after it. What the body holds depends on the routine that
/// reads the entry (raw pixels for <see cref="RawImage"/>); <see cref="GraphicFile.Read"/> only splits it.
/// </summary>
public sealed record Graphic
{
    /// <summary>The 10-byte header.</summary>
    public required GraphicHeader Header { get; init; }

    /// <summary>Everything after the header.</summary>
    public required ReadOnlyMemory<byte> Body { get; init; }
}

/// <summary>
/// A raw image: <see cref="GraphicHeader.Width"/> x <see cref="GraphicHeader.Height"/> palette indices, row by
/// row from the top, with no transparency (the program copies the rows as they are).
/// </summary>
public sealed record RawImage
{
    /// <summary>The 10-byte header.</summary>
    public required GraphicHeader Header { get; init; }

    /// <summary>One palette index per pixel, row by row from the top, width x height bytes.</summary>
    public required ReadOnlyMemory<byte> Pixels { get; init; }
}

/// <summary>The graphics-entry reader (<c>docs/rules/graphics-images.md</c>).</summary>
public static class GraphicFile
{
    /// <summary>
    /// Splits an entry into its header and body. Throws <see cref="InvalidDataException"/> for an entry shorter
    /// than the header.
    /// </summary>
    public static Graphic Read(ReadOnlyMemory<byte> entry)
    {
        if (entry.Length < GraphicHeader.Size)
        {
            throw new InvalidDataException(
                $"the entry is {entry.Length} bytes, shorter than its {GraphicHeader.Size}-byte header"
            );
        }

        var span = entry.Span;
        var header = new GraphicHeader
        {
            First = (sbyte)span[0],
            Second = (sbyte)span[1],
            Width = BitConverter.ToUInt16(span[2..]),
            Height = BitConverter.ToUInt16(span[4..]),
            OffsetX = BitConverter.ToInt16(span[6..]),
            OffsetY = BitConverter.ToInt16(span[8..]),
        };

        return new Graphic { Header = header, Body = entry[GraphicHeader.Size..] };
    }

    /// <summary>
    /// Reads an entry as a raw image. Throws <see cref="InvalidDataException"/> for an entry shorter than its
    /// header, for a width or height of 0 (stand-in: the program draws nothing for it and no raw image of the
    /// reference install is empty), and for an entry whose size is not 10 + width x height.
    /// </summary>
    public static RawImage ReadRaw(ReadOnlyMemory<byte> entry)
    {
        var graphic = Read(entry);
        var header = graphic.Header;
        if (header.Width == 0 || header.Height == 0)
        {
            throw new InvalidDataException(
                $"the raw image is {header.Width} x {header.Height}: a width or height is 0, so there is nothing to draw"
            );
        }

        // Long: 65535 x 65535 does not fit an int.
        var expected = GraphicHeader.Size + (long)header.Width * header.Height;
        if (entry.Length != expected)
        {
            throw new InvalidDataException(
                $"the raw image is {header.Width} x {header.Height}, so its entry must be {expected} bytes, this entry is {entry.Length} bytes"
            );
        }

        return new RawImage { Header = header, Pixels = graphic.Body };
    }
}

/// <summary>The conversion from palette indices to RGBA, shared by every kind that previews pixels.</summary>
public static class IndexedImage
{
    /// <summary>
    /// Converts palette indices to RGBA, four bytes per pixel in pixel order, each pixel in the color the game
    /// shows for its index (<see cref="Palette.Rgba"/>) with alpha 255. A kind with transparency lowers the alpha
    /// of the pixels its routine does not draw.
    /// </summary>
    public static byte[] ToRgba(ReadOnlySpan<byte> indices, Palette palette)
    {
        var rgba = new byte[indices.Length * 4];
        for (var i = 0; i < indices.Length; i++)
        {
            palette.Rgba(indices[i]).CopyTo(rgba.AsSpan(i * 4));
        }

        return rgba;
    }
}
