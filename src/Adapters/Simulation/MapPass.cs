namespace OpenSettlers.Simulation;

/// <summary>
/// A step of <c>MapGen_Generate</c> after which the generation reports the map and the generator
/// (<c>docs/rules/map-form.md</c>). The names are the harness's names for the passes.
/// </summary>
public enum MapPass
{
    /// <summary>The generator holds the map seed and has stepped twice. The map holds nothing yet.</summary>
    Seed,

    /// <summary><c>MapGen_ClearMap</c> (<c>0x421d3a</c>): the whole map zeroed.</summary>
    ClearMap,

    /// <summary><c>MapGen_SeedHeights</c> (<c>0x421d71</c>): a random height on every 16th column and row.</summary>
    SeedHeights,

    /// <summary><c>MapGen_SubdivideHeights</c> (<c>0x421e75</c>): the heights between the seeded ones, by midpoint subdivision.</summary>
    SubdivideHeights,

    /// <summary><c>MapGen_LimitSlopes</c> (<c>0x42215a</c>): no neighbor to the south, south-east, or east differs by more than 32.</summary>
    LimitSlopes,

    /// <summary><c>MapGen_FillLakes</c> (<c>0x422a0e</c>): the lakes flooded up to the water level and marked as water and shore.</summary>
    FillLakes,

    /// <summary><c>MapGen_LowerToWaterLevel</c> (<c>0x42231f</c>): the heights lowered so that water is 0 and the shore is 1.</summary>
    LowerToWaterLevel,

    /// <summary><c>MapGen_ClassifyTerrain</c> (<c>0x422857</c>): the terrain of each tile's two triangles from their corner heights.</summary>
    ClassifyTerrain,

    /// <summary><c>MapGen_KeepMainLand</c> (<c>0x4223de</c>): the first land mass that covers a quarter of the map kept, the rest water.</summary>
    KeepMainLand,

    /// <summary><c>MapGen_ScaleHeights</c> (<c>0x42237d</c>): the heights scaled down to 0 to 32.</summary>
    ScaleHeights,

    /// <summary>The passes of <c>MapGen_PlaceTerrainAndObjects</c> (<c>0x42336d</c>), in the order it runs them.</summary>
    GradeWaterDepth,

    /// <summary><c>MapGen_SetShoreTerrain</c> (<c>0x423536</c>): terrain 5 next to terrain 3 turned into 4.</summary>
    SetShoreTerrain,

    /// <summary><c>MapGen_PlacePatches</c> (<c>0x4239db</c>): patches of terrain 10 grown on terrain 5.</summary>
    PlacePatches,

    /// <summary><c>MapGen_BlendPatchEdges</c> (<c>0x423d74</c>): the patch edges blended into terrain 5 through the types 7, 8, and 9.</summary>
    BlendPatchEdges,

    /// <summary><c>MapGen_MarkPeaks</c> (<c>0x423fec</c>): an object <c>0x52</c> on every peak above height 25.</summary>
    MarkPeaks,

    /// <summary>The 15 object scatter passes, <c>0x424175</c> to <c>0x4246f4</c>, one after the other.</summary>
    ScatterObjects1,

    /// <summary>The second object scatter pass.</summary>
    ScatterObjects2,

    /// <summary>The third object scatter pass.</summary>
    ScatterObjects3,

    /// <summary>The fourth object scatter pass.</summary>
    ScatterObjects4,

    /// <summary>The fifth object scatter pass.</summary>
    ScatterObjects5,

    /// <summary>The sixth object scatter pass.</summary>
    ScatterObjects6,

    /// <summary>The seventh object scatter pass.</summary>
    ScatterObjects7,

    /// <summary>The eighth object scatter pass.</summary>
    ScatterObjects8,

    /// <summary>The ninth object scatter pass.</summary>
    ScatterObjects9,

    /// <summary>The tenth object scatter pass.</summary>
    ScatterObjects10,

    /// <summary>The eleventh object scatter pass.</summary>
    ScatterObjects11,

    /// <summary>The twelfth object scatter pass.</summary>
    ScatterObjects12,

    /// <summary>The thirteenth object scatter pass.</summary>
    ScatterObjects13,

    /// <summary>The fourteenth object scatter pass.</summary>
    ScatterObjects14,

    /// <summary>The fifteenth object scatter pass.</summary>
    ScatterObjects15,

    /// <summary>The deposits of resource type 3, the first deposit pass (<c>MapGen_PlaceDeposits</c>).</summary>
    PlaceDeposits3,

    /// <summary>The deposits of resource type 2, the second deposit pass.</summary>
    PlaceDeposits2,

    /// <summary>The deposits of resource type 1, the third deposit pass.</summary>
    PlaceDeposits1,

    /// <summary>The deposits of resource type 4, the fourth deposit pass.</summary>
    PlaceDeposits4,

    /// <summary><c>MapGen_ResolveObjectOverlap</c> (<c>0x424c57</c>): a large object removed where it would overlap another.</summary>
    ResolveObjectOverlap,

    /// <summary>The passes of <c>MapGen_Generate</c> that follow <c>MapGen_PlaceTerrainAndObjects</c>.</summary>
    MarkWaterAdjacent,

    /// <summary><c>MapGen_CountResource1</c> (<c>0x421841</c>): the sum of the amounts of the deposits of resource type 1.</summary>
    CountResource1,
}
