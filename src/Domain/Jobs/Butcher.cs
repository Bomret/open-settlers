using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The butcher, who turns each pig the butcher's shop holds into meat and carries it out to the flag (<c>docs/rules/butchering.md</c>).</summary>
[JobName(
    "butcher",
    "Butcher; the handler 0x42e1b3 of the building type 0xd asks for job 13 with the tool good 19 (stock word 0x26), its start 0x43d275 asks for pigs (class 3) and its state 0x26 takes a pig out of the building (0x4332a8) and carries meat (good 3, 0x4333a5) out to the flag; inferred name, docs/rules/food-buildings.md, docs/rules/butchering.md"
)]
public sealed record Butcher : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 13;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FoodHutFigureAdds)];
}
