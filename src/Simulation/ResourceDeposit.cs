namespace OpenSettlers.Simulation;

/// <summary>
/// A resource deposit the generation placed on a tile: a resource type from 1 to 4 and an amount from 1 to 20,
/// read from the tile's <see cref="MapLayer.Resource"/> byte (type in the high 3 bits, amount in the low 5).
/// What each type stands for is not established yet (<c>docs/rules/map-form.md</c>).
/// </summary>
/// <param name="X">The tile's column.</param>
/// <param name="Y">The tile's row.</param>
/// <param name="Type">The resource type.</param>
/// <param name="Amount">The amount left on the tile.</param>
public readonly record struct ResourceDeposit(int X, int Y, int Type, int Amount);
