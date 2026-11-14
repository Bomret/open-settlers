using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// One step of the order in which a storage hands its goods out to requests (the program's tables at <c>0x42a313</c>,
/// <c>0x42a35d</c>, and <c>0x42a3a7</c>, <c>docs/rules/construction-materials.md</c>): the flags asking for the class
/// <see cref="Class"/> (in its request word) are served the good of <see cref="Goods"/> the storage holds most of; a step of one good
/// serves that good, the step of the food goods names three.
/// </summary>
public readonly record struct DistributionStep
{
    /// <summary>The class the step looks for, with the request word it looks in.</summary>
    public required GoodClass Class { get; init; }

    /// <summary>The goods the step hands out: the storage serves the one with the greatest count (the first of equals).</summary>
    public required IReadOnlyList<Good> Goods { get; init; }
}
