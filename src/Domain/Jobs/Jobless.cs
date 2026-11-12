namespace OpenSettlers.Domain;

/// <summary>A settler without a job yet.</summary>
public sealed record Jobless : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 21;

    /// <inheritdoc />
    public override int Code => Number;
}
