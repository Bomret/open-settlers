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
    /// The table at 0x437dfb excepts the jobs 14, 20, and 22 and up from the generic arrival; the number is all an unported job has to go by,
    /// and its rules stay stand-ins.
    /// </summary>
    public override bool ArrivesHomeGenerically => !(Raw is 14 or 20 or >= 22);

    /// <summary>Whether <paramref name="other"/> holds the same number.</summary>
    public bool Equals(UnportedJob? other) => other is not null && Raw == other.Raw;

    /// <inheritdoc />
    public override int GetHashCode() => Raw;
}
