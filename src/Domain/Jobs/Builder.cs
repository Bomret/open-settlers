using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The builder, who walks out to a building site and builds it from the planks and stones on site (<c>docs/rules/construction.md</c>).</summary>
public sealed record Builder : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 3;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(BuilderFigureAdds), typeof(WorkerRequest)];
}
