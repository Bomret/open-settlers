namespace OpenSettlers.Domain;

/// <summary>
/// What a building under construction still holds: the planks and stones its type needs and the height its node is levelled
/// to (<c>Building_Place</c>, <c>0x449a63</c>, <c>docs/rules/build-sites.md</c>). A <see cref="Building"/> whose
/// <see cref="Building.Site"/> is set is a building site.
/// </summary>
public sealed record BuildingSite
{
    /// <summary>The planks the building still needs: the type's count from the needs table (<c>0x449f18</c>).</summary>
    public required int Planks { get; init; }

    /// <summary>The stones the building still needs: the type's count from the needs table.</summary>
    public required int Stones { get; init; }

    /// <summary>The height the node is levelled to: the evaluator's height at the placement (0 where the node's site value is 3).</summary>
    public required int TargetHeight { get; init; }
}
