using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The work-area overlay's tint marks (CONTEXT.md, "Work-area overlay"): one mark on every node in the work area of at least one of
// the player's gatherer buildings or building sites, on a flat meadow of the smallest size with the castle at 10,10. The ranges
// named here are the original's (JobSearchTests pins them); the view reads them from the job search alone (INV-0017).
[TestClass]
public sealed class WorkAreaDrawsTests
{
    // The five gatherers and a sawmill around the castle.
    const string AllFive = """
        {"type": "farm", "x": 7, "y": 6},
        {"type": "woodcutter", "x": 12, "y": 6},
        {"type": "fisher", "x": 14, "y": 10},
        {"type": "forester", "x": 14, "y": 13},
        {"type": "stonecutter", "x": 7, "y": 14},
        {"type": "sawmill", "x": 12, "y": 14}
        """;

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

    // A layout of a flat meadow of size 1 with the castle at 10,10 and the finished buildings given.
    static Layout Meadow(string buildings)
    {
        var rows = string.Join(",", Enumerable.Repeat("\"" + new string('1', 32) + "\"", 32));
        var terrain = string.Join(",", Enumerable.Repeat("\"" + new string('g', 32) + "\"", 32));
        var text = $$"""
            {
              "size": 1,
              "seed": "1234567812345678",
              "height": [{{rows}}],
              "terrain": [{{terrain}}],
              "objects": [],
              "castle": {"x": 10, "y": 10, "stock": {"scythe": 1, "fishing-rod": 1} },
              "buildings": [{{buildings}}]
            }
            """;
        var read = Layout.Read(text, "meadow.jsonc");

        return read.Layout ?? throw new AssertFailedException(string.Join("; ", read.Problems));
    }

    static Game Applied(string buildings, RuleTables? tables = null)
    {
        var layout = Meadow(buildings);
        var game = GameFactory.Create(tables ?? BasicTables(), layout.BuildMap());
        Assert.IsNull(layout.Apply(game));

        return game;
    }

    // The nodes of spiral entries first to last around (x, y) on the 32 by 32 map, wrapped.
    static IEnumerable<(int, int)> Entries(int x, int y, int first, int last) =>
        Enumerable
            .Range(first, last - first + 1)
            .Select(entry => ((x + Spiral.Offsets[entry].Dx) & 31, (y + Spiral.Offsets[entry].Dy) & 31));

    static HashSet<(int, int)> Marked(Game game) =>
        [.. WorkAreaDraws.Of(game, 0).Select(draw => (draw.Column, draw.Row))];

    [TestMethod]
    public void The_marks_are_the_union_of_the_five_gatherers_work_areas()
    {
        var game = Applied(AllFive);

        HashSet<(int, int)> expected =
        [
            .. Entries(7, 6, 7, 38),
            .. Entries(12, 6, 1, 128),
            .. Entries(14, 10, 1, 64),
            .. Entries(14, 13, 1, 128),
            .. Entries(7, 14, 1, 128),
        ];

        CollectionAssert.AreEquivalent(expected.ToList(), Marked(game).ToList());
    }

    [TestMethod]
    public void The_farm_marks_entries_7_to_38_and_leaves_1_to_6_unmarked()
    {
        var game = Applied("""{"type": "farm", "x": 7, "y": 6}""");

        var marked = Marked(game);

        CollectionAssert.AreEquivalent(Entries(7, 6, 7, 38).ToList(), marked.ToList());
        Assert.IsFalse(Entries(7, 6, 1, 6).Any(marked.Contains));
    }

    [TestMethod]
    public void Overlapping_areas_give_one_mark_per_shared_node()
    {
        var game = Applied(AllFive);

        var draws = WorkAreaDraws.Of(game, 0);

        Assert.AreEqual(draws.Count, draws.Select(draw => (draw.Column, draw.Row)).Distinct().Count());
    }

    [TestMethod]
    public void A_hut_near_the_map_s_edge_marks_the_wrapped_nodes_on_the_far_side()
    {
        var game = Applied("""{"type": "woodcutter", "x": 5, "y": 10}""");

        var marked = Marked(game);

        CollectionAssert.AreEquivalent(Entries(5, 10, 1, 128).ToList(), marked.ToList());
        Assert.IsTrue(marked.Any(node => node.Item1 >= 30), "a node across the west edge");
    }

    [TestMethod]
    public void Each_mark_stands_at_its_node_raised_by_the_node_s_height()
    {
        var game = Applied("""{"type": "woodcutter", "x": 12, "y": 6}""");

        foreach (var draw in WorkAreaDraws.Of(game, 0))
        {
            var (x, y) = Camera.NodePosition(draw.Column, draw.Row);
            Assert.AreEqual(x, draw.X);
            Assert.AreEqual(y - game.Map.At(draw.Column, draw.Row).Height * TerrainTriangles.HeightStep, draw.Y);
        }
    }

    [TestMethod]
    public void A_gatherer_s_building_site_is_marked_like_the_finished_building()
    {
        var game = Applied("");
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.PlaceBuilding,
                X = 12,
                Y = 6,
                Building = BuildingType.Woodcutter,
            }
        );
        game.Tick();

        Assert.IsTrue(
            game.Buildings.Any(building => building.Type == BuildingType.Woodcutter && building.Constructing)
        );
        CollectionAssert.AreEquivalent(Entries(12, 6, 1, 128).ToList(), Marked(game).ToList());
    }

    [TestMethod]
    public void A_burning_gatherer_building_the_castle_and_a_sawmill_show_no_area()
    {
        var game = Applied("""{"type": "woodcutter", "x": 12, "y": 6}, {"type": "sawmill", "x": 12, "y": 14}""");
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = CommandKind.RemoveBuilding,
                X = 12,
                Y = 6,
            }
        );
        game.Tick();

        Assert.IsTrue(
            game.Buildings.Any(building => building.Type == BuildingType.Woodcutter && building.IsDemolishing)
        );
        Assert.AreEqual(0, WorkAreaDraws.Of(game, 0).Count);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void With_the_History_Edition_s_tables_the_same_layout_gives_the_same_marks()
    {
        var basic = Applied(AllFive);
        var original = Applied(AllFive, TestProgram.Tables());

        CollectionAssert.AreEquivalent(Marked(basic).ToList(), Marked(original).ToList());
    }

    [TestMethod]
    public void Building_the_marks_leaves_the_game_s_digest_as_it_was()
    {
        var game = Applied(AllFive);
        var digest = game.Digest();

        WorkAreaDraws.Of(game, 0);

        Assert.AreEqual(digest, game.Digest());
    }

    [TestMethod]
    public void No_file_of_the_map_view_or_the_game_view_names_a_spiral_entry()
    {
        var root = RepositoryRoot();
        var named = new[] { "src/MapView", "src/Viewer" }
            .SelectMany(folder =>
                Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories)
            )
            .Where(file => File.ReadAllText(file).Contains("Spiral"))
            .ToList();

        CollectionAssert.AreEqual(new List<string>(), named);
    }
}
