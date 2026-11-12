namespace OpenSettlers.Simulation;

/// <summary>
/// The stage of a building site at which <see cref="SitePass"/> asks the castle for a worker: the job rules modules name the worker of
/// each stage (<see cref="JobRules.SiteWorker"/>), so the site pass names no job.
/// </summary>
public enum SiteStage
{
    /// <summary>A large site whose ground is not levelled yet (<c>0x42d52a</c>): the worker who levels it.</summary>
    Levelling,

    /// <summary>A site that has no worker inside or on the way and starts (<c>0x42d5fc</c>): the worker who builds it.</summary>
    Building,
}
