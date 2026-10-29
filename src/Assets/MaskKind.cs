using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The entries the program draws as masks, the graphics that hold only runs of skipped and covered pixels
/// (<c>docs/rules/graphics-images.md</c>). The preview is the mask drawn alone in the one palette color the
/// program stores for it, as a <see cref="RawImagePreview"/> whose uncovered pixels have alpha 0.
/// </summary>
public sealed class MaskKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "mask";

    /// <summary>The palette index the shadow routine at <c>0x405b80</c> stores.</summary>
    public const int ShadowColor = 0x80;

    /// <summary>
    /// The palette index of the first player's color, <c>0x40 + 4 x player</c> with player 0 (the formula of the
    /// routine at <c>0x406610</c>). Stand-in for a textured-fill mask, which the program fills with a texture and
    /// not with one color.
    /// </summary>
    public const int FirstPlayerColor = 0x40;

    // Each range with what the program does with it, the color it is shown in, and the routine that reads it.
    static readonly (int First, int Last, string What, int Color)[] Ranges =
    [
        (4, 4, "shadow", ShadowColor), // 0x44fff3 sets the entry to 4 and calls 0x4061a0, which calls 0x405b80
        // 0x44da4d adds 1500 to an object's number and calls 0x4061a0; the same number plus 1250 is the object's sprite.
        // The bases the code reaches are 0 to 119 (0x44d5b4) and 128 to 143 (0x44db26); the range ends where the
        // archives' entries end, which is the end of the sprites plus 250. 1613 to 1618 are copies of 1602.
        (1500, 1612, "shadow", ShadowColor),
        (1619, 1693, "shadow", ShadowColor),
        // 0x406120 reads the mask at 0x674008, plus 60 at 0x4278be, 141 at 0x427a25, a computed number at 0x4281cc.
        (60, 256, "textured fill", FirstPlayerColor),
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => Find(entry) >= 0;

    /// <summary>The palette index the mask of the entry is drawn in.</summary>
    public static int ColorOf(int entry) => Ranges[Find(entry)].Color;

    static int Find(int entry) => Array.FindIndex(Ranges, r => entry >= r.First && entry <= r.Last);

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        throw new InvalidOperationException("a mask is read with the palette of its archive");

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data)
    {
        var mask = MaskFile.Read(data);
        var source = entry.AliasOf ?? entry.Number;
        var (_, _, what, color) = Ranges[Find(source)];
        var palette = PaletteOf(archive, PaletteKind.GamePalette);

        return new RawImagePreview
        {
            Summary =
                $"Mask ({what}) of entry {entry.Number}: {mask.Width} x {mask.Height}, drawn alone in palette color 0x{color:x2} of the palette of entry {PaletteKind.GamePalette}.",
            Width = mask.Width,
            Height = mask.Height,
            Rgba = mask.ToRgba(palette, color),
        };
    }
}
