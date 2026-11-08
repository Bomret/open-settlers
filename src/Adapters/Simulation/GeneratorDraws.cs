namespace OpenSettlers.Simulation;

/// <summary>
/// The game's random generator behind the <see cref="IRandomDraws"/> port: it continues from the generator the map's
/// generation left, as the original's does.
/// </summary>
/// <param name="start">The generator at the start.</param>
internal sealed class GeneratorDraws(RandomGenerator start) : IRandomDraws
{
    /// <summary>The generator as the draws so far left it; its three words are the generator's whole state.</summary>
    public RandomGenerator Generator { get; private set; } = start;

    /// <inheritdoc />
    public ushort Draw()
    {
        Generator = Generator.Step();

        return Generator.W0;
    }
}
