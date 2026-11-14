namespace OpenSettlers.Domain;

/// <summary>
/// What a building asks the castle for when it needs a worker (<c>Building_RequestWorker</c>, <c>0x42be63</c>): the job, and the tool
/// the job needs where no settler of the job waits and a jobless settler takes it. The handlers of the building types and of the
/// building under construction pass both (<c>docs/rules/construction.md</c>). A request is never without a job.
/// </summary>
public sealed record WorkerRequest
{
    /// <summary>The good the jobless settler takes from the stock with the job, or <see cref="Good.None"/> for a job that needs none.</summary>
    public required Good Tool { get; init; }

    readonly Job _job = null!;

    /// <summary>The job of the worker.</summary>
    /// <exception cref="System.ArgumentNullException">The job is null, at construction or in a copy made with <c>with</c>.</exception>
    public required Job Job
    {
        get => _job;
        init => _job = value ?? throw new System.ArgumentNullException(nameof(Job));
    }
}
