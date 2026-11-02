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

    /// <summary>
    /// The object of each tile: the low 7 bits are the object code (0 for none) and bit 7 marks a tile whose
    /// upper or lower triangle is water. Record A byte 3 of the original.
    /// </summary>
    Object = 4,

    /// <summary>
    /// The resource deposit of each tile: the high 3 bits are the resource type (0 for none) and the low 5 bits the
    /// amount. The original keeps it in the byte of <see cref="Lake"/>: the two layers are the same bytes.
    /// </summary>
    Resource = 5,
}
