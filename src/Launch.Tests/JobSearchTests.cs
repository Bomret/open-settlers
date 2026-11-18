using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The job search each gatherer's rules declare (INV-0017): its range of spiral entries, pinned against the draw mask of its loop, and
// its target test, on a hand-made map of lowland. The game answers it for a building type through the worker the rule tables name.
[TestClass]
public sealed class JobSearchTests
{
    const int X = 20;
    const int Y = 20;

    static readonly Terrain Water = new() { Code = 0 };

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "project.godot")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("no project.godot above the test assembly");
    }

    static RuleTables BasicTables() =>
        new OpenSettlersRuleTables(Path.Combine(RepositoryRoot(), "content", "basic")).Read();

    // A map of the smallest size whose every triangle is lowland, with no object and no road.
    static Map Meadow()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        return map;
    }

    static JobSearch SearchOf(BuildingType type) =>
        GameFactory.Create(BasicTables(), Meadow()).SearchOf(type)
        ?? throw new AssertFailedException($"no job search for {type}");

    [TestMethod]
    [DataRow("woodcutter-hut", 1, 128, 0x1fc)]
    [DataRow("stonecutter-hut", 1, 128, 0x1fc)]
    [DataRow("forester-hut", 1, 128, 0x1fc)]
    [DataRow("fisher-hut", 1, 64, 0xfc)]
    [DataRow("farm", 7, 38, 0x7c)]
    public void A_gatherer_s_search_covers_the_entries_its_draw_mask_reaches(string name, int first, int last, int mask)
    {
        var search = SearchOf(new BuildingType { Name = name });

        Assert.AreEqual(first, search.First);
        Assert.AreEqual(last, search.Last);
        Assert.AreEqual(mask, search.Mask);
        Assert.AreEqual(first, search.Entry(0));
        Assert.AreEqual(last, search.Entry(0xffff));
        var entries = Enumerable
            .Range(0, 0x10000)
            .Select(draw => search.Entry((ushort)draw))
            .Distinct()
            .Order()
            .ToArray();
        CollectionAssert.AreEqual(Enumerable.Range(first, last - first + 1).ToArray(), entries);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-4)]
    [DataRow(100)]
    public void A_search_refuses_a_count_that_is_no_power_of_two(int count)
    {
        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() =>
            SearchOf(BuildingType.Woodcutter) with
            {
                Count = count,
            }
        );
    }

    [TestMethod]
    public void Buildings_whose_worker_searches_nothing_and_the_castle_answer_no_search()
    {
        var game = GameFactory.Create(BasicTables(), Meadow());

        AnswerNone(game);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void With_the_History_Edition_tables_the_same_buildings_answer_no_search()
    {
        var game = GameFactory.Create(TestProgram.Tables(), Meadow());

        AnswerNone(game);
        Assert.IsNotNull(game.SearchOf(BuildingType.Woodcutter));
    }

    static void AnswerNone(Game game)
    {
        foreach (
            var type in new[]
            {
                BuildingType.Sawmill,
                BuildingType.PigFarm,
                BuildingType.Butcher,
                BuildingType.Mill,
                BuildingType.Baker,
                BuildingType.Castle,
            }
        )
        {
            Assert.IsNull(game.SearchOf(type), $"{type}");
        }
    }

    [TestMethod]
    public void The_woodcutter_s_test_accepts_a_tree()
    {
        var map = Meadow();
        map.At(X, Y).Object = new MapObject { Code = MapObject.FirstTreeCode };
        var search = SearchOf(BuildingType.Woodcutter);

        Assert.IsTrue(search.Accepts(map, X, Y));
        Assert.IsFalse(search.Accepts(map, X + 1, Y));
    }

    [TestMethod]
    public void The_stonecutter_s_test_accepts_a_node_with_a_heap_north_west_and_no_lake_mark()
    {
        var map = Meadow();
        map.At(X - 1, Y - 1).Object = new MapObject { Code = MapObject.FirstHeapCode };
        var search = SearchOf(BuildingType.Stonecutter);

        Assert.IsTrue(search.Accepts(map, X, Y));
        Assert.IsFalse(search.Accepts(map, X + 1, Y), "no heap north-west");
        map.At(X, Y).LakeMark = true;
        Assert.IsFalse(search.Accepts(map, X, Y), "lake-marked");
    }

    [TestMethod]
    public void The_stonecutter_s_test_finds_the_heap_across_the_map_s_edge()
    {
        var map = Meadow();
        map.At(31, 31).Object = new MapObject { Code = MapObject.FirstHeapCode };

        Assert.IsTrue(SearchOf(BuildingType.Stonecutter).Accepts(map, 0, 0));
    }

    [TestMethod]
    public void The_forester_s_test_accepts_free_meadow()
    {
        var map = Meadow();
        var search = SearchOf(BuildingType.Forester);

        Assert.IsTrue(search.Accepts(map, X, Y));
        map.At(X, Y).Roads = RoadSet.Of(0);
        Assert.IsFalse(search.Accepts(map, X, Y), "road");
        map.At(X + 2, Y).Object = new MapObject { Code = MapObject.FirstTreeCode };
        Assert.IsFalse(search.Accepts(map, X + 2, Y), "object");
        map.At(X + 4, Y).Upper = new Terrain { Code = 6 };
        Assert.IsFalse(search.Accepts(map, X + 4, Y), "terrain");
    }

    [TestMethod]
    public void The_fisher_s_test_accepts_a_free_node_at_a_shore()
    {
        var map = Meadow();
        // The east shore: the node's lower triangle is water, the upper one of its north-west neighbour is not.
        map.At(X, Y).Lower = Water;
        // The west shore: the lower triangle of the west neighbour is water, the upper one of the north neighbour is not.
        map.At(X + 9, Y).Lower = Water;
        var search = SearchOf(BuildingType.Fisher);

        Assert.IsTrue(search.Accepts(map, X, Y), "east shore");
        Assert.IsTrue(search.Accepts(map, X + 10, Y), "west shore");
        Assert.IsFalse(search.Accepts(map, X + 5, Y), "no shore");
        map.At(X, Y).Object = new MapObject { Code = MapObject.FirstTreeCode };
        Assert.IsFalse(search.Accepts(map, X, Y), "object on the shore");
    }

    [TestMethod]
    public void The_farmer_s_test_accepts_a_harvestable_field_or_a_sowable_node()
    {
        var map = Meadow();
        map.At(X, Y).Object = new MapObject { Code = MapObject.GrownFieldCode };
        map.At(X + 6, Y).Object = new MapObject { Code = 3 };
        var search = SearchOf(BuildingType.Farm);

        Assert.IsTrue(search.Accepts(map, X, Y), "harvestable field");
        Assert.IsTrue(search.Accepts(map, X + 3, Y), "sowable node");
        Assert.IsFalse(search.Accepts(map, X + 5, Y), "beside a large building");
    }

    [TestMethod]
    public void Evaluating_a_target_test_draws_nothing_and_writes_nothing()
    {
        var game = GameFactory.Create(BasicTables(), Meadow());
        var digest = game.Digest();
        var generator = game.RandomState;

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
            var search = game.SearchOf(type)!;
            for (var index = 0; index < game.Map.Count; index++)
            {
                search.Accepts(game.Map, index % 32, index / 32);
            }
        }

        Assert.AreEqual(digest, game.Digest());
        Assert.AreEqual(generator, game.RandomState);
    }
}
