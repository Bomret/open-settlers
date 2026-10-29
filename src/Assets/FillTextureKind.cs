using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The textures of the textured fill: raw images that the routine at <c>0x47b19c</c>, reached through
/// <c>0x406120</c>, tiles over the covered pixels of a mask (<c>docs/rules/graphics-images.md</c>). The preview is
/// the texture as a <see cref="RawImagePreview"/>, opaque, in the game's palette.
/// </summary>
public sealed class FillTextureKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "fill texture";

    // 0x4278be and 0x427a25 add 260 to a value of a table of 0 to 31 and 32; 0x4281cc picks 300 to 309.
    static readonly (int First, int Last)[] Ranges = [(260, 292), (300, 309)];

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => Array.Exists(Ranges, r => entry >= r.First && entry <= r.Last);

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        throw new InvalidOperationException("a fill texture is read with the palette of its archive");

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data)
    {
        var image = GraphicFile.ReadRaw(data);
        var palette = PaletteOf(archive, PaletteKind.GamePalette);

        return new RawImagePreview
        {
            Summary =
                $"Fill texture of entry {entry.Number}: {image.Header.Width} x {image.Header.Height}, opaque, in the palette of entry {PaletteKind.GamePalette}.",
            Width = image.Header.Width,
            Height = image.Header.Height,
            Rgba = IndexedImage.ToRgba(image.Pixels.Span, palette),
        };
    }
}
