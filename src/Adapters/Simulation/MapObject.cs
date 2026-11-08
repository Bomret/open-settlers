namespace OpenSettlers.Simulation;

/// <summary>
/// An object the generation placed on a tile: a tree, a stone, a plant, or another kind of the original's object
/// codes. The code is the low 7 bits of the tile's <see cref="MapLayer.Object"/> byte; the meaning of a code is
/// not established yet (<c>docs/rules/map-form.md</c>).
/// </summary>
/// <param name="X">The tile's column.</param>
/// <param name="Y">The tile's row.</param>
/// <param name="Code">The object code, from 1 to 127.</param>
public readonly record struct MapObject(int X, int Y, byte Code);
