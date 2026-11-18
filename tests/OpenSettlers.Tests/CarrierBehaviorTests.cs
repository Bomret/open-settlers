using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The carrier behaviour under the basic set's road capacities, which the application's test project cannot reach; the rest of this
// class is in src/Application.Tests.
[TestClass]
public sealed class CarrierBehaviorTests
{
    static readonly Good Log = Good.Log;
    const int East = 0;
    const int West = 3;

    [TestMethod]
    public void UnderTheBasicSetABusyRoadsCarriersStopAtItsLengthClassInTheSetsTable()
    {
        var basic = new OpenSettlersRuleTables(Path.Combine(RepositoryRoot(), "content", "basic")).Read();
        var capacities = Enumerable.Range(0, RuleTables.RoadCapacityCount).Select(i => basic.RoadCapacity(i)).ToArray();
        foreach (var (steps, carriers) in new[] { (2, (int)basic.RoadCapacity(0)), (6, basic.RoadCapacity(2) - 1) })
        {
            var rig = new CarrierRig(carrier: true, capacities: capacities, steps: steps);
            rig.Bench.Flags[0].Side(East).Carriers = carriers;
            rig.Bench.Flags[1].Side(West).Carriers = carriers;
            for (var place = 0; place < 3; place++)
            {
                rig.Goods.LayGood(0, place, Log, destination: 1, way: East);
            }

            var slot = rig.Bench.Settlers.Add(new Settler(new Jobless(), 0, 0, 0, 0, new WaitsInCastle()));
            rig.Roster.Waiting[new Jobless()] = slot;

            rig.Carriers.FlagPass(0);

            Assert.AreEqual(
                carriers == basic.RoadCapacity(LengthClass.OfSteps(steps).Value) ? 0 : 1,
                rig.Roster.Sent,
                $"a road of {steps} steps with {carriers} carriers"
            );
        }
    }

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "project.godot")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("no project.godot above the test assembly");
    }
}
