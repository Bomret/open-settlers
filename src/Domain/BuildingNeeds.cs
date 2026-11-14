namespace OpenSettlers.Domain;

/// <summary>The planks and stones a building type needs to be built (the program's table at <c>0x449f18</c>).</summary>
public readonly record struct BuildingNeeds
{
    /// <summary>The planks the type needs.</summary>
    public required int Planks { get; init; }

    /// <summary>The stones the type needs.</summary>
    public required int Stones { get; init; }
}
