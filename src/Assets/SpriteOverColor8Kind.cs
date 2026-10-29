using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>
/// The sprites the program draws only over destination pixels of palette index 8: entries 630 to 645, drawn by
/// <c>0x47b4f9</c> (through <c>0x406420</c> and <c>0x405d00</c>) from <c>0x44ff59</c>, which picks one of the sixteen
/// by a value of the object and a counter of the game state. They hold the same stream as the other sprites
/// (<c>docs/rules/graphics-images.md</c>); the preview draws every pixel of the stream, because the pixels
/// under it are not known. The entries are claimed by this kind and by no kind of <see cref="SpriteKind"/>'s ranges.
/// </summary>
public sealed class SpriteOverColor8Kind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "sprite over color 8";

    const int First = 630;
    const int Last = 645;

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => entry >= First && entry <= Last;

    /// <inheritdoc/>
    protected override Preview Read(Archive archive, ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        SpriteKind.Draw(
            archive,
            entry,
            data,
            null,
            $"drawn only over color 8 (palette index 8) of the surface, in the palette of entry {PaletteKind.GamePalette}; the preview shows every pixel of the stream over a transparent background"
        );
}
