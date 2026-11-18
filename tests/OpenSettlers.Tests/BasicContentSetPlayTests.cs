using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Domain;
using OpenSettlers.Import.ContentSet;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The game's loops played headless on the checked-in rule files of the basic content set (content/basic/rules/), through the
// rule tables port, with no install and no Godot, in the shape of GoldenTests: the construction loop on a generated map by
// commands (the script of play/full-loop.play), and each production chain on a flat layout built here, finished buildings,
// roads, and their carriers laid by the layout seam, with a lake whose nodes hold fish (a hand-drawn lake holds none). A value
// that stalls a loop (a zero step time, road capacity, or blow step, an empty distribution order) fails the run, either
// because the reader refuses it or because the loop does not finish within its tick cap (INV-0001: the values stay invented;
// the tests pin that they play). INV-0002: the same files, seed, and commands give the same digest on every run.
//
// The bread chain is one game: the farm's grain goes through the mill and the baker to the castle, from a castle that starts with
// no grain, flour, or bread. It needs 6 jobless settlers, because each of its three roads' carriers and each of the farmer, the
// miller, and the baker takes one, and the castle's 20 first settlers hold 5. Its layout's castle therefore names its settlers,
// 6 jobless ones, which replace the 20 first settlers (docs/rules/layouts.md); the other chains keep the 20 first settlers.
[TestClass]
public sealed class BasicContentSetPlayTests
{
    // The screen form of the seed play/full-loop.play runs: the castle's click at 24,3 gives its flag at 25,4.
    const string LoopSeed = "1234567812345634";

    // The tick caps: far above what each run needs (about 2400 ticks for the construction loop, 14300 for the longest chain,
    // the bread chain), so a slow value is not mistaken for a stalled one and a stalled one ends the run soon.
    const int LoopCap = 8000;
    const int ChainCap = 30000;

    // The fish a lake node holds: a stand-in count, enough for every cast of a run.
    const int LakeDetail = 40;

    // The start stock level that gives the castle two grain, no flour, one bread, and one meat (goods.jsonc, startStock).
    const int GrainLevel = 20;

