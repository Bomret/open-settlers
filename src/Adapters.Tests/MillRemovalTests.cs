using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal of a finished mill against the emulator (mill-removals.txt, the verb remove of tools/GhidraDump/emulate.py): the remove
// click on the building takes it down and its miller walks home, from where he stands: waiting for grain, grinding or pausing, or on
// the flag with the flour; the grain held on site and the grain on its way are lost with the building. The replay is the removal tests'
// (RemovalReplay.Replay).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class MillRemovalTests
{
    const string Fixture = "mill-removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        EmulatorFixture.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void RemovingAMillSendsItsMillerHomeAndCountsNoGoodAsOnItsWayAsTheOriginalDoes(string name) =>
        RemovalReplay.Replay(Fixture, name, (_, _) => [CommandKind.RemoveBuilding], line => line["sound"] == "4");
}
