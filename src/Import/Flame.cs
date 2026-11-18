namespace OpenSettlers.Import;

/// <summary>The size of a flame over a burning building (<c>CONTEXT.md</c>, "Flame").</summary>
public enum FlameSize
{
    /// <summary>A small flame.</summary>
    Small,

    /// <summary>A large flame.</summary>
    Large,
}

/// <summary>
/// One flame of a burning building as the graphics source places it (ADR 0021): its size and its offset in map pixels from
/// the building's draw position.
/// </summary>
public sealed record Flame
{
    /// <summary>The flame's size.</summary>
    public required FlameSize Size { get; init; }

    /// <summary>The horizontal offset in map pixels from the building's draw position.</summary>
    public required int X { get; init; }

    /// <summary>The vertical offset in map pixels from the building's draw position.</summary>
    public required int Y { get; init; }
}
