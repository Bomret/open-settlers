using System;
using System.Collections.Generic;

namespace OpenSettlers.Domain;

/// <summary>
/// The pig farmer, who raises pigs on the grain the pig farm holds and carries each pig out to the flag (<c>docs/rules/pig-farming.md</c>).
/// </summary>
public sealed record PigFarmer : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 12;

    /// <inheritdoc />
    public override int Code => Number;

    /// <inheritdoc />
    public override IReadOnlyList<Type> Data => [typeof(PigFarmerFigureAdds), typeof(PigFarmRates)];
}
