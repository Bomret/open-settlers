namespace OpenSettlers.Domain;

/// <summary>
/// A resource deposit the generation placed on a node: a resource type from 1 to 4 and an amount from 1 to 20.
/// What each type stands for is not established yet (<c>docs/rules/map-form.md</c>).
/// </summary>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
/// <param name="Type">The resource type.</param>
/// <param name="Amount">The amount left on the node.</param>
public readonly record struct ResourceDeposit(int X, int Y, int Type, int Amount);
