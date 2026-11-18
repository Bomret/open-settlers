using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The miller, who grinds the grain the mill holds into flour and carries each sack of flour out to the flag (<c>docs/rules/milling.md</c>).</summary>
[JobName(
    "miller",
    "Miller; the handler 0x42e2a0 of the building type 0xf asks for job 15 with no tool, its start 0x43d33d asks for grain (class 4) and its state 0x23 takes grain out of the building (0x43386b) and carries flour (good 5, 0x433a1e) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/milling.md"
)]
public sealed record Miller : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 15;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds)];
}
