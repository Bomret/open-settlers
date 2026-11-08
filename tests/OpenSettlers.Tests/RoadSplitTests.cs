using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The split runs of road-splits.txt are the emulator's output for the flag button (0x442244) pressed on a node of a road
// (docs/rules/roads.md#a-flag-on-a-road): the flag placement that calls the split of the road, with the carrier in each
// state, a good, the west road, and no free settler. They are the verb remove of RemovalTests, whose replay checks the map
// bytes, the settlers, and the flag items after the click and after each pass.
[TestClass]
[TestCategory(Needs.Install)]
public sealed class RoadSplitTests
{
    private const string Fixture = "road-splits.txt";

    // The scenarios whose clicks are flag buttons: the building placements (b@x,y) are the build site's slice.
    public static IEnumerable<object[]> Scenarios =>
        RemovalTests
            .Load(Fixture)
            .Where(scenario =>
                scenario.Name.StartsWith("split-", System.StringComparison.Ordinal)
                && scenario.Command.Split(':')[0] == "remove"
            )
            .Select(scenario => new object[] { scenario.Name });

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void AFlagOnARoadSplitsItAsTheOriginalDoes(string name) =>
        RemovalTests.Replay(Fixture, name, (_, _) => [CommandKind.PlaceFlag], line => !line.ContainsKey("m"));

    // The building button's click (0x449a63) on a node whose flag node holds a road splits the road under the hut's flag; the
    // click's map bytes and the flags' items are the original's. The passes after it differ (the hut stands finished at once).
    [TestMethod]
    public void AHutWhoseFlagLandsOnARoadSplitsItAsTheOriginalDoes() =>
        RemovalTests.Replay(
            Fixture,
            "building-state6",
            (click, _) => [click == 'b' ? CommandKind.PlaceBuilding : CommandKind.PlaceFlag],
            line => false,
            clickOnly: true
        );
}
