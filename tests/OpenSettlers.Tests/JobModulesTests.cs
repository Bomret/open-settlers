using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The generated registry (ADR 0016) builds the job rules modules of the application layer.
[TestClass]
public sealed class JobModulesTests
{
    static System.Collections.Generic.IReadOnlyList<JobRules> Modules() =>
        JobModules.Build(
            new JobParts
            {
                Movement = null!,
                Buildings = null!,
                Flags = null!,
                History = null!,
                Draws = null!,
                LayGood = null!,
                Hut = null!,
            }
        );

    [TestMethod]
    public void TheRegistryHoldsTheModulesOfTheHutsAndTheBuildingJobsInTheOrderOfTheirTypeNames()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                typeof(Builder),
                typeof(Digger),
                typeof(Fisher),
                typeof(Forester),
                typeof(Sawmiller),
                typeof(Stonecutter),
                typeof(Woodcutter),
            },
            Modules().Select(module => module.JobType).ToArray()
        );
    }

    [TestMethod]
    public void OnlyTheModulesOfTheWoodcutterTheStonecutterTheForesterAndTheFisherLetTheirSettlerStandInTheHut()
    {
        CollectionAssert.AreEqual(
            new[] { typeof(Fisher), typeof(Forester), typeof(Stonecutter), typeof(Woodcutter) },
            Modules().Where(module => module.CanStandInHut).Select(module => module.JobType).ToArray()
        );
    }

    [TestMethod]
    public void TheModulesOfTheHutsNameTheCauseOfTheirWork()
    {
        CollectionAssert.AreEqual(
            new[] { Cause.Fishing, Cause.Forestry, Cause.Stonecutting, Cause.Woodcutting },
            Modules().Where(module => module.CanStandInHut).Select(module => module.Cause).ToArray()
        );
    }
}
