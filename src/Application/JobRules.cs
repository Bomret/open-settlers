using System;

namespace OpenSettlers.Simulation;

/// <summary>
/// The rules of one job (ADR 0016): a job rules module is a class derived from <see cref="JobRules{TJob}"/> that declares a
/// constructor taking the <see cref="JobParts"/>. A source generator writes <c>JobModules</c>, which builds every module. The shared
/// states of the settlers (the door, the free walk, the step out) call the hooks of the module of the settler's job, and the game's
/// passes call the pass and building hooks of every module. Each hook has a default, so a module overrides only what its job does.
/// </summary>
public abstract class JobRules
{
    private protected JobRules(JobParts parts) => Parts = parts;

    /// <summary>The shared parts the module is built from.</summary>
    protected JobParts Parts { get; }

    /// <summary>The type of the job the module rules.</summary>
    public abstract Type JobType { get; }

    /// <summary>
    /// Whether a layout stands a settler of the job inside a finished building of its own (a hut, as the mission start does). Default: no.
    /// </summary>
    public virtual bool CanStandInHut => false;

    /// <summary>The cause the shared rules record a change of the job's settler under, apart from layouts and walks home. Default: the settlers' rules.</summary>
    public virtual Cause Cause => Cause.Settlers;

    /// <summary>
    /// The request a building site makes for a worker of this job at <paramref name="stage"/>, read from <paramref name="tables"/>, or
    /// null where the job is not the worker of that stage. <see cref="SitePass"/> asks the castle for the one non-null answer of the
    /// modules and refuses a stage two modules answer. Default: null for every stage.
    /// </summary>
    public virtual WorkerRequest? SiteWorker(SiteStage stage, RuleTables tables) => null;

    /// <summary>Registers the behaviour of the activities of the job's own states. Default: none.</summary>
    public virtual void Register(ActivityRegistry registry) { }

    /// <summary>
    /// The start routine of the job, run where the settler's step through the door ends in a building that is not taken down: the
    /// settler's slot, the settler, and the building. Default: nothing.
    /// </summary>
    public virtual void Start(int slot, Settler settler, Building building) { }

    /// <summary>
    /// The arrival at the end of a free walk. Default: the generic arrival home where the job arrives home generically or the walk is
    /// the walk home, else nothing.
    /// </summary>
    public virtual void Arrive(int slot, Settler settler) => Parts.Hut.ArriveByDefault(slot, settler);

    /// <summary>
    /// Run at each turn of a free walk whose timer has run out, before the step; it may change the settler's <see cref="WalksFree"/>.
    /// Default: nothing.
    /// </summary>
    public virtual void WalkTurn(Settler settler) { }

    /// <summary>Run once each pass of the main loop, before the tables. Default: nothing.</summary>
    public virtual void Pass() { }

    /// <summary>Run in the turn of each building that is no site and not taken down, after the roster's handler. Default: nothing.</summary>
    public virtual void Turn(Building building) { }
}

/// <summary>The job rules module of the job <typeparamref name="TJob"/>: at most one module rules a job.</summary>
/// <typeparam name="TJob">The job type.</typeparam>
/// <remarks>Makes the module from the shared parts.</remarks>
/// <param name="parts">The shared parts.</param>
public abstract class JobRules<TJob>(JobParts parts) : JobRules(parts)
    where TJob : Job
{
    /// <inheritdoc />
    public sealed override Type JobType => typeof(TJob);
}
