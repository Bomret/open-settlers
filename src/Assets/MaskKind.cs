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
        (4, 4, "shadow", ShadowColor), // 0x45006a stores 4, then the call at 0x45007d reaches 0x4061a0 and 0x405b80
        // 0x44da4d adds 1500 to an object's number and calls 0x4061a0 at 0x44daa4 (the same number plus 1250 is the
        // object's sprite). Each range below is the numbers a site in the code reaches; the entries between them have
        // no site that reaches them. 1613 to 1618 are copies of 1602.
        // Objects 0 to 119: the call at 0x44d6e8, number from the map object's fourth byte.
        (1500, 1612, "shadow", ShadowColor),
        (1619, 1619, "shadow", ShadowColor),
        // Objects 128 to 143: the call at 0x44dcad, 128 plus 0, 4, 8, or 12 and a phase of 0 to 3. 144 and 145: the
        // calls at 0x44fa74 and 0x44fc21.
        (1628, 1645, "shadow", ShadowColor),
        // Objects 152 to 171 and 174: numbers the object routines in 0x44e62b..0x44f928 store before their calls
        // (172 and 173 appear in none).
        (1652, 1671, "shadow", ShadowColor),
        // Objects 175 to 177 and 179 to 187 and 193: the table indexed at 0x44fc87 (call at 0x44fcac). 178 and 192: the
        // calls at 0x44f928 and 0x44ec2a. 188 to 191: 188 plus 0 to 3 at 0x44f3a8 (call at 0x44f3ae).
        (1674, 1693, "shadow", ShadowColor),
        // 0x406120 reads the mask at 0x674008. Mask = 60 + a 9 x 9 index at 0x4278be and 141 + the same kind of index at
        // 0x427a25 (the tables hold 81 entries); at 0x428008, 0x428105, and 0x4281cc a base of 234, 243, or 252 plus
        // the height difference of two nodes, which the nine-wide spacing bounds at -4 to 4. Masks 222 to 229 are
        // reached by none of the five.
        (60, 221, "textured fill", FirstPlayerColor),
        (230, 256, "textured fill", FirstPlayerColor),
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => Find(entry) >= 0;

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
