using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The digger, who levels the ground of a large building site before its builder starts (<c>docs/rules/digging.md</c>).</summary>
public sealed record Digger : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 2;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FigureAdds), typeof(WorkerRequest)];
}
