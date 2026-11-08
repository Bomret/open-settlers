namespace OpenSettlers.Domain;

/// <summary>
/// The class of a road's length (0 to 7, <c>0x4454e7</c>): it sets how many carriers the road holds
/// (<see cref="RuleTables.RoadCapacity"/>).
/// </summary>
/// <param name="Value">The class, 0 to 7.</param>
public readonly record struct LengthClass(int Value)
{
    /// <summary>The class of a road of <paramref name="steps"/> steps.</summary>
    public static LengthClass OfSteps(int steps) =>
        new(
            steps switch
            {
                <= 3 => 0,
                <= 5 => 1,
                6 => 2,
                <= 9 => 3,
                <= 12 => 4,
                <= 17 => 5,
                <= 23 => 6,
                _ => 7,
            }
        );

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
