using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The digger, who levels the ground of a large building site before its builder starts (<c>docs/rules/digging.md</c>).</summary>
[JobName(
    "digger",
    "Digger; the large site's handler 0x42d328 asks for job 2 with the tool good 16 (request at 0x42d52a) and the job's state 8 changes the heights of the site's node and its six neighbours by one a pass (0x43e45e); docs/rules/digging.md"
)]
public sealed record Digger : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 2;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FigureAdds), typeof(WorkerRequest)];
}
