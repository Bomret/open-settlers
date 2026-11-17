using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal of a finished pig farm against the emulator (pig-removals.txt, the verb remove of tools/GhidraDump/emulate.py): the remove
// click on the building takes it down and its pig farmer walks home, from where he stands: waiting for grain, working or pausing, or on
// the flag with the pig; the grain held on site and the grain on its way are lost with the building. The replay is the removal tests'
// (RemovalTests.Replay).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class PigFarmRemovalTests
{
    const string Fixture = "pig-removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        RemovalTests.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void RemovingAPigFarmSendsItsPigFarmerHomeAndCountsNoGoodAsOnItsWayAsTheOriginalDoes(string name) =>
        RemovalTests.Replay(Fixture, name, (_, _) => [CommandKind.RemoveBuilding], line => line["sound"] == "4");
}
