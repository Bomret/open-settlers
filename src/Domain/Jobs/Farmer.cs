using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The farmer, who sows the nodes around his farm, harvests the ripe fields, and carries the grain to the farm's flag (<c>docs/rules/farming.md</c>).</summary>
[JobName(
    "farmer",
    "Farmer; the type 0xc handler 0x42e25e asks for job 14 with the scythe (tool stock word 0x28), his work sows and harvests the fields 0x69 to 0x6e and 0x79 to 0x7e (0x433a3a) and puts grain (good 4) on the farm's flag (0x43847d); docs/rules/farming.md"
)]
public sealed record Farmer : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 14;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FarmerFigureAdds)];
}
