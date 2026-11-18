using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The builder, who walks out to a building site and builds it from the planks and stones on site (<c>docs/rules/construction.md</c>).</summary>
[JobName(
    "builder",
    "Builder; the under-construction handler 0x42d55f asks for job 3 with the tool good 17 (request at 0x42d5fc) and the job's state 9 builds the site (0x43ec82); docs/rules/construction.md"
)]
public sealed record Builder : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 3;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(BuilderFigureAdds), typeof(WorkerRequest)];
}
