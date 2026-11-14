using System.Linq;

namespace OpenSettlers.Tests;

/// <summary>Reads of the typed map that several tests share.</summary>
static class TestMaps
{
    /// <summary>The object on each node, in row-major node order.</summary>
    public static MapObject[] Objects(Map map) =>
        [.. Enumerable.Range(0, map.Count).Select(index => map.At(index).Object)];

    /// <summary>The roads that leave each node, in row-major node order.</summary>
    public static RoadSet[] Roads(Map map) => [.. Enumerable.Range(0, map.Count).Select(index => map.At(index).Roads)];
}
