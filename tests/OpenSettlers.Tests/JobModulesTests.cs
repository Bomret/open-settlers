using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The generated registry (ADR 0016) builds the job rules modules of the application layer.
[TestClass]
public sealed class JobModulesTests
{
    [TestMethod]
    public void TheRegistryHoldsTheBuilderTheDiggerAndTheSawmillerInTheOrderOfTheirTypeNames()
    {
        var modules = JobModules.Build(new JobParts(null!, null!, null!, null!, null!, null!, null!, null!, null!));

        CollectionAssert.AreEqual(
            new[] { typeof(Builder), typeof(Digger), typeof(Sawmiller) },
            modules.Select(module => module.JobType).ToArray()
        );
    }

    [TestMethod]
    public void AModuleDoesNotLetItsSettlerStandInTheHutByDefault()
    {
        var modules = JobModules.Build(new JobParts(null!, null!, null!, null!, null!, null!, null!, null!, null!));

        Assert.IsTrue(modules.All(module => !module.CanStandInHut));
    }
}
