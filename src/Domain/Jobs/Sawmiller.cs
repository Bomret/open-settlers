using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The sawmiller, who saws the logs the sawmill holds into planks and carries each plank out to the flag (<c>docs/rules/sawmilling.md</c>).</summary>
[JobName(
    "sawmiller",
    "Sawmiller; the handler 0x42da0b of the building type 0x11 asks for job 6 with the tool good 22 and the job's state 0x18 takes a log out of the building (0x4359fe) and carries the plank (good 8) out to the flag (0x43c6aa); docs/rules/sawmilling.md"
)]
public sealed record Sawmiller : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 6;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(SawmillerFigureAdds)];
}
