namespace OpenSettlers.Domain;

/// <summary>
/// The original's random generator: three unsigned 16-bit words that <c>Rng_Step</c> (<c>0x466ade</c>) advances
/// one step at a time (<c>docs/rules/map-start.md</c>). The words are the generator's whole state.
/// </summary>
public readonly record struct RandomGenerator
{
    /// <summary>The word the step writes last; the zero flag the original leaves follows it.</summary>
    public required ushort W0 { get; init; }

    /// <summary>The second state word.</summary>
    public required ushort W1 { get; init; }

    /// <summary>The third state word.</summary>
    public required ushort W2 { get; init; }

    /// <summary>The generator one step on.</summary>
    public RandomGenerator Step()
    {
        var sum = (ushort)(W0 + W1);
        var first = (ushort)(sum ^ W2);
        var third = (ushort)(W2 + W1);
        var second = (ushort)(W1 ^ third);

        return new RandomGenerator
        {
            W0 = first,
            W1 = RotateRight(second),
            W2 = RotateRight(third),
        };
    }

    static ushort RotateRight(ushort word) => (ushort)((word >> 1) | (word << 15));
}
