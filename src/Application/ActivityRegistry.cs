using System;
using System.Collections.Generic;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The behaviour of each activity type: what a settler does at its turn while it is in that activity. An activity type takes
/// exactly one behaviour. A behaviour not yet moved out of the game registers here from where it lives.
/// </summary>
public sealed class ActivityRegistry
{
    private readonly Dictionary<Type, Action<int, Settler>> _behaviours = [];

    /// <summary>The activity types that have a behaviour.</summary>
    public IReadOnlyCollection<Type> Registered => _behaviours.Keys;

    /// <summary>
    /// Registers the behaviour of <typeparamref name="TActivity"/>: it gets the slot of the settler, the settler, and the settler's
    /// activity. Throws <see cref="InvalidOperationException"/> where the type has a behaviour already.
    /// </summary>
    public void Register<TActivity>(Action<int, Settler, TActivity> behaviour)
        where TActivity : Activity
    {
        if (
            !_behaviours.TryAdd(
                typeof(TActivity),
                (slot, settler) => behaviour(slot, settler, (TActivity)settler.Activity)
            )
        )
        {
            throw new InvalidOperationException($"{typeof(TActivity).Name} has a behaviour already");
        }
    }

    /// <summary>
    /// Hands the settler in the slot to the behaviour of its activity type. Throws <see cref="InvalidOperationException"/> where
    /// the type has none.
    /// </summary>
    public void Handle(int slot, Settler settler)
    {
        if (!_behaviours.TryGetValue(settler.Activity.GetType(), out var behaviour))
        {
            throw new InvalidOperationException($"{settler.Activity.GetType().Name} has no behaviour");
        }

        behaviour(slot, settler);
    }
}
