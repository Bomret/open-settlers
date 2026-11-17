using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal of a finished bakery against the emulator (baker-removals.txt, the verb remove of tools/GhidraDump/emulate.py): the remove
// click on the building takes it down and its baker walks home, from where he stands: waiting for flour, baking, or on the flag with the
// bread; the flour held on site and the flour on its way are lost with the building. The replay is the removal tests'
// (RemovalTests.Replay).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class BakerRemovalTests
{
    const string Fixture = "baker-removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        RemovalTests.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void RemovingABakeryAsksItsBakerHomeAndCountsNoGoodAsOnItsWayAsTheOriginalDoes(string name) =>
        RemovalTests.Replay(Fixture, name, (_, _) => [CommandKind.RemoveBuilding], line => line["sound"] == "4");
}
