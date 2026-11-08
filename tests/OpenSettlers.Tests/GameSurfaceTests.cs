using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game is a coordinator: it applies commands, runs the tick, and builds the digest (ADR 0013). Everything else a caller
// reaches is a read-only part; the behaviours that change the game are internal to the assemblies that build and drive it.
[TestClass]
public sealed class GameSurfaceTests
{
    [TestMethod]
    public void TheGamePublishesOnlyEnqueueTickAndDigestAsMethods()
    {
        var methods = typeof(Game)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order();

        CollectionAssert.AreEqual(new[] { "Digest", "Enqueue", "Tick" }, methods.ToArray());
    }

    [TestMethod]
    public void EveryPublicPropertyOfTheGameIsReadOnly()
    {
        var writable = typeof(Game)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name);

        CollectionAssert.AreEqual(System.Array.Empty<string>(), writable.ToArray());
    }

    [TestMethod]
    public void TheGamePublishesOnlyTheCountersAndTheStateItsCallersRead()
    {
        var properties = typeof(Game)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order();
        string[] expected =
        [
            "Buildings",
            "CastleStock",
            "Clock",
            "Flags",
            "History",
            "Map",
            "Network",
            "Revision",
            "RoadPlanner",
            "Settlers",
            "Tables",
            "TickCount",
        ];

        CollectionAssert.AreEqual(expected.Order().ToArray(), properties.ToArray());
    }

    [TestMethod]
    public void TheGameLivesInTheApplicationAssembly()
    {
        Assert.AreEqual("OpenSettlers.Application", typeof(Game).Assembly.GetName().Name);
    }
}
