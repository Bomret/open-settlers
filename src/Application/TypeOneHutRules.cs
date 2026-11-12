using System;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The rules module of the type 1 hut's settler (<c>docs/rules/settlers.md</c>). His work, a search for a free node by the water, is not
/// ported: he starts nothing at the door, arrives home only on a walk home, and his events are the settlers'. A layout stands no worker
/// in his hut.
/// </summary>
/// <param name="parts">The shared parts.</param>
public sealed class TypeOneHutRules(JobParts parts) : JobRules<TypeOneHut>(parts);

/// <summary>
/// The rules of every job that has no module of its own: the door starts nothing, the cause is the settlers', and the arrival is
/// <see cref="JobRules.Arrive"/>'s default. <see cref="HutWork"/> takes them for a settler whose job has no registered module.
/// </summary>
/// <param name="parts">The shared parts.</param>
internal sealed class UnruledRules(JobParts parts) : JobRules(parts)
{
    /// <inheritdoc />
    public override Type JobType => typeof(Job);
}
