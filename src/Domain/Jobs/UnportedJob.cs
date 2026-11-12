using System;

namespace OpenSettlers.Domain;

/// <summary>A job the engine does not port yet: any number a byte holds that no other job owns.</summary>
/// <param name="Code">The job's number; a number another job owns is refused.</param>
public sealed record UnportedJob(int Code) : Job
{
    /// <inheritdoc />
    public override int Code { get; } =
        IsPorted(Code) ? throw new ArgumentException($"The number {Code} belongs to another job.", nameof(Code)) : Code;

    /// <inheritdoc />
    public override bool ArrivesHomeGenerically => !(Code is 14 or 20 or >= FirstKnight);
}
