using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The pig farmer, who raises pigs on the grain the pig farm holds and carries each pig out to the flag (<c>docs/rules/pig-farming.md</c>).
/// </summary>
[JobName(
    "pig-farmer",
    "Pig farmer; the handler 0x42e0fa of the building type 0xe asks for job 12 with no tool, its start 0x43d179 asks for grain (class 4) and its state 0x25 takes grain out of the building (0x4333c1) and carries a pig (good 2, 0x433577) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/pig-farming.md"
)]
public sealed record PigFarmer : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 12;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds), typeof(PigFarmRates)];
}
