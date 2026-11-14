namespace OpenSettlers.Domain;

/// <summary>
/// A resource deposit the generation placed on a node: a resource type from 1 to 4 and an amount from 1 to 20.
/// What each type stands for is not established yet (<c>docs/rules/map-form.md</c>).
/// </summary>
public readonly record struct ResourceDeposit
{
    /// <summary>The node's column.</summary>
    public required int X { get; init; }

    /// <summary>The node's row.</summary>
    public required int Y { get; init; }

    /// <summary>The resource type.</summary>
    public required int Type { get; init; }

    /// <summary>The amount left on the node.</summary>
    public required int Amount { get; init; }
}
