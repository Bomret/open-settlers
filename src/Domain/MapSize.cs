using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OpenSettlers.Domain;

/// <summary>
/// One of the map sizes the original offers (<c>docs/rules/map-form.md</c>). The size value is the one
/// <c>Game_SetStartParams</c> (<c>0x468717</c>) hands on; <c>StartScreen_StartGame</c> (<c>0x4685bc</c>) turns it
/// into the map's width and height.
/// </summary>
public sealed record MapSize
{
    // The original starts at 32 by 32 and doubles the width, then the height, then the width again, once for
    // each step of the value above 1 (0x4685da to 0x468603).
    /// <summary>The sizes the original offers, smallest first. Only these exist.</summary>
    public static IReadOnlyList<MapSize> Offered { get; } = [new(1, 32, 32), new(2, 64, 32), new(3, 64, 64)];

    private MapSize(int value, int width, int height)
    {
        Value = value;
        Width = width;
        Height = height;
    }

    /// <summary>The size value: 1 for 32 by 32 tiles, 2 for 64 by 32, 3 for 64 by 64.</summary>
    public int Value { get; }

    /// <summary>The number of tile columns; a power of two.</summary>
    public int Width { get; }

    /// <summary>The number of tile rows; a power of two.</summary>
    public int Height { get; }

    /// <summary>
    /// Finds the offered size with this value. Returns false with the reason when the original offers no such
    /// size.
    /// </summary>
    public static bool TryFromValue(
        int value,
        [NotNullWhen(true)] out MapSize? size,
        [NotNullWhen(false)] out string? problem
    )
    {
        size = Offered.FirstOrDefault(offered => offered.Value == value);
        problem = size is null
            ? $"the map size {value} is not one of the sizes the original offers: {string.Join(", ", Offered.Select(offered => offered.Value))}"
            : null;

        return size is not null;
    }
}
