using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The fisher, the settler of the type 1 building (job 11): he walks to a node by the water, casts for fish at the lake details, and
/// carries a catch to his building's flag (<c>docs/rules/fishing.md</c>).
/// </summary>
[JobName(
    "fisher",
    "Fisher, inferred: the type 1 handler 0x42e0b8 asks for job 11 with the tool of stock word 0x24 (good 18), and his work casts at the lake details (0x433e05) and puts good 1 on the flag (0x438232); docs/rules/fishing.md"
)]
public sealed record Fisher : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 11;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FisherFigureAdds)];
}
