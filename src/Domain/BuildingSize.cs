namespace OpenSettlers.Domain;

/// <summary>
/// How much room a building type takes, as the program's size table gives it (<c>0x42d307</c>, <c>docs/rules/build-sites.md</c>):
/// the site value the build-site evaluator must reach for the type, and the object code its node takes.
/// </summary>
public enum BuildingSize
{
    /// <summary>A mine: it needs the mountain site (2).</summary>
    Mine,

    /// <summary>A small building: it needs the site of a small or a large building (3 or 4); its node takes the object code 2.</summary>
    Small,

    /// <summary>A large building: it needs the site of a large building (4); its node takes the object code 3.</summary>
    Large,
}
