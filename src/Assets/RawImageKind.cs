using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of a raw image: its size and its pixels as RGBA, drawn in the game's palette.</summary>
public sealed record RawImagePreview : Preview
{
    /// <summary>The image width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The image height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Four bytes per pixel (red, green, blue, alpha), row by row from the top, all opaque.</summary>
    public required byte[] Rgba { get; init; }
}

/// <summary>
/// The entries the program draws as raw images: the routine at <c>0x405730</c>, reached from
/// <c>Gfx_DrawSprite</c> at <c>0x4060b0</c>, copies width x height palette indices opaque
/// (<c>docs/rules/graphics-images.md</c>). Each range names the code that picks its entries; the bounds of a range
/// that an index picks are the entries the archives hold.
/// </summary>
public sealed class RawImageKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "raw image";

    // Each range with the palette entry that is loaded while the program draws it and the call sites of
    // Gfx_DrawSprite that draw it.
    static readonly (int First, int Last, int Palette)[] Ranges =
    [
        (1, 1, PaletteKind.TitlePalette), // 0x452394
        (5, 11, PaletteKind.TitlePalette), // 0x452400: a table of 0 to 6, plus 5
        (15, 21, PaletteKind.TitlePalette), // 0x452443: a counter of 0 to 6, plus 15
        (25, 38, PaletteKind.GamePalette), // 0x45f9ae: a value of the game state, plus 25
        // 0x41ea7c and 0x41eaaa (40, 41), the list at 0x674824 drawn by 0x41ee33 (42 to 57), and 0x425926 (42 plus an
        // index of 0 to 15, which draws in palette 3). The six colors that 42 to 57 use are the same in 3 and 3998.
        (40, 57, PaletteKind.StartPalette),
        (600, 603, PaletteKind.GamePalette), // 0x42105f to 0x4213e9
        (660, 663, PaletteKind.GamePalette), // 0x421412 to 0x42152a, 0x45cf10
        (670, 677, PaletteKind.GamePalette), // 0x45cf57 to 0x45d02c: 670 to 673, and 674 to 677 as 670 to 673 plus 4
        (870, 1187, PaletteKind.GamePalette), // 0x44de86 and 0x467bd4: a tile index plus 870
        (1750, 1774, PaletteKind.GamePalette), // 0x44d157: a byte plus 1750
        (1780, 1796, PaletteKind.GamePalette), // 0x44d24f, 0x44d2e8 (1782 to 1785) and the lists at 0x420d9f, 0x420e19, 0x420eab, 0x420f25 drawn by 0x420d97
        (1800, 1805, PaletteKind.GamePalette), // the same four lists
        (3880, 3882, PaletteKind.GamePalette), // 0x421251 to 0x42136d
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => Find(entry) >= 0;

    /// <summary>The entry of the palette a raw image is drawn in: the palette the program has loaded while it draws the image.</summary>
    public static int PaletteEntryOf(int entry) => Ranges[Find(entry)].Palette;

    // The index of the range that holds the entry, or -1.
    static int Find(int entry) => Array.FindIndex(Ranges, r => entry >= r.First && entry <= r.Last);

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        Draw("Raw image", archive, entry, data, PaletteEntryOf(entry.AliasOf ?? entry.Number));

    /// <summary>
    /// The preview of a raw image of an archive, opaque, in the palette of entry <c>paletteEntry</c>; the summary
    /// starts with <c>what</c>.
    /// </summary>
    internal static RawImagePreview Draw(
        string what,
        Archive archive,
        ArchiveEntry entry,
        ReadOnlyMemory<byte> data,
        int paletteEntry
    )
    {
        var image = GraphicFile.ReadRaw(data);
        var palette = PaletteOf(archive, paletteEntry);

        return new RawImagePreview
        {
            Summary =
                $"{what} of entry {entry.Number}: {image.Header.Width} x {image.Header.Height}, opaque, in the palette of entry {paletteEntry}.",
            Width = image.Header.Width,
            Height = image.Header.Height,
            Rgba = IndexedImage.ToRgba(image.Pixels.Span, palette),
        };
    }
}
