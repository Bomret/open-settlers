using System;
using OpenSettlers.Formats;

namespace OpenSettlers.Assets;

/// <summary>The preview of a palette: its 256 colors.</summary>
public sealed record PalettePreview : Preview
{
    /// <summary>The palette.</summary>
    public required Palette Palette { get; init; }
}

/// <summary>
/// The palette entries of the graphics archives (<c>docs/rules/graphics-images.md</c>): entry 3, the palette the
/// game draws with, and entries 3997 and 3998, which the program loads through the same routine.
/// </summary>
public sealed class PaletteKind : ArchiveEntryKind
{
    /// <summary>The kind's name.</summary>
    public const string KindName = "palette";

    /// <summary>The entry of the palette the game draws with (<c>0x407250</c> and <c>0x407400</c> load it).</summary>
    public const int GamePalette = 3;

    /// <summary>The entry of the palette the title scene draws with (<c>0x407410</c>, called at <c>0x451ee9</c>).</summary>
    public const int TitlePalette = 3997;

    /// <summary>The entry of the palette the program loads at start (<c>0x4075ac</c>).</summary>
    public const int StartPalette = 3998;

    /// <inheritdoc/>
    public override string Name => KindName;

    /// <inheritdoc/>
    public override bool Claims(int entry) => entry is GamePalette or TitlePalette or StartPalette;

    /// <inheritdoc/>
    protected override Preview Read(ArchiveEntry entry, ReadOnlyMemory<byte> data) =>
        new PalettePreview
        {
            Summary = $"Palette of entry {entry.Number}: 256 colors, 6 bits per component, color 0 black.",
            Palette = PaletteFile.Read(data.Span),
        };
}
