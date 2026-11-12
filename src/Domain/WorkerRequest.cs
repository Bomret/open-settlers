namespace OpenSettlers.Domain;

/// <summary>
/// What a building asks the castle for when it needs a worker (<c>Building_RequestWorker</c>, <c>0x42be63</c>): the job, and the tool
/// the job needs where no settler of the job waits and a jobless settler takes it. The handlers of the building types and of the
/// building under construction pass both (<c>docs/rules/construction.md</c>). A request is never without a job.
/// </summary>
/// <param name="Job">The job of the worker.</param>
/// <param name="Tool">The good the jobless settler takes from the stock with the job, or <see cref="Good.None"/> for a job that needs none.</param>
public sealed record WorkerRequest(Job Job, Good Tool)
{
    /// <summary>The job of the worker.</summary>
    public Job Job { get; } = Job ?? throw new System.ArgumentNullException(nameof(Job));
}
