using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

[TestClass]
public sealed class LayoutTests
{
    const string Seed = "1234567812345678";

    // A grid of 32 rows with one row replaced; rows[y] is the replacement text of row y.
    static string Grid(char fill, params (int Row, string Text)[] rows) =>
        "["
        + string.Join(
            ", ",
            Enumerable
                .Range(0, 32)
                .Select(y => $"\"{rows.FirstOrDefault(r => r.Row == y).Text ?? new string(fill, 32)}\"")
        )
        + "]";

    static string Row(char fill, int at, char with) => new string(fill, at) + with + new string(fill, 31 - at);

    internal static string Text(
        string size = "1",
        string? height = null,
        string? terrain = null,
        string? lower = null,
        string? lakes = null,
        string objects = "[]",
        string seed = $"\"{Seed}\"",
        string extra = ""
    ) =>
        $$"""
            // a drawn map
            {
              "size": {{size}},
              "seed": {{seed}},
              "height": {{height ?? Grid('1')}},
              "terrain": {{terrain ?? Grid('g')}},
              {{(lower is null ? "" : $"\"terrainLower\": {lower},")}}
              {{(lakes is null ? "" : $"\"lakes\": {lakes},")}}
              "objects": {{objects}},{{extra}}
            }
            """;

    static Map Built(string text)
    {
        var read = Layout.Read(text, "test.jsonc");
        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));

        return read.Layout!.BuildMap();
    }

    static int At(int x, int y) => y * 32 + x;

    [TestMethod]
    public void ADrawnMapOfGrassAHillALakeAndTreesLoadsIntoTheLayers()
    {
        var map = Built(
            Text(
                height: Grid('1', (4, Row('1', 5, '9')), (10, Row('1', 3, '0'))),
                terrain: Grid('g', (10, Row('g', 3, 'w'))),
                lower: Grid('g', (10, Row('g', 3, 'x'))),
                objects: """[{"x": 7, "y": 2, "code": 8}, {"x": 8, "y": 2, "code": 9}]"""
            )
        );

        Assert.AreEqual(32, map.Size.Width);
        Assert.AreEqual(9, map.At(5, 4).Height);
        Assert.AreEqual(0, map.At(3, 10).Height);
        Assert.AreEqual((new Terrain(5), new Terrain(5)), (map.At(0, 0).Upper, map.At(0, 0).Lower));
        Assert.AreEqual((new Terrain(0), new Terrain(1)), (map.At(3, 10).Upper, map.At(3, 10).Lower));
        CollectionAssert.AreEqual(
            new[] { (7, 2, 8), (8, 2, 9) },
            map.Objects.Select(o => (o.X, o.Y, (int)o.Code)).ToArray()
        );
    }

    [TestMethod]
    public void ALowerTerrainDefaultsToTheUpperTerrain()
    {
        var map = Built(Text(terrain: Grid('g', (0, Row('g', 0, 'm')))));

        Assert.AreEqual((new Terrain(11), new Terrain(11)), (map.At(0, 0).Upper, map.At(0, 0).Lower));
    }

    // The letters of docs/rules/map-form.md, in the order of the terrain codes 0 to 15.
    const string Letters = "wxyzbghijkpmnoqs";

    // A generated 32 by 32 map of the first seed word at or after first that holds water.
    static Map Generated(int first) =>
        Enumerable
            .Range(first, 200)
            .Select(word =>
                MapGenerator.Generate(TestProgram.Made(), new MapSeed((ushort)word, 0xA0D2, 0xB4FA), MapSize.Offered[0])
            )
            .First(map => Enumerable.Range(0, map.Count).Any(tile => map.At(tile).Upper.IsWater));

    // The generated map's heights and terrain, drawn again as a layout and built.
    static Map Redrawn(Map generated, bool withLakes = false)
    {
        string RowOf(int y, System.Func<int, char> letter) =>
            string.Concat(Enumerable.Range(0, 32).Select(x => letter(y * 32 + x)));
        string DrawnGrid(System.Func<int, char> letter) =>
            "[" + string.Join(", ", Enumerable.Range(0, 32).Select(y => $"\"{RowOf(y, letter)}\"")) + "]";

        return Built(
            Text(
                height: DrawnGrid(tile => "0123456789abcdefghijklmnopqrstuv"[generated.At(tile).Height]),
                terrain: DrawnGrid(tile => Letters[generated.At(tile).Upper.Code]),
                lower: DrawnGrid(tile => Letters[generated.At(tile).Lower.Code]),
                lakes: withLakes ? DrawnGrid(tile => generated.At(tile).LakeMark ? 'L' : '.') : null
            )
        );
    }

    [TestMethod]
    public void ADrawnTileCarriesTheLakeFlagTheGeneratorGivesATileOfTheSameHeightAndTerrain()
    {
        // Flags bit 6 also marks a large object (MapGen_ResolveObjectOverlap), which a layout does not derive, so
        // the comparison covers the tiles that hold no object. The drawn rule is a superset of the generator's: a
        // generated map also holds unmarked tiles at height 0 whose six neighbors are at height 0 (the state the
        // generator marks from is not drawn), so the test pins every generated lake tile and leaves those tiles out
        // (docs/rules/map-form.md).
        var lakes = 0;
        var missed = new System.Collections.Generic.List<string>();
        foreach (var first in Enumerable.Range(1, 150))
        {
            var generated = Generated(first);
            var map = Redrawn(generated);
            for (var tile = 0; tile < 1024; tile++)
            {
                if (generated.At(tile).Object.Exists)
                {
                    continue;
                }

                if (generated.At(tile).LakeMark)
                {
                    lakes++;
                    if (!map.At(tile).LakeMark)
                    {
                        missed.Add($"{first}:{tile % 32},{tile / 32}");
                    }
                }
            }
        }

        Assert.IsTrue(lakes > 0, "the generated maps hold lakes");
        Assert.AreEqual(
            0,
            missed.Count,
            "generated lake tiles the drawn map does not mark: " + string.Join(" ", missed)
        );
    }

    [TestMethod]
    public void ALakeGridSetsFlagsBit6OnEveryTileExactlyAsTheGeneratorDoes()
    {
        var marked = 0;
        var wrong = new System.Collections.Generic.List<string>();
        foreach (var first in Enumerable.Range(1, 150))
        {
            var generated = Generated(first);
            var map = Redrawn(generated, withLakes: true);
            for (var tile = 0; tile < 1024; tile++)
            {
                var want = generated.At(tile).LakeMark;
                marked += want ? 1 : 0;
                if (map.At(tile).LakeMark != want)
                {
                    wrong.Add($"{first}:{tile % 32},{tile / 32}");
                }
            }
        }

        Assert.IsTrue(marked > 0, "the generated maps hold marked tiles");
        Assert.AreEqual(0, wrong.Count, "tiles whose Flags bit 6 differs: " + string.Join(" ", wrong));
    }

    [TestMethod]
    public void ALakeGridReplacesTheDerivedRuleAndAnAbsentGridKeepsIt()
    {
        var flat = Grid('0');
        var lakes = Grid('.', (12, Row('.', 3, 'L')));
        var water = Grid('w');

        var drawn = Built(Text(height: flat, terrain: water, lakes: lakes));
        var derived = Built(Text(height: flat, terrain: water));

        Assert.IsTrue(drawn.At(3, 12).LakeMark);
        Assert.IsFalse(drawn.At(12, 12).LakeMark);
        Assert.IsTrue(derived.At(12, 12).LakeMark);
    }

    [TestMethod]
    public void ALakeGridOfTheWrongShapeOrWithAnotherCharacterIsAProblemNamingTheFileAndTheEntry()
    {
        var read = Layout.Read(Text(lakes: Grid('.', (3, "..."), (4, Row('.', 5, 'x')))), "test.jsonc");

        Assert.IsNull(read.Layout);
        CollectionAssert.AreEqual(
            new[]
            {
                "test.jsonc: lakes[3]: has 3 characters, the map is 32 wide",
                "test.jsonc: lakes[4]: column 5: 'x' is not a lake character, . or L",
            },
            read.Problems.ToArray()
        );
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(13)]
    [DataRow(31)]
    public void ALakeGridRowWithANulCharacterIsAProblemNamingTheRowAndColumn(int column)
    {
        // The JSON escape \u0000 reads as one NUL character in the row.
        var row = new string('.', column) + "\\u0000" + new string('.', 31 - column);

        var read = Layout.Read(Text(lakes: Grid('.', (4, row))), "test.jsonc");

        Assert.IsNull(read.Layout);
        CollectionAssert.AreEqual(
            new[] { $"test.jsonc: lakes[4]: column {column}: '\0' is not a lake character, . or L" },
            read.Problems.ToArray()
        );
    }

    [TestMethod]
    public void ADrawnTileCarriesTheWaterMarkTheGeneratorGivesATileOfTheSameTerrain()
    {
        // The generated map's heights and terrain, drawn again: the object layer's bit 7 (MapGen_MarkWaterAdjacent)
        // is the one byte the layout derives from the terrain that the generated map also holds.
        var generated = Generated(1);
        var map = Redrawn(generated);

        var water = Enumerable.Range(0, 1024).Count(tile => generated.At(tile).WaterTriangle);
        Assert.IsTrue(water > 0 && water < 1024, "the generated map holds water and land");
        CollectionAssert.AreEqual(
            Enumerable.Range(0, 1024).Select(tile => generated.At(tile).WaterTriangle).ToArray(),
            Enumerable.Range(0, 1024).Select(tile => map.At(tile).WaterTriangle).ToArray()
        );
    }

    [TestMethod]
    public void AWaterTileKeepsTheWaterMarkBesideItsObjectCode()
    {
        var map = Built(Text(terrain: Grid('g', (2, Row('g', 2, 'z'))), objects: """[{"x": 2, "y": 2, "code": 8}]"""));

        Assert.AreEqual(8, map.At(2, 2).Object.Code);
        Assert.IsTrue(map.At(2, 2).WaterTriangle);
    }

    [TestMethod]
    public void TheMiddleOfADrawnLakeCarriesTheLakeFlagAndItsShoreDoesNot()
    {
        var water = Enumerable
            .Range(0, 32)
            .Select(y =>
                y is >= 10 and <= 14 ? new string('g', 10) + "wwwww" + new string('g', 17) : new string('g', 32)
            );
        var lakeHeights = Enumerable
            .Range(0, 32)
            .Select(y =>
                y is >= 10 and <= 14 ? new string('1', 10) + "00000" + new string('1', 17) : new string('1', 32)
            );
        string Json(System.Collections.Generic.IEnumerable<string> rows) =>
            "[" + string.Join(", ", rows.Select(r => $"\"{r}\"")) + "]";

        var map = Built(Text(height: Json(lakeHeights), terrain: Json(water)));

        Assert.IsTrue(map.At(12, 12).LakeMark);
        Assert.IsFalse(map.At(10, 12).LakeMark);
        Assert.IsFalse(map.At(12, 10).LakeMark);
    }

    static Game Played(string text, int ticks)
    {
        var game = GameFactory.Create(TestProgram.Made(), Built(text));
        for (var i = 0; i < ticks; i++)
        {
            game.Tick();
        }

        return game;
    }

    [TestMethod]
    public void TwoLoadsOfOneLayoutGiveTheSameDigestAfterTheSameTicks()
    {
        var text = Text(objects: """[{"x": 7, "y": 2, "code": 8}]""");

        Assert.AreEqual(Played(text, 25).Digest(), Played(text, 25).Digest());
    }

    [TestMethod]
    public void AnotherSeedGivesAnotherGameStateAndRandomSequence()
    {
        var first = Played(Text(), 5);
        var second = Played(Text(seed: "\"8765432187654321\""), 5);

        Assert.AreNotEqual(first.Digest(), second.Digest());
        Assert.AreNotEqual(first.Draws.Generator, second.Draws.Generator);
    }

    [TestMethod]
    public void TheGamesRandomGeneratorStartsWhereTheMapGeneratorLeavesItForTheSeed()
    {
        OpenSettlers.Simulation.Seed.TryParse(Seed, out var seed, out _);
        var words = seed.ToMapSeed();

        var game = GameFactory.Create(TestProgram.Made(), Built(Text()));

        Assert.AreEqual(new RandomGenerator(words.W0, words.W1, words.W2).Step().Step(), game.Draws.Generator);
    }

    [TestMethod]
    public void AMissingFileIsAProblemNamingTheFile()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "layout-tests-no-such-dir", "none.jsonc");

        var read = Layout.ReadFile(path);

        Assert.IsNull(read.Layout);
        Assert.HasCount(1, read.Problems);
        StringAssert.StartsWith(read.Problems[0], path + ": ");
    }

    [TestMethod]
    public void AnEmptyPathIsOneProblem()
    {
        var read = Layout.ReadFile("");

        Assert.IsNull(read.Layout);
        CollectionAssert.AreEqual(new[] { "'': file: the path is empty" }, read.Problems.ToArray());
    }

    [TestMethod]
    public void APathWithANulIsOneProblemNamingIt()
    {
        // The file system refuses a path with a NUL character (an ArgumentException), not an IOException.
        var read = Layout.ReadFile("a\0b");

        Assert.IsNull(read.Layout);
        Assert.HasCount(1, read.Problems);
        StringAssert.StartsWith(read.Problems[0], "a\0b: file: cannot be read: ");
    }

    [TestMethod]
    public void ADirectoryPathIsOneProblemNamingIt()
    {
        // A directory cannot be read as a file whoever runs the test, root included.
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-tests").FullName;
        try
        {
            var read = Layout.ReadFile(folder);

            Assert.IsNull(read.Layout);
            Assert.HasCount(1, read.Problems);
            StringAssert.StartsWith(read.Problems[0], folder + ": ");
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void AFileOnDiskLoadsAndItsProblemsNameItsPath()
    {
        var folder = System.IO.Directory.CreateTempSubdirectory("layout-tests").FullName;
        try
        {
            var good = System.IO.Path.Combine(folder, "good.jsonc");
            var bad = System.IO.Path.Combine(folder, "bad.jsonc");
            System.IO.File.WriteAllText(good, Text());
            System.IO.File.WriteAllText(bad, Text(size: "9"));

            Assert.IsNotNull(Layout.ReadFile(good).Layout);
            StringAssert.StartsWith(Layout.ReadFile(bad).Problems[0], bad + ": size: ");
        }
        finally
        {
            System.IO.Directory.Delete(folder, true);
        }
    }

    static string Rows31 =>
        "[" + string.Join(", ", Enumerable.Range(0, 31).Select(_ => $"\"{new string('1', 32)}\"")) + "]";

    [TestMethod]
    public void AGoodLayoutWithCommentsAndTrailingCommasLoadsWithNoProblem()
    {
        // Text starts with a comment and ends its last entry with a comma.
        var read = Layout.Read(Text(), "x.jsonc");

        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
    }

    [TestMethod]
    [DataRow("{ \"size\": 1, ", "syntax")]
    [DataRow(
        "{ \"size\": 1, \"seed\": \"1234567812345678\", \"height\": [], \"terrain\": [], \"objects\": [], \"bogus\": 1 }",
        "bogus"
    )]
    [DataRow("{ \"size\": 1, \"seed\": \"1234567812345678\", \"height\": [], \"objects\": [] }", "terrain")]
    [DataRow("{ \"seed\": \"1234567812345678\", \"height\": [], \"terrain\": [], \"objects\": [] }", "size")]
    [DataRow("[1]", "layout")]
    public void ABrokenFileNamesTheFileAndTheEntryAndLoadsNothing(string text, string entry)
    {
        var read = Layout.Read(text, "layouts/broken.jsonc");

        Assert.IsNull(read.Layout);
        Assert.IsTrue(
            read.Problems.Any(p => p.StartsWith("layouts/broken.jsonc: ") && p.Contains(entry)),
            string.Join("; ", read.Problems)
        );
    }

    [TestMethod]
    public void AMissingOptionalEntryIsNoProblem()
    {
        var read = Layout.Read(Text().Replace("\"objects\": [],", ""), "x.jsonc");

        Assert.AreEqual(0, read.Problems.Count, string.Join("; ", read.Problems));
    }

    public static System.Collections.Generic.IEnumerable<object[]> BrokenEntries =>
        [
            ["size is a string", Text(size: "\"big\""), "size"],
            ["size not offered", Text(size: "4"), "size"],
            ["seed is a number", Text(seed: "5"), "seed"],
            ["seed is no seed", Text(seed: "\"abc\""), "seed"],
            ["height row too short", Text(height: Grid('1', (3, "111"))), "height[3]"],
            ["height row too long", Text(height: Grid('1', (7, new string('1', 33)))), "height[7]"],
            ["height has 31 rows", Text(height: Rows31), "height"],
            ["height outside 0-v", Text(height: Grid('1', (1, Row('1', 4, 'w')))), "height[1]"],
            ["height is no list", Text(height: "\"1\""), "height"],
            ["terrain letter unknown", Text(terrain: Grid('g', (2, Row('g', 9, 'Z')))), "terrain[2]"],
            ["terrain has 31 rows", Text(terrain: Rows31), "terrain"],
            ["lower row too short", Text(lower: Grid('g', (5, "g"))), "terrainLower[5]"],
            ["lower letter unknown", Text(lower: Grid('g', (6, Row('g', 0, '?')))), "terrainLower[6]"],
            ["object outside the map", Text(objects: """[{"x": 32, "y": 0, "code": 8}]"""), "objects[0]"],
            [
                "object below the map",
                Text(objects: """[{"x": 0, "y": 0, "code": 8}, {"x": 1, "y": -1, "code": 8}]"""),
                "objects[1]",
            ],
            ["object code 0", Text(objects: """[{"x": 0, "y": 0, "code": 0}]"""), "objects[0]"],
            ["object code 128", Text(objects: """[{"x": 0, "y": 0, "code": 128}]"""), "objects[0]"],
            ["object has an unknown key", Text(objects: """[{"x": 0, "y": 0, "code": 8, "z": 1}]"""), "objects[0]"],
            ["object lacks its code", Text(objects: """[{"x": 0, "y": 0}]"""), "objects[0]"],
            ["object is no object", Text(objects: "[3]"), "objects[0]"],
            [
                "two objects on one tile",
                Text(objects: """[{"x": 1, "y": 1, "code": 8}, {"x": 1, "y": 1, "code": 9}]"""),
                "objects[1]",
            ],
            ["a key twice", Text(extra: "").Replace("\"size\": 1,", "\"size\": 1, \"size\": 1,"), "size"],
        ];

    [TestMethod]
    [DynamicData(nameof(BrokenEntries))]
    public void AnEntryTheMapCannotDrawIsAProblemNamingTheEntry(string name, string text, string entry)
    {
        var read = Layout.Read(text, "layouts/broken.jsonc");

        Assert.IsNull(read.Layout, name);
        Assert.IsTrue(
            read.Problems.Any(p =>
                p.StartsWith("layouts/broken.jsonc: " + entry) || p.StartsWith("layouts/broken.jsonc: " + entry + ":")
            ),
            name + ": " + string.Join("; ", read.Problems)
        );
    }
}
