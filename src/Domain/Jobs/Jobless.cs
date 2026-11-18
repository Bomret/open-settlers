namespace OpenSettlers.Domain;

/// <summary>A settler without a job yet.</summary>
[JobName("jobless", "Jobless: 21 is a settler without a job yet")]
public sealed record Jobless : Job
{
    /// <summary>The job's number.</summary>
    public const int Number = 21;

    /// <inheritdoc />
    public override int? OriginalCode => Number;
}
