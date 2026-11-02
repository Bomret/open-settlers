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

    /// <summary>The passes of <c>MapGen_PlaceTerrainAndObjects</c> (<c>0x42336d</c>), in the order it runs them.</summary>
    GradeWaterDepth,
    SetShoreTerrain,
    PlacePatches,
    BlendPatchEdges,
    MarkPeaks,

    /// <summary>The 15 object scatter passes, <c>0x424175</c> to <c>0x4246f4</c>, one after the other.</summary>
    ScatterObjects1,
    ScatterObjects2,
    ScatterObjects3,
    ScatterObjects4,
    ScatterObjects5,
    ScatterObjects6,
    ScatterObjects7,
    ScatterObjects8,
    ScatterObjects9,
    ScatterObjects10,
    ScatterObjects11,
    ScatterObjects12,
    ScatterObjects13,
    ScatterObjects14,
    ScatterObjects15,
    PlaceDeposits3,
    PlaceDeposits2,
    PlaceDeposits1,
    PlaceDeposits4,
    ResolveObjectOverlap,

    /// <summary>The passes of <c>MapGen_Generate</c> that follow <c>MapGen_PlaceTerrainAndObjects</c>.</summary>
    MarkWaterAdjacent,
    CountResource1,
}
