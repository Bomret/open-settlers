using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The baker, who bakes the flour the bakery holds into bread and carries each loaf out to the flag (<c>docs/rules/baking.md</c>).</summary>
[JobName(
    "baker",
    "Baker; the handler 0x42e37e of the building type 0x10 asks for job 16 with no tool, its start 0x43d3d9 asks for flour (class 5) and its state 0x24 takes flour out of the building (0x433694) and carries bread (good 6, 0x43384f) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/baking.md"
)]
public sealed record Baker : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 16;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds)];
}
