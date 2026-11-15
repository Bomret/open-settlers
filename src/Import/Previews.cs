using System.Collections.Generic;

namespace OpenSettlers.Import;

/// <summary>The preview of a palette: its 256 colors as a 16 x 16 grid, color 0 first.</summary>
public sealed record PalettePreview : Preview
{
    /// <summary>Four bytes per color (red, green, blue, alpha), 256 colors in index order.</summary>
    public required byte[] Rgba { get; init; }
}

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

/// <summary>The preview of a raw image or a mask: its size and its pixels as RGBA, drawn in the game's palette.</summary>
public sealed record RawImagePreview : Preview
{
    /// <summary>The image width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The image height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Four bytes per pixel (red, green, blue, alpha), row by row from the top.</summary>
    public required byte[] Rgba { get; init; }
}

/// <summary>The preview of a sound: the sound itself, which the view plays.</summary>
public sealed record SoundPreview : Preview
{
    /// <summary>The sample rate in Hz.</summary>
    public required int SampleRate { get; init; }

    /// <summary>The samples as signed 8-bit mono, the form a player of signed 8-bit streams takes.</summary>
    public required byte[] SignedSamples { get; init; }
}

/// <summary>The preview of the music file: its tracks and length. It is shown as text and never played.</summary>
public sealed record MusicPreview : Preview
{
    /// <summary>The event count of each track, in track order.</summary>
    public required IReadOnlyList<int> TrackEventCounts { get; init; }

    /// <summary>The length of the music in seconds.</summary>
    public required double LengthSeconds { get; init; }
}

/// <summary>The preview of an entry whose bytes the browser does not show (rule data, an entry no routine reads): where it lies. It has no byte preview.</summary>
public sealed record LocatedEntryPreview : Preview
{
    /// <summary>The entry's program number.</summary>
    public required int Number { get; init; }

    /// <summary>The entry's offset in its archive.</summary>
    public required uint Offset { get; init; }

    /// <summary>The entry's size in bytes.</summary>
    public required uint Size { get; init; }
}

/// <summary>One key of the text table and its value in each language, in <see cref="TextPreview.Languages"/> order; null where the language has no such key.</summary>
public sealed record TextRow
{
    /// <summary>The key of the text table.</summary>
    public required string Key { get; init; }

    /// <summary>The key's value in each language, in <see cref="TextPreview.Languages"/> order; null where the language has no such key.</summary>
    public required IReadOnlyList<string?> Values { get; init; }
}

/// <summary>The preview of the texts: one table of every key, with one value column per language.</summary>
public sealed record TextPreview : Preview
{
    /// <summary>The languages, one column each, in the program's order.</summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>The keys in order of first appearance across the languages.</summary>
    public required IReadOnlyList<TextRow> Rows { get; init; }
}
