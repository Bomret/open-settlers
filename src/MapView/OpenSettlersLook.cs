using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>
/// The look of a job under the Open Settlers source: the name its frames carry in the content set's manifest
/// (<see cref="OpenSettlersSource.SettlerKey"/>). A job without a look draws nothing under the source.
/// </summary>
public abstract class OpenSettlersLook
{
    /// <summary>The job the look is for.</summary>
    public abstract Job Job { get; }

    /// <summary>The job's name in the manifest keys (<c>carrier</c>, <c>woodcutter</c>, <c>type-one-hut</c>).</summary>
    public abstract string Key { get; }
}

/// <summary>
/// The Open Settlers look of the job <typeparamref name="TJob"/> (ADR 0016, glossary: Job module). A look derives from this type
/// and declares a constructor without parameters; the generator writes <c>OpenSettlersLookRegistry</c> from the looks it finds, and
/// fails the build for a look with no such constructor and for a second look of one job.
/// </summary>
/// <typeparam name="TJob">The job the look is for.</typeparam>
/// <param name="key">The job's name in the manifest keys.</param>
public abstract class OpenSettlersLook<TJob>(string key) : OpenSettlersLook
    where TJob : Job, new()
{
    /// <inheritdoc />
    public sealed override Job Job { get; } = new TJob();

    /// <inheritdoc />
    public sealed override string Key { get; } = key;
}
