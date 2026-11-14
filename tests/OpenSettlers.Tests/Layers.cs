using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The original's encoding of a typed map, for the tests that compare a map with values observed in the original (the
/// emulator's digests, recovered bytes). Rule tests assert typed state instead.
/// </summary>
static class Layers
{
    /// <summary>The bytes of a layer of the map's encoding.</summary>
    public static byte[] Of(Map map, MapLayer layer) => MapLayerExporter.Export(map).Layer(layer).ToArray();

    /// <summary>The digest of a layer of the map's encoding.</summary>
    public static ulong Digest(Map map, MapLayer layer) => MapLayerExporter.Export(map).Digest(layer);
}
