using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal of a finished butcher's shop against the emulator (butcher-removals.txt, the verb remove of tools/GhidraDump/emulate.py): the
// remove click on the building takes it down and its butcher walks home, from where he stands: waiting for a pig, at work, or on the flag
// with the meat; the pig held on site and the pig on its way are lost with the building. The replay is the removal tests'
// (RemovalTests.Replay).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class ButcherRemovalTests
{
    const string Fixture = "butcher-removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        RemovalTests.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void RemovingAButchersShopSendsItsButcherHomeAndCountsNoGoodAsOnItsWayAsTheOriginalDoes(string name) =>
        RemovalTests.Replay(Fixture, name, (_, _) => [CommandKind.RemoveBuilding], line => line["sound"] == "4");
}
