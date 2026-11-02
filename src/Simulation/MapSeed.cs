namespace OpenSettlers.Simulation;

/// <summary>
/// The three unsigned 16-bit words the map generator loads into the random generator
/// (<c>docs/rules/map-start.md</c>). A free game derives them from its <see cref="Seed"/>.
/// </summary>
public readonly record struct MapSeed(ushort W0, ushort W1, ushort W2);
