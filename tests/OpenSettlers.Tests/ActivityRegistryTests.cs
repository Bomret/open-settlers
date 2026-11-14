using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The table pass hands each settler to the behaviour registered for its activity type (INV-0008: the registry lives in the
// application layer; the behaviours the game registers live in the adapters).
[TestClass]
public sealed class ActivityRegistryTests
{
    static IEnumerable<Type> ActivityTypes() =>
        typeof(Activity).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Activity)) && !type.IsAbstract);

    static Game NewGame() =>
        GameFactory.Create(
            LayoutSettlersTests.MadeTables(),
            MapGenerator.Generate(TestProgram.Made(), new MapSeed(0x1234, 0x5678, 0x9ABC), MapSize.Offered[0])
        );

    [TestMethod]
    public void EveryActivityTypeHasABehaviourRegistered()
    {
        var registered = NewGame().Behaviours.Registered;

        var unhandled = ActivityTypes().Except(registered).Select(type => type.Name).ToList();

        Assert.AreEqual(0, unhandled.Count, $"no behaviour for: {string.Join(", ", unhandled)}");
    }

    [TestMethod]
    public void NoBehaviourIsRegisteredForAnythingButAnActivityType()
    {
        var registered = NewGame().Behaviours.Registered;

        Assert.AreEqual(0, registered.Except(ActivityTypes()).Count());
    }

    [TestMethod]
    public void AnActivityTypeTakesExactlyOneBehaviour()
    {
        var registry = new ActivityRegistry();
        registry.Register<WaitsInCastle>((_, _, _) => { });

        Assert.ThrowsExactly<InvalidOperationException>(() => registry.Register<WaitsInCastle>((_, _, _) => { }));
    }

    [TestMethod]
    public void TheRegistryHandsASettlerToTheBehaviourOfItsActivityType()
    {
        var registry = new ActivityRegistry();
        var seen = new List<string>();
        registry.Register<WaitsInCastle>((slot, _, activity) => seen.Add($"castle {slot} {activity.Storage}"));
        registry.Register<BuildsCastle>((slot, _, _) => seen.Add($"builds {slot}"));

        registry.Handle(5, new Settler(new Carrier(), 0, 0, 0, 0, new WaitsInCastle(2)));
        registry.Handle(6, new Settler(new CastleSettler(), 0, 0, 0, 0, new BuildsCastle()));

        CollectionAssert.AreEqual(new[] { "castle 5 2", "builds 6" }, seen);
    }

    [TestMethod]
    public void ASettlerWhoseActivityHasNoBehaviourIsAnError()
    {
        var registry = new ActivityRegistry();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            registry.Handle(0, new Settler(new Carrier(), 0, 0, 0, 0, new BuildsCastle()))
        );
    }
}
