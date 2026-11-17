using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The fisher, the settler of the type 1 building (job 11): he walks to a node by the water, casts for fish at the lake details, and
/// carries a catch to his building's flag (<c>docs/rules/fishing.md</c>).
/// </summary>
public sealed record Fisher : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 11;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FisherFigureAdds)];
}
