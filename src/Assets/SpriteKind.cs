using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The preview of a transparent sprite: its size, its offsets from the draw position, and its pixels as RGBA, drawn
/// in the game's palette (or in one color) over a transparent background.
/// </summary>
public sealed record SpritePreview : Preview
{
    /// <summary>The sprite width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The sprite height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>The signed offset of the sprite's left edge from the draw position. The preview draws the sprite alone, so it only reports this.</summary>
    public required int OffsetX { get; init; }

    /// <summary>The signed offset of the sprite's top edge from the draw position. The preview draws the sprite alone, so it only reports this.</summary>
    public required int OffsetY { get; init; }

    /// <summary>Four bytes per pixel (red, green, blue, alpha), row by row from the top; alpha is 0 where the program draws nothing.</summary>
    public required byte[] Rgba { get; init; }

    /// <summary>The palette index every drawn pixel has when the program draws the sprite in one color, or null when it draws the stored pixels.</summary>
    public int? OneColor { get; init; }
}

/// <summary>
/// The entries the program draws as transparent sprites: the stream that <c>0x405590</c> reads (called from
/// <c>0x4062e0</c> and <c>0x406610</c>), and in one color <c>0x405880</c> (called from <c>0x406490</c> and
/// <c>0x406610</c>); see <c>docs/rules/graphics-images.md</c>. Each range names the code that picks its entries;
/// the entries of a range are the ones the archive holds. An overlay of 2500 to 3040 may have no drawn pixel.
/// </summary>
public sealed class SpriteKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "sprite";

    /// <summary>The palette index of the text color the program draws the glyphs of 750 to 793 in (<c>0x450fde</c>, <c>0x451111</c>).</summary>
    const int GlyphColor = 31;

    /// <summary>The palette index of the first player's color (0x40 plus 4 times the player, <c>0x406610</c>), the stand-in for the player of the overlays of 2500 to 3040.</summary>
    const int FirstPlayerColor = 0x40;

    /// <summary>The palette index of the shadow color the program draws the glyphs of 810 to 853 in (<c>0x4511d5</c>).</summary>
    const int GlyphShadowColor = 1;

    // Each range with the step between its claimed entries (the sources of the copies that 0x407290 makes are
    // 6 apart) and the code that picks it.
    static readonly (int First, int Last, int Step)[] Ranges =
    [
        (321, 372, 1), // 0x44de9a and 0x44fd4a: an index plus 320
        (490, 490, 1), // 0x44de9a and 0x44fd4a: an index plus 320 (170), the one entry of the dropped range 448 to 521 that the controller keeps
        (610, 619, 1), // 0x428528: 610 to 612, those plus 3 or 6, and 619
        (750, 793, 1), // 0x4511d5 through 0x406490: a glyph plus 749, one color
        (810, 853, 1), // 0x4511d5 through 0x406490: a glyph plus 809, one color
        (1250, 1273, 1), // 0x44da4d: an index plus 1250
        (1314, 1362, 1), // 0x44da4d, 0x406350, 0x406590: an index plus 1250 (64 to 112)
        (1378, 1443, 1), // 0x44da4d, 0x406350, 0x406590: an index plus 1250 (128 to 193)
        (1850, 2390, 1), // 0x406610: an index plus 1850
        (2500, 2701, 1), // 0x406610: an index plus 2500, in the player color
        (2740, 2965, 1), // 0x406610: an index plus 2500, in the player color
        (3000, 3040, 1), // 0x406610: an index plus 2500, in the player color
        (3150, 3450, 1), // 0x406610: an index plus 3150
        (3456, 3732, 6), // 0x406610: the first entry of each group of six that 0x407290 fills
        (3738, 3764, 1), // 0x406610: an index plus 3150
        (3768, 3779, 1), // 0x406610: an index plus 3150
    ];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) =>
        Array.Exists(Ranges, r => entry >= r.First && entry <= r.Last && (entry - r.First) % r.Step == 0);

    /// <summary>The palette index the program draws the entry in when it draws it in one color through <c>0x405880</c>, or null.</summary>
    public static int? OneColorOf(int entry) =>
        entry switch
        {
            >= 750 and <= 793 => GlyphColor,
            >= 810 and <= 853 => GlyphShadowColor,
            >= 2500 and <= 3040 => FirstPlayerColor,
            _ => null,
        };

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        throw new InvalidOperationException("a sprite is read with the palette of its archive");

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data)
    {
        var sprite = SpriteFile.Read(data);
        var palette = PaletteOf(archive, PaletteKind.GamePalette);
        var oneColor = OneColorOf(entry.AliasOf ?? entry.Number);
        var header = sprite.Header;
        var drawing = oneColor is { } color ? $"in one color, palette index {color}" : "in its own colors";

        return new SpritePreview
        {
            Summary =
                $"Sprite of entry {entry.Number}: {header.Width} x {header.Height}, offset {header.OffsetX}, {header.OffsetY}, {drawing}, over a transparent background, in the palette of entry {PaletteKind.GamePalette}.",
            Width = header.Width,
            Height = header.Height,
            OffsetX = header.OffsetX,
            OffsetY = header.OffsetY,
            Rgba = sprite.ToRgba(palette, (byte?)oneColor),
            OneColor = oneColor,
        };
    }
}
