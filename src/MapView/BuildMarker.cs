using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A build marker (<c>CONTEXT.md</c>, "Build-site overlay"): the draw term of the sign the build-site overlay shows on a
/// node for what may be put there. It holds the kind alone, no entry number (INV-0010).
/// </summary>
public enum BuildMarker
{
    /// <summary>A flag may be put there.</summary>
    Flag,

    /// <summary>A mine may be put there.</summary>
    Mine,

    /// <summary>A small building may be put there.</summary>
    Small,

    /// <summary>A large building may be put there.</summary>
    Large,
}

/// <summary>
/// The rule that picks the build marker for the build-site evaluator's judgment of a node, as the original's
/// <c>0x450d85</c> picks it (<c>docs/rules/build-sites.md</c>, "The build-site overlay").
/// </summary>
public static class BuildMarkers
{
    /// <summary>
    /// The marker for <paramref name="result"/>, or null where the original draws none: a node state of 3 or lower, a
    /// site of 0, and the evaluator's default result. A node state of 4 with any other site shows a flag. A node state of 5
    /// or more shows the marker of its site: a flag for 1, a mine for 2, a small building for 3, and a large building for 4
    /// and for the castle site 5. A node state of 5 with site 1 shows a flag although the flag click refuses that node
    /// (<see cref="BuildSiteResult.AllowsFlag"/>).
    /// </summary>
    public static BuildMarker? Of(BuildSiteResult result) =>
        result.Node <= 3 || result.Site == 0 ? null
        : result.Node == 4 ? BuildMarker.Flag
        : result.Site switch
        {
            1 => BuildMarker.Flag,
            2 => BuildMarker.Mine,
            3 => BuildMarker.Small,
            _ => BuildMarker.Large,
        };
}
