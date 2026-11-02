namespace OpenSettlers.Simulation;

/// <summary>
/// The original's random generator: three unsigned 16-bit words that <c>Rng_Step</c> (<c>0x466ade</c>) advances
/// one step at a time (<c>docs/rules/map-start.md</c>). The words are the generator's whole state.
/// </summary>
/// <param name="W0">The word the step writes last; the zero flag the original leaves follows it.</param>
/// <param name="W1">The second state word.</param>
/// <param name="W2">The third state word.</param>
public readonly record struct RandomGenerator(ushort W0, ushort W1, ushort W2)
{
    /// <summary>The generator one step on.</summary>
    public RandomGenerator Step()
    {
        var sum = (ushort)(W0 + W1);
        var first = (ushort)(sum ^ W2);
        var third = (ushort)(W2 + W1);
        var second = (ushort)(W1 ^ third);

        return new RandomGenerator(first, RotateRight(second), RotateRight(third));
    }

    private static ushort RotateRight(ushort word) => (ushort)((word >> 1) | (word << 15));
}
