namespace OpenSettlers.Domain;

/// <summary>The planks and stones a building type needs to be built (the program's table at <c>0x449f18</c>).</summary>
/// <param name="Planks">The planks the type needs.</param>
/// <param name="Stones">The stones the type needs.</param>
public readonly record struct BuildingNeeds(int Planks, int Stones);
