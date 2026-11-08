namespace OpenSettlers.Domain;

/// <summary>
/// The steps a free walk has left to take, east and south (a negative number is west or north). The original keeps each in
/// a signed byte, so a value wraps at 128.
/// </summary>
public readonly record struct Vector
{
    /// <summary>A vector of <paramref name="east"/> and <paramref name="south"/> steps, each wrapped to a signed byte.</summary>
    public Vector(int east, int south)
    {
        East = (sbyte)east;
        South = (sbyte)south;
    }

    /// <summary>The steps east; negative for west.</summary>
    public int East { get; }

    /// <summary>The steps south; negative for north.</summary>
    public int South { get; }

    /// <summary>Whether no step is left.</summary>
    public bool IsUsedUp => East == 0 && South == 0;

    /// <summary>The vector after one step in <paramref name="direction"/> (0 east to 5 north, <c>docs/rules/woodcutting.md</c>).</summary>
    public Vector After(Direction direction) =>
        direction.Value switch
        {
            0 => new(East - 1, South),
            1 => new(East - 1, South - 1),
            2 => new(East, South - 1),
            3 => new(East + 1, South),
            4 => new(East + 1, South + 1),
            _ => new(East, South + 1),
        };
}
