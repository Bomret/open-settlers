using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The farmer, who sows the nodes around his farm, harvests the ripe fields, and carries the grain to the farm's flag (<c>docs/rules/farming.md</c>).</summary>
public sealed record Farmer : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 14;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FarmerFigureAdds)];
}
