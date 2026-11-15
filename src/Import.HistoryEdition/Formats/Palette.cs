using System;
using System.Collections.Generic;
using System.IO;

namespace OpenSettlers.Formats;

/// <summary>One palette color with each component at 6 bits (0 to 63), as the program holds it.</summary>
readonly record struct Rgb
{
    /// <summary>The red component, 0 to 63.</summary>
    public required byte R { get; init; }

    /// <summary>The green component, 0 to 63.</summary>
    public required byte G { get; init; }

    /// <summary>The blue component, 0 to 63.</summary>
    public required byte B { get; init; }
}

/// <summary>
/// The game's 256-color palette (<c>docs/rules/graphics-images.md</c>). The program holds each component
/// at 6 bits and shows it times 4, so <see cref="Rgba"/> gives 0 to 252.
/// </summary>
sealed class Palette
{
    /// <summary>The number of colors in a palette.</summary>
    public const int ColorCount = 256;

    // R, G, B, A of each color as 8-bit values, ready for the conversion of an indexed image.
    readonly byte[] _rgba = new byte[ColorCount * 4];

    internal Palette(IReadOnlyList<Rgb> colors)
    {
        Colors = colors;
        for (var i = 0; i < ColorCount; i++)
        {
            _rgba[i * 4] = (byte)(colors[i].R << 2);
            _rgba[i * 4 + 1] = (byte)(colors[i].G << 2);
            _rgba[i * 4 + 2] = (byte)(colors[i].B << 2);
            _rgba[i * 4 + 3] = 255;
        }
    }

    /// <summary>The 256 colors in index order, components at 6 bits.</summary>
    public IReadOnlyList<Rgb> Colors { get; }

    /// <summary>The color with this index (0 to 255), components at 6 bits.</summary>
    public Rgb this[int index] => Colors[Checked(index)];

    /// <summary>
    /// The color with this index as four bytes, red, green, blue, and an opaque alpha, as the game shows it.
    /// Throws <see cref="ArgumentOutOfRangeException"/> naming the index where it is not 0 to 255.
    /// </summary>
    public ReadOnlySpan<byte> Rgba(int index) => _rgba.AsSpan(Checked(index) * 4, 4);

    /// <summary>
    /// The color or index <paramref name="value"/> when it is 0 to 255. Throws <see cref="ArgumentOutOfRangeException"/>
    /// naming the value otherwise, so no caller wraps it into another color.
    /// </summary>
    public static int Checked(int value, string? name = null) =>
        value is >= 0 and < ColorCount
            ? value
            : throw new ArgumentOutOfRangeException(
                name ?? "index",
                value,
                $"palette color {value} is outside 0 to {ColorCount - 1}"
            );
}

/// <summary>The palette reader (<c>docs/rules/graphics-images.md</c>).</summary>
static class PaletteFile
{
    /// <summary>The size of a palette entry: 256 colors of three bytes.</summary>
    public const int Size = Palette.ColorCount * 3;

    /// <summary>
    /// Reads a palette entry as the program's palette builder does: three bytes per color, each reduced to 6 bits,
    /// color 0 forced to black. Throws <see cref="InvalidDataException"/> for an entry that is not 768 bytes.
    /// </summary>
    public static Palette Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new InvalidDataException(
                $"a palette is {Size} bytes (256 colors of 3 bytes), this entry is {bytes.Length} bytes"
            );
        }

        var colors = new Rgb[Palette.ColorCount];
        for (var i = 1; i < colors.Length; i++)
        {
            colors[i] = new Rgb
            {
                R = (byte)(bytes[i * 3] >> 2),
                G = (byte)(bytes[i * 3 + 1] >> 2),
                B = (byte)(bytes[i * 3 + 2] >> 2),
            };
        }

        return new Palette(colors);
    }
}
