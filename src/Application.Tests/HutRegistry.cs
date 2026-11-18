using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

/// <summary>
/// The activity registry over a hut of <see cref="HutBench"/>. It stays with the tests because it registers the wait of a step
/// through <c>WalkBehavior.WaitOutStep</c>, which is internal to the application.
/// </summary>
static class HutRegistry
{
    /// <summary>
    /// The registry of the hut work's activities and the modules' (the woodcutter's among them), and the wait of a step that the game's
    /// own walk behaviour handles.
    /// </summary>
    public static ActivityRegistry Registry(BehaviourBench bench, HutWork behaviour, JobParts parts, Causes causes)
    {
        var registry = new ActivityRegistry();
        behaviour.Register(registry);
        foreach (var module in JobModules.Build(parts))
        {
            behaviour.RegisterModule(module);
            module.Register(registry);
        }

        registry.Register<WaitsOutStep>(
            (slot, settler, _) => WalkBehavior.WaitOutStep(bench.Movement, bench.History, causes, slot, settler)
        );

        return registry;
    }
}
