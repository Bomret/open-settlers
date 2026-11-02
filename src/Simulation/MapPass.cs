namespace OpenSettlers.Simulation;

/// <summary>
/// A step of <c>MapGen_Generate</c> after which the generation reports the map and the generator
/// (<c>docs/rules/map-form.md</c>). The names are the harness's names for the passes.
/// </summary>
public enum MapPass
{
    /// <summary>The generator holds the map seed and has stepped twice. The map holds nothing yet.</summary>
    Seed,
    ClearMap,
    SeedHeights,
    SubdivideHeights,
    LimitSlopes,
    FillLakes,
    LowerToWaterLevel,
    ClassifyTerrain,
    KeepMainLand,
    ScaleHeights,
}
