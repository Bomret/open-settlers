using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The reach markers of the work-area overlay (CONTEXT.md, "Reach marker") while a gatherer's build button is pressed, on the flat
// meadow of WorkAreaDrawsTests with the castle at 10,10: a disc where the building fits and its worker's target test accepts a node
// of the work area it would have there, a ring where it fits and accepts none, nothing where it does not fit.
[TestClass]
public sealed class ReachMarkerDrawsTests
{
    // A stone heap at 15,12: the stonecutter accepts the node 16,13, south-east of it.
    const string Heap = """{"x": 15, "y": 12, "code": 72}""";

    // Whether a building of the type fits the node, as Build help's marker shows it: a large building needs the large marker, a
    // small one the small or the large marker.
    static bool Fits(Game game, int x, int y, BuildingType type)
    {
        var marker = BuildMarkers.Of(BuildSite.Evaluate(game.Tables, game.Map, x, y, 0, true, game.Buildings));

        return game.Tables.SizeOf(type) == BuildingSize.Large
            ? marker == BuildMarker.Large
            : marker is BuildMarker.Small or BuildMarker.Large;
    }

    // The marker every node should show for the type: true for a disc, false for a ring, absent for none. The work area is the
    // spiral entries first to last around the node, and a node of it is a target where the test accepts it.
    static Dictionary<(int, int), bool> Expected(
        Game game,
        BuildingType type,
        int first,
        int last,
        System.Func<int, int, bool> target
    )
    {
        var expected = new Dictionary<(int, int), bool>();
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (!Fits(game, x, y, type))
                {
                    continue;
                }

                expected[(x, y)] = Enumerable
                    .Range(first, last - first + 1)
                    .Any(entry => target((x + Spiral.Offsets[entry].Dx) & 31, (y + Spiral.Offsets[entry].Dy) & 31));
            }
        }

        return expected;
    }

    static Dictionary<(int, int), bool> Shown(Game game, BuildingType type) =>
        ReachMarkerDraws.Of(game, 0, type).ToDictionary(draw => (draw.Column, draw.Row), draw => draw.InReach);

    static void AssertShowsBoth(Dictionary<(int, int), bool> shown)
    {
        Assert.IsTrue(shown.Values.Any(inReach => inReach), "a disc");
        Assert.IsTrue(shown.Values.Any(inReach => !inReach), "a ring");
    }

    [TestMethod]
    public void The_stonecutter_shows_a_disc_where_a_heap_lies_north_west_of_a_node_in_reach_and_a_ring_elsewhere()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);

        var shown = Shown(game, BuildingType.Stonecutter);

        CollectionAssert.AreEquivalent(
            Expected(game, BuildingType.Stonecutter, 1, 128, (x, y) => (x, y) == (16, 13)).ToList(),
            shown.ToList()
        );
        AssertShowsBoth(shown);
    }

    [TestMethod]
    public void The_woodcutter_shows_a_disc_where_a_tree_stands_in_reach()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: """{"x": 16, "y": 13, "code": 8}""");

        var shown = Shown(game, BuildingType.Woodcutter);

        CollectionAssert.AreEquivalent(
            Expected(game, BuildingType.Woodcutter, 1, 128, (x, y) => (x, y) == (16, 13)).ToList(),
            shown.ToList()
        );
        AssertShowsBoth(shown);
    }

    [TestMethod]
    public void The_fisher_shows_a_disc_where_a_shore_lies_within_64_entries()
    {
        var game = WorkAreaDrawsTests.Applied("");
        // The lower triangle of 17,13 is water: the node is an east shore.
        game.Map.At(17, 13).Lower = new Terrain { Code = 0 };
        var search = game.SearchOf(BuildingType.Fisher)!;

        var shown = Shown(game, BuildingType.Fisher);

        CollectionAssert.AreEquivalent(
            Expected(game, BuildingType.Fisher, 1, 64, (x, y) => search.Accepts(game.Map, x, y)).ToList(),
            shown.ToList()
        );
        AssertShowsBoth(shown);
    }

    [TestMethod]
    [DataRow(9, 1, 128)]
    [DataRow(0xc, 7, 38)]
    public void The_forester_and_the_farmer_show_their_target_test_over_their_entries(int code, int first, int last)
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);
        var type = new BuildingType { Code = code };
        var search = game.SearchOf(type)!;

        var shown = Shown(game, type);

        Assert.IsTrue(shown.Count > 0);
        CollectionAssert.AreEquivalent(
            Expected(game, type, first, last, (x, y) => search.Accepts(game.Map, x, y)).ToList(),
            shown.ToList()
        );
    }

    // The same on the History Edition tables, where the farm is a large building.
    [TestMethod]
    [TestCategory(Needs.Install)]
    [DataRow(9, 1, 128)]
    [DataRow(0xc, 7, 38)]
    public void With_the_History_Edition_tables_the_forester_and_the_farmer_show_their_target_test_over_their_entries(
        int code,
        int first,
        int last
    )
    {
        var game = WorkAreaDrawsTests.Applied("", TestProgram.Tables(), Heap);
        var type = new BuildingType { Code = code };
        var search = game.SearchOf(type)!;

        var shown = Shown(game, type);

        Assert.IsTrue(shown.Count > 0);
        CollectionAssert.AreEquivalent(
            Expected(game, type, first, last, (x, y) => search.Accepts(game.Map, x, y)).ToList(),
            shown.ToList()
        );
    }

    [TestMethod]
    public void A_sawmill_shows_no_reach_marker()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);

        Assert.AreEqual(0, ReachMarkerDraws.Of(game, 0, BuildingType.Sawmill).Count);
    }

    [TestMethod]
    public void Removing_the_only_heap_in_reach_turns_the_disc_into_a_ring()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);
        var discs = Shown(game, BuildingType.Stonecutter)
            .Where(entry => entry.Value)
            .Select(entry => entry.Key)
            .ToList();

        game.Map.At(15, 12).Object = MapObject.None;
        var after = Shown(game, BuildingType.Stonecutter);

        Assert.IsTrue(discs.Count > 0);
        Assert.IsTrue(discs.All(node => !after[node]));
    }

    [TestMethod]
    public void Before_the_castle_stands_no_gatherer_fits_and_no_marker_shows()
    {
        var layout = WorkAreaDrawsTests.Meadow("", Heap);
        var game = GameFactory.Create(WorkAreaDrawsTests.BasicTables(), layout.BuildMap());

        Assert.AreEqual(0, ReachMarkerDraws.Of(game, 0, BuildingType.Stonecutter).Count);
    }

    [TestMethod]
    public void Each_marker_stands_at_its_node_raised_by_the_node_s_height()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);

        foreach (var draw in ReachMarkerDraws.Of(game, 0, BuildingType.Stonecutter))
        {
            var (x, y) = Camera.NodePosition(draw.Column, draw.Row);
            Assert.AreEqual(x, draw.X);
            Assert.AreEqual(y - game.Map.At(draw.Column, draw.Row).Height * TerrainTriangles.HeightStep, draw.Y);
        }
    }

    [TestMethod]
    public void Building_the_markers_leaves_the_game_s_digest_as_it_was()
    {
        var game = WorkAreaDrawsTests.Applied("", objects: Heap);
        var digest = game.Digest();

        foreach (
            var type in new[]
            {
                BuildingType.Woodcutter,
                BuildingType.Stonecutter,
                BuildingType.Forester,
                BuildingType.Fisher,
                BuildingType.Farm,
            }
        )
        {
            ReachMarkerDraws.Of(game, 0, type);
        }

        Assert.AreEqual(digest, game.Digest());
    }
}
