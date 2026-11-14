namespace OpenSettlers.Simulation;

/// <summary>A player's castle: the node it stands on and the height its site was levelled to.</summary>
public readonly record struct Castle
{
    /// <summary>The node's column; the castle's flag stands one column east and one row south of it.</summary>
    public required int X { get; init; }

    /// <summary>The node's row.</summary>
    public required int Y { get; init; }

    /// <summary>The height the castle's node and its six neighbors were set to.</summary>
    public required int Height { get; init; }
}
