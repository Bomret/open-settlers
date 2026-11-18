using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The forester, who plants saplings on free meadow nodes around his hut (<c>docs/rules/forestry.md</c>).</summary>
[JobName(
    "forester",
    "Forester; the type 9 handler 0x42d9c9 asks for job 8 with no tool, and his work plants the saplings 0x67 and 0x68 (0x4389bf); docs/rules/forestry.md"
)]
public sealed record Forester : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 8;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(ForesterFigureAdds)];
}
