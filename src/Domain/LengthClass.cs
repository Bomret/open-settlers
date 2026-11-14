namespace OpenSettlers.Domain;

/// <summary>
/// The class of a road's length (0 to 7, <c>0x4454e7</c>): it sets how many carriers the road holds
/// (<see cref="RuleTables.RoadCapacity"/>).
/// </summary>
public readonly record struct LengthClass
{
    /// <summary>The class, 0 to 7.</summary>
    public required int Value { get; init; }

    /// <summary>The class of a road of <paramref name="steps"/> steps.</summary>
    public static LengthClass OfSteps(int steps) =>
        new()
        {
            Value = steps switch
            {
                <= 3 => 0,
                <= 5 => 1,
                6 => 2,
                <= 9 => 3,
                <= 12 => 4,
                <= 17 => 5,
                <= 23 => 6,
                _ => 7,
            },
        };

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
