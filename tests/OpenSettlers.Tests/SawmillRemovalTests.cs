using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The removal of a finished sawmill against the emulator (saw-removals.txt, the verb remove of tools/GhidraDump/emulate.py): the remove
// click on the building takes it down and its worker walks home, from where he stands: waiting for a log, sawing, or on the flag with the
// plank. The replay is the removal tests' (RemovalTests.Replay).
[TestClass]
[TestCategory(Needs.Install)]
public sealed class SawmillRemovalTests
{
    private const string Fixture = "saw-removals.txt";

    public static IEnumerable<object[]> Scenarios =>
        RemovalTests.Load(Fixture).Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void RemovingASawmillSendsItsWorkerHomeAsTheOriginalDoes(string name) =>
        RemovalTests.Replay(Fixture, name, (_, _) => [CommandKind.RemoveBuilding], line => line["sound"] == "4");
}
