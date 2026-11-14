namespace OpenSettlers.Simulation;

/// <summary>
/// The three unsigned 16-bit words the map generator loads into the random generator
/// (<c>docs/rules/map-start.md</c>). A free game derives them from its <see cref="Seed"/>.
/// </summary>
public readonly record struct MapSeed
{
    /// <summary>The first of the map seed's three 16-bit words.</summary>
    public required ushort W0 { get; init; }

    /// <summary>The second of the map seed's three 16-bit words.</summary>
    public required ushort W1 { get; init; }

    /// <summary>The third of the map seed's three 16-bit words.</summary>
    public required ushort W2 { get; init; }
}
