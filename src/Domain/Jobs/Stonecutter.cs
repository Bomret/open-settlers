using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>The stonecutter, who cuts stone heaps and carries the stones to his hut's flag (<c>docs/rules/stonecutting.md</c>).</summary>
[JobName(
    "stonecutter",
    "Stonecutter; his work puts good 10 (a stone) on the hut's flag (0x438182) and cuts the stone heaps 0x48 to 0x4f (0x435c5c); docs/rules/stonecutting.md"
)]
public sealed record Stonecutter : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 7;

    /// <inheritdoc />
    public override int? OriginalCode => Number;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => false;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(FigureAdds)];
}