    static string RepositoryRoot()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "OpenSettlers.csproj")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName
            ?? throw new InvalidOperationException("OpenSettlers.csproj not found above the test output");
    }

    static string CheckedIn() => Path.Combine(RepositoryRoot(), "content", "basic");

    static void Do(Game game, int kind, int x, int y, int[]? directions = null, BuildingType? building = null)
    {
        game.Enqueue(
            new Command
            {
                Tick = game.TickCount,
                Player = 0,
                Kind = kind,
                X = x,
                Y = y,
                Directions = directions,
                Building = building,
            }
        );
        game.Tick();
    }

    // Ticks until the condition holds or the cap is reached, and tells whether it held.
    static bool Until(Game game, int cap, Func<bool> condition)
    {
        for (var tick = 0; tick < cap && !condition(); tick++)
        {
            game.Tick();
        }

        return condition();
    }

    // The construction loop of play/full-loop.play. Returns the failure, or null with the game's digest in digest.
    static string? ConstructionLoop(string folder, out ulong digest)
    {
        digest = 0;
        var tables = new OpenSettlersRuleTables(folder).Read();
        Assert.IsTrue(Seed.TryParse(LoopSeed, out var seed, out var problem), problem);
        var map = MapGenerator.Generate(tables, seed.ToMapSeed(), MapSize.Offered[0]);
        var game = GameFactory.Create(tables, map, everyPassTurns: true, handsOutGoods: true);

        // The castle holds 5 planks and 4 stones (the first start stock row's 12 and 6 less its own site's 7 and 2): as
        // many planks as the sawmill (3 planks, 2 stones) and the woodcutter's hut (2 planks) need. The sawmill's site at
        // 25,1 has its flag at 26,2, the woodcutter's at 26,4 its flag at 27,5.
        Do(game, CommandKind.PlaceCastle, 24, 3);
        Do(game, CommandKind.PlaceBuilding, 25, 1, building: BuildingType.Sawmill);
        Do(game, CommandKind.PlaceBuilding, 26, 4, building: BuildingType.Woodcutter);
        Do(game, CommandKind.BuildRoad, 25, 4, [5, 0, 5]);
        Do(game, CommandKind.BuildRoad, 25, 4, [1, 0]);
        if (game.Buildings.Count != 3)
        {
            return $"the two sites stand with the castle: {game.Buildings.Count} buildings";
        }

        var sawmill = game.Buildings[1];
        var level = sawmill.Site!.TargetHeight;
        if (game.Map.At(25, 1).Height == level)
        {
            return "the sawmill's ground is level before its digger comes: the loop has no digging";
        }

        // The castle hands its materials out over the roads: it holds no plank afterwards.
        if (!Until(game, LoopCap, () => game.CastleStock.Planks == 0))
        {
            return "the castle's planks never left it for the sites";
        }

        // A third site only the sawmill's planks can pay for, a stonecutter's at 23,4 with its flag at 24,5.
        Do(game, CommandKind.PlaceBuilding, 23, 4, building: BuildingType.Stonecutter);
        Do(game, CommandKind.BuildRoad, 25, 4, [2, 3]);
        var stonecutter = game.Buildings[3];
        var planksFromCastle = false;
        if (
            !Until(
                game,
                LoopCap,
                () =>
                {
                    planksFromCastle |= game.CastleStock.Planks > 0;

                    return !game.Buildings.Any(building => building.Constructing);
                }
            )
        )
        {
            return "a site is still unfinished: "
                + string.Join(", ", game.Buildings.Where(b => b.Constructing).Select(b => b.Type.ToString()));
        }

        if (planksFromCastle || stonecutter.Constructing)
        {
            return "the stonecutter's planks did not come from the sawmill";
        }

        if (game.Map.At(25, 1).Height != level)
        {
            return "the digger did not level the sawmill's ground";
        }

        digest = game.Digest();

        return null;
    }

    // A flat layout (castle at 15,10, flag at 16,11) with finished buildings and the roads that join their flags, one
    // carrier to a road, and a lake south of the castle whose nodes hold fish. Returns the game with the layout applied.
    static Game ChainGame(
        string folder,
        int stockLevel,
        (string Type, int X, int Y)[] buildings,
        (int X, int Y, int[] Directions)[] roads,
        bool lake,
        int? jobless = null
    )
    {
        static string Rows(Func<int, int, char> at) =>
            string.Join(
                ",",
                Enumerable
                    .Range(0, 32)
                    .Select(y => $"\"{new string([.. Enumerable.Range(0, 32).Select(x => at(x, y))])}\"")
            );

        var flags = roads
            .SelectMany(road => new[] { (road.X, road.Y), Far(road.X, road.Y, road.Directions) })
            .Where(flag => flag != (16, 11))
            .Distinct();
        // A castle that names its settlers holds the castle settler and those alone, in place of the 20 first settlers.
        var settlers = jobless is { } count ? $",\"settlers\":{{\"jobless\":{count}}}" : "";
        var json =
            "{\"size\":1,\"seed\":\"1234567812345678\","
            + $"\"height\":[{Rows((_, _) => '1')}],"
            + $"\"terrain\":[{Rows((x, y) => lake && y is >= 14 and <= 20 && x is >= 16 and <= 22 ? 'w' : 'g')}],"
            + $"\"lakes\":[{Rows((x, y) => lake && y is >= 15 and <= 19 && x is >= 17 and <= 21 ? 'L' : '.')}],"
            + $"\"castle\":{{\"x\":15,\"y\":10{settlers}}},"
            + $"\"flags\":[{string.Join(",", flags.Select(f => $"{{\"x\":{f.X},\"y\":{f.Y}}}"))}],"
            + $"\"roads\":[{string.Join(",", roads.Select(r => $"{{\"x\":{r.X},\"y\":{r.Y},\"directions\":[{string.Join(",", r.Directions)}]}}"))}],"
            + $"\"settlers\":[{string.Join(",", roads.Select(r => $"{{\"job\":\"carrier\",\"state\":\"waiting-on-road\",\"x\":{r.X},\"y\":{r.Y},\"direction\":{r.Directions[0]}}}"))}],"
            + $"\"buildings\":[{string.Join(",", buildings.Select(b => $"{{\"type\":\"{b.Type}\",\"x\":{b.X},\"y\":{b.Y}}}"))}]}}";
        var read = Layout.Read(json, "basic-chain");
        Assert.IsNotNull(read.Layout, string.Join("; ", read.Problems));
        var map = read.Layout.BuildMap();
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (map.At(x, y).LakeMark)
                {
                    map.At(x, y).LakeDetail = LakeDetail;
                }
            }
        }

        var game = GameFactory.Create(
            new OpenSettlersRuleTables(folder).Read(),
            map,
            stockLevel,
            everyPassTurns: true,
            handsOutGoods: true
        );
        Assert.IsNull(read.Layout.Apply(game));

        return game;
    }

    // The flag a road ends at: the steps of the directions from its start.
    static (int X, int Y) Far(int x, int y, int[] directions) =>
        directions.Aggregate((x, y), (at, d) => (at.x + Direction.Steps[d].Dx, at.y + Direction.Steps[d].Dy));

    // Plays a chain until the castle holds at least gain more of the good than it started with. Returns the failure or null.
    static string? Chain(
        string folder,
        int stockLevel,
        (string Type, int X, int Y)[] buildings,
        (int X, int Y, int[] Directions)[] roads,
        bool lake,
        Good good,
        int gain,
        out ulong digest,
        int? jobless = null
    )
    {
        var game = ChainGame(folder, stockLevel, buildings, roads, lake, jobless);
        var start = game.CastleStock.Count(good);
        var held = Until(game, ChainCap, () => game.CastleStock.Count(good) >= start + gain);
        digest = game.Digest();

        return held
            ? null
            : $"{string.Join("+", buildings.Select(b => b.Type))}: {start} of good {good.Name} after {ChainCap} ticks";
    }

    // The castle's flag at 16,11 to the flag of a building at (x, 10) three steps east, and so on east.
    static readonly (int X, int Y, int[] Directions) CastleEast = (16, 11, [0, 0, 0]);
    static readonly (int X, int Y, int[] Directions) NextEast = (19, 11, [0, 0, 0]);

    // The farm's flag at 19,14 three steps north to the mill's flag at 19,11.
    static readonly (int X, int Y, int[] Directions) FarmNorth = (19, 14, [5, 5, 5]);

    static readonly (string Type, int X, int Y)[] BreadBuildings = [("farm", 18, 13), ("mill", 18, 10), ("baker", 21, 10)];

    static readonly (int X, int Y, int[] Directions)[] BreadRoads = [CastleEast, NextEast, FarmNorth];

    static string? Bread(string folder, out ulong digest, string? missing = null) =>
        Chain(
            folder,
            stockLevel: 0,
            buildings: [.. BreadBuildings.Where(building => building.Type != missing)],
            roads: BreadRoads,
            lake: false,
            good: Good.Bread,
            gain: 2,
            out digest,
            jobless: 6
        );

    static string? PigFarmAndButcher(string folder, out ulong digest) =>
        Chain(
            folder,
            stockLevel: GrainLevel,
            buildings: [("pig-farm", 18, 10), ("butcher", 21, 10)],
            roads: [CastleEast, NextEast],
            lake: false,
            good: Good.Meat,
            gain: 2,
            out digest
        );

    static string? Fisher(string folder, out ulong digest) =>
        Chain(
            folder,
            stockLevel: 0,
            buildings: [("fisher-hut", 18, 10)],
            roads: [CastleEast],
            lake: true,
            good: Good.Fish,
            gain: 2,
            out digest
        );

    // Every loop in turn; the first failure, or null. A file the reader refuses is a failure, with its message.
    static string? Failure(string folder)
    {
        try
        {
            return ConstructionLoop(folder, out _)
                ?? Bread(folder, out _)
                ?? PigFarmAndButcher(folder, out _)
                ?? Fisher(folder, out _);
        }
        catch (InvalidDataException e)
        {
            return $"refused: {e.Message}";
        }
    }

    // A copy of the checked-in rule files in a temporary folder, which a test edits and plays.
    sealed class RuleFolder : IDisposable
    {
        public RuleFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "open-settlers-play-" + Guid.NewGuid().ToString("N"));
            foreach (var file in OpenSettlersRuleTables.Files)
            {
                var target = Path.Combine(Root, file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Path.Combine(CheckedIn(), file), target);
            }
        }

        public string Root { get; }

        public void Replace(string file, string find, string replacement)
        {
            var path = Path.Combine(Root, file);
            var text = File.ReadAllText(path);
            Assert.Contains(find, text, $"the fixture edit {find} is not in {file}");
            File.WriteAllText(path, text.Replace(find, replacement));
        }

        public void ReplacePattern(string file, string pattern, string replacement)
        {
            var path = Path.Combine(Root, file);
            var text = File.ReadAllText(path);
            var edited = Regex.Replace(text, pattern, replacement, RegexOptions.Singleline);
            Assert.AreNotEqual(text, edited, $"the pattern {pattern} is not in {file}");
            File.WriteAllText(path, edited);
        }

        public void Dispose() => Directory.Delete(Root, true);
    }

    // A layout's good is given by its stable name: under the basic set, whose catalog holds no original codes, the carrier lines
    // the log up with the log's rank and the good reads its request class.
    [TestMethod]
    public void ALayoutGoodOnTheBasicSetReadsItsTransportPriorityAndRequestClass()
    {
        var read = Layout.Read(
            LayoutTests.Text(
                extra: """

                "castle": {"x": 10, "y": 10},
                "flags": [{"x": 14, "y": 11}],
                "goods": [{"x": 14, "y": 11, "place": 0, "good": "log"}]
                """
            ),
            "good.jsonc"
        );
        Assert.IsNotNull(read.Layout, string.Join("; ", read.Problems));
        var tables = new OpenSettlersRuleTables(CheckedIn()).Read();
        var game = GameFactory.Create(tables, read.Layout.BuildMap(), 0);
        Assert.IsNull(read.Layout.Apply(game));

        var placed = game.Flags[game.Flags.SlotAt(14, 11)].Places[0].Good;

        Assert.AreEqual(tables.TransportPriority(Good.Log), tables.TransportPriority(placed));
        Assert.AreNotEqual(0, tables.TransportPriority(placed), "the log is lined up");
        Assert.IsNotNull(tables.ClassOfGood(placed), "the log has a request class");
    }

    [TestMethod]
    public void OneLayoutWithGoodsByStableNameOpensUnderBothContentSets()
    {
        var read = Layout.Read(
            LayoutTests.Text(
                extra: """

                "castle": {"x": 10, "y": 10},
                "flags": [{"x": 14, "y": 11}],
                "goods": [{"x": 14, "y": 11, "place": 0, "good": "shield"}, {"x": 14, "y": 11, "place": 1, "good": "plank"}]
                """
            ),
            "both.jsonc"
        );
        Assert.IsNotNull(read.Layout, string.Join("; ", read.Problems));

        foreach (var tables in new[] { new OpenSettlersRuleTables(CheckedIn()).Read(), TestProgram.Made() })
        {
            var game = GameFactory.Create(tables, read.Layout.BuildMap(), 0);

            Assert.IsNull(read.Layout.Apply(game));
            var places = game.Flags[game.Flags.SlotAt(14, 11)].Places;
            Assert.AreEqual((new Good { Name = "shield" }, Good.Plank), (places[0].Good, places[1].Good));
        }
    }

    [TestMethod]
    public void TheConstructionLoopPlaysOnTheCheckedInFilesUntilASiteIsPaidWithTheSawmillsPlanks()
    {
        Assert.IsNull(ConstructionLoop(CheckedIn(), out _));
    }

    // The cheapest food building (the fisher's hut: 2 planks, no stone) placed as a site by command on the loop's map, with the
    // castle's stock paying its needs and the builder's blows finishing it.
    [TestMethod]
    public void AFoodBuildingBuiltByCommandIsFinishedFromTheBasicStock()
    {
        var tables = new OpenSettlersRuleTables(CheckedIn()).Read();
        Assert.IsTrue(Seed.TryParse(LoopSeed, out var seed, out var problem), problem);
        var game = GameFactory.Create(
            tables,
            MapGenerator.Generate(tables, seed.ToMapSeed(), MapSize.Offered[0]),
            everyPassTurns: true,
            handsOutGoods: true
        );

        Do(game, CommandKind.PlaceCastle, 24, 3);
        Do(game, CommandKind.PlaceBuilding, 26, 4, building: BuildingType.Fisher);
        Do(game, CommandKind.BuildRoad, 25, 4, [1, 0]);
        var fisher = game.Buildings[1];

        Assert.AreEqual(BuildingType.Fisher, fisher.Type);
        Assert.IsTrue(fisher.Constructing, "the site starts unfinished");
        Assert.IsTrue(Until(game, LoopCap, () => !fisher.Constructing), "the fisher's site never finished");
    }

    // The settler supply at rate 40, a period of 125 passes, on the loop's map: the castle's first settlers take every sword and
    // shield of the start stock, so the knight the second try owes is not made and stays owed, and both new settlers are without a job.
    [TestMethod]
    public void TheSettlerSupplyMakesSettlersWithoutAJobAndNoKnightOnTheBasicSet()
    {
        var tables = new OpenSettlersRuleTables(CheckedIn()).Read();
        Assert.IsTrue(Seed.TryParse(LoopSeed, out var seed, out var problem), problem);
        var game = GameFactory.Create(
            tables,
            MapGenerator.Generate(tables, seed.ToMapSeed(), MapSize.Offered[0]),
            settlerSupply: true,
            settlerRate: SettlerSupply.HighestRate
        );
        Do(game, CommandKind.PlaceCastle, 24, 3);
        var count = game.Settlers.Count;

        for (var pass = 0; pass < 2 * 125 + 1; pass++)
        {
            game.Tick();
        }

        CollectionAssert.AreEqual(
            new Job[] { new Jobless(), new Jobless() },
            game.Settlers.Skip(count).Select(settler => settler.Job).ToArray()
        );
        Assert.AreEqual(1, game.Supply!.KnightsOwed, "the knight the second try owes stays owed");
        Assert.AreEqual(0, game.CastleStock.Count(Good.Sword), "the castle stock holds no sword");
        Assert.AreEqual(0, game.CastleStock.Count(Good.Shield), "the castle stock holds no shield");
    }

    [TestMethod]
    public void FarmMillAndBakerGiveBreadFromTheFarmsGrainInOneGame()
    {
        var start = ChainGame(CheckedIn(), 0, BreadBuildings, BreadRoads, lake: false, jobless: 6).CastleStock;
        Assert.AreEqual(
            (0, 0, 0),
            (start.Count(Good.Grain), start.Count(Good.Flour), start.Count(Good.Bread)),
            "the castle starts with no grain, flour, or bread"
        );

        Assert.IsNull(Bread(CheckedIn(), out _));
    }

    [TestMethod]
    [DataRow("farm", "mill+baker")]
    [DataRow("mill", "farm+baker")]
    [DataRow("baker", "farm+mill")]
    public void TheBreadChainWithALinkMissingGivesNoBread(string missing, string left)
    {
        Assert.AreEqual($"{left}: 0 of good bread after {ChainCap} ticks", Bread(CheckedIn(), out _, missing));
    }

    // INV-0001: a castle whose layout names no settlers holds the recovered 20 first settlers, the road's carrier among them.
    [TestMethod]
    public void AChainGameWhoseCastleNamesNoSettlersHoldsTheTwentyFirstSettlers()
    {
        var game = ChainGame(CheckedIn(), 0, [("fisher-hut", 18, 10)], [CastleEast], lake: true);

        Assert.AreEqual(20, game.Settlers.Count);
    }

    [TestMethod]
    public void PigFarmAndButcherGiveMeatToTheCastle()
    {
        Assert.IsNull(PigFarmAndButcher(CheckedIn(), out _));
    }

    [TestMethod]
    public void TheFisherGivesFishToTheCastle()
    {
        Assert.IsNull(Fisher(CheckedIn(), out _));
    }

    [TestMethod]
    public void EveryLoopGivesTheSameDigestOnEveryRun()
    {
        var first = new List<ulong>();
        var second = new List<ulong>();
        foreach (var digests in new[] { first, second })
        {
            Assert.IsNull(ConstructionLoop(CheckedIn(), out var loop));
            Assert.IsNull(Bread(CheckedIn(), out var bread));
            Assert.IsNull(PigFarmAndButcher(CheckedIn(), out var meat));
            Assert.IsNull(Fisher(CheckedIn(), out var fish));
            digests.AddRange([loop, bread, meat, fish]);
        }

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void TheLoopsPlayOnAnUntouchedCopyOfTheFiles()
    {
        using var folder = new RuleFolder();

        Assert.IsNull(Failure(folder.Root));
    }

    [TestMethod]
    public void AZeroStepTimeFailsTheLoops()
    {
        using var folder = new RuleFolder();
        folder.Replace(OpenSettlersRuleTables.WorldFile, "\"0\": 64,", "\"0\": 0,");

        Assert.StartsWith("refused: rules/world.jsonc: stepTimes.0: ", Failure(folder.Root));
    }

    [TestMethod]
    public void AZeroRoadCapacityFailsTheLoops()
    {
        using var folder = new RuleFolder();
        folder.Replace(OpenSettlersRuleTables.WorldFile, "    1, // A short road", "    0, // A short road");

        Assert.StartsWith("refused: rules/world.jsonc: roadCapacities[0]: ", Failure(folder.Root));
    }

    [TestMethod]
    public void AZeroBlowStepFailsTheLoops()
    {
        using var folder = new RuleFolder();
        folder.ReplacePattern(OpenSettlersRuleTables.BuildingsFile, "(\"sawmill\".*?\"firstHalf\": )20", "${1}0");

        Assert.StartsWith("refused: rules/buildings.jsonc: buildings.sawmill.blows.firstHalf: ", Failure(folder.Root));
    }

    [TestMethod]
    public void AnEmptyDistributionOrderFailsTheLoops()
    {
        using var folder = new RuleFolder();
        folder.ReplacePattern(
            OpenSettlersRuleTables.GoodsFile,
            "\"distribution\": \\[.*?\\n  \\],",
            "\"distribution\": [[], [], []],"
        );

        Assert.AreEqual("the castle's planks never left it for the sites", Failure(folder.Root));
    }
}
