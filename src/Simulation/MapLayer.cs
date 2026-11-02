namespace OpenSettlers.Simulation;

/// <summary>
/// A layer of the canonical map form: one byte for each tile, in row-major order (<c>docs/rules/map-form.md</c>).
/// The numbers are the layer order and never change; a later layer takes the next number.
/// </summary>
public enum MapLayer
{
    /// <summary>The flag byte of each tile; bit 6 marks the middle of a lake.</summary>
    Flags = 0,

    /// <summary>The height of each tile's corner.</summary>
    Height = 1,

    /// <summary>The terrain of the two triangles of each tile: the upper triangle in the high nibble.</summary>
    Terrain = 2,

    /// <summary>The lake detail of each tile: a draw from 0 to 7 where a lake has its middle.</summary>
    Lake = 3,
}
