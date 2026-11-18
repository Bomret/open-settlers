using System;

namespace OpenSettlers.Domain;

/// <summary>
/// A job the engine does not port yet, which has no stable name: the original's raw number is all it holds. Two unported jobs are
/// equal where their numbers are.
/// </summary>
/// <param name="Raw">The job's number; a number another job owns is refused.</param>
public sealed record UnportedJob(int Raw) : Job
{
    /// <summary>The job's number as the original holds it.</summary>
    public int Raw { get; } =
        IsPorted(Raw) ? throw new ArgumentException($"The number {Raw} belongs to another job.", nameof(Raw)) : Raw;

    /// <inheritdoc />
    public override int? OriginalCode => Raw;

    /// <summary>
    /// Whether the settler arrives at the end of a free walk home generically (<see cref="Job.ArrivesHomeGenerically"/>). The original's table
    /// excepts some of the jobs the engine does not port, so the one that builds the job states it; the default is generic.
    /// </summary>
    public bool Generic { get; init; } = true;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => Generic;

    /// <summary>Whether <paramref name="other"/> holds the same number.</summary>
    public bool Equals(UnportedJob? other) => other is not null && Raw == other.Raw;

    /// <inheritdoc />
    public override int GetHashCode() => Raw;
}
