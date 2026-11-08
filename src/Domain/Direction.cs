namespace OpenSettlers.Domain;

/// <summary>
/// One of the six directions of the map's node grid, numbered as the original numbers them (0 to 5). The direction
/// opposite to a direction is the one three on.
/// </summary>
/// <param name="Value">The direction's number, 0 to 5.</param>
public readonly record struct Direction(int Value)
{
    /// <summary>The number of directions.</summary>
    public const int Count = 6;

    /// <summary>
    /// The column and row each of the six directions moves by: 0 east, 1 south-east, 2 south, 3 west, 4 north-west,
    /// 5 north.
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<(int Dx, int Dy)> Steps { get; } =
        [(1, 0), (1, 1), (0, 1), (-1, 0), (-1, -1), (0, -1)];

    /// <summary>The direction that leads back along a step in this direction.</summary>
    public Direction Opposite => new((Value + 3) % Count);

    /// <summary>A direction from its number.</summary>
    public static implicit operator Direction(int value) => new(value);

    /// <summary>A direction's number.</summary>
    public static implicit operator int(Direction direction) => direction.Value;

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
