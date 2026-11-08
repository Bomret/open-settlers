namespace OpenSettlers.Domain;

/// <summary>What the land of a triangle can bear, as the build-site evaluator groups the terrain codes (<c>0x44be72</c>).</summary>
public enum TerrainKind
{
    /// <summary>Terrain codes 0 to 3: water.</summary>
    Water,

    /// <summary>Terrain codes 4 to 7: plain land.</summary>
    Plain,

    /// <summary>Terrain codes 11 to 14: mountain.</summary>
    Mountain,

    /// <summary>The other codes (8 to 10 and 15): neither plain land nor mountain, and not built on.</summary>
    Other,
}

/// <summary>
/// The terrain of one triangle of a tile. A tile has an upper and a lower triangle; the original keeps them in the high
/// and the low nibble of one byte (<c>docs/rules/map-form.md</c>).
/// </summary>
/// <param name="Code">The original's terrain code, 0 to 15; kept raw because most codes have no recovered meaning.</param>
public readonly record struct Terrain(int Code)
{
    /// <summary>The code the generation gives the lowest land (<c>MapGen_ClassifyTerrain</c>), the one a castle site needs.</summary>
    public static Terrain Lowland => new(5);

    /// <summary>True for the water codes 0 to 3.</summary>
    public bool IsWater => Code < 4;

    /// <summary>The group the build-site evaluator puts the code in.</summary>
    public TerrainKind Kind =>
        Code switch
        {
            < 4 => TerrainKind.Water,
            < 8 => TerrainKind.Plain,
            > 10 and < 15 => TerrainKind.Mountain,
            _ => TerrainKind.Other,
        };
}
