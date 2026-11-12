using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The generated registry (ADR 0016) builds the job rules modules of the application layer.
[TestClass]
public sealed class JobModulesTests
{
    private static System.Collections.Generic.IReadOnlyList<JobRules> Modules() =>
        JobModules.Build(new JobParts(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!));

    [TestMethod]
    public void TheRegistryHoldsTheModulesOfTheHutsAndTheBuildingJobsInTheOrderOfTheirTypeNames()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(Builder),
                typeof(Digger),
                typeof(Forester),
                typeof(Sawmiller),
                typeof(Stonecutter),
                typeof(TypeOneHut),
                typeof(Woodcutter),
            },
            Modules().Select(module => module.JobType).ToArray()
        );
    }

    [TestMethod]
    public void OnlyTheModulesOfTheWoodcutterTheStonecutterAndTheForesterLetTheirSettlerStandInTheHut()
    {
        CollectionAssert.AreEqual(
            new[] { typeof(Forester), typeof(Stonecutter), typeof(Woodcutter) },
            Modules().Where(module => module.CanStandInHut).Select(module => module.JobType).ToArray()
        );
    }

    [TestMethod]
    public void TheModulesOfTheHutsNameTheCauseOfTheirWork()
    {
        CollectionAssert.AreEqual(
            new[] { Cause.Forestry, Cause.Stonecutting, Cause.Woodcutting },
            Modules().Where(module => module.CanStandInHut).Select(module => module.Cause).ToArray()
        );
    }
}
