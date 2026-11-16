using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The miller, who grinds the grain the mill holds into flour and carries each sack of flour out to the flag (<c>docs/rules/milling.md</c>).</summary>
public sealed record Miller : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 15;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(MillerFigureAdds)];
}
