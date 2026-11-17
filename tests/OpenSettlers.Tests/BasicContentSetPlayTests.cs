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
// The castle's 20 first settlers hold 5 jobless ones, and each road's carrier and each worker without a tool takes one
// (play/food-chain.play), so a chain that needs more than 5 of them cannot be laid in one game: the bread chain is played in two
// halves that share the mill, farm and mill to flour in the castle's stock, and the castle's grain through mill and baker to bread.
[TestClass]
public sealed class BasicContentSetPlayTests
{
    // The screen form of the seed play/full-loop.play runs: the castle's click at 24,3 gives its flag at 25,4.
    const string LoopSeed = "1234567812345634";

    // The tick caps: far above what each run needs (about 2400 ticks for the construction loop, 4400 for the longest chain),
    // so a slow value is not mistaken for a stalled one and a stalled one ends the run soon.
    const int LoopCap = 8000;
    const int ChainCap = 20000;

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
        bool lake
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
        var json =
            "{\"size\":1,\"seed\":\"1234567812345678\","
            + $"\"height\":[{Rows((_, _) => '1')}],"
            + $"\"terrain\":[{Rows((x, y) => lake && y is >= 14 and <= 20 && x is >= 16 and <= 22 ? 'w' : 'g')}],"
            + $"\"lakes\":[{Rows((x, y) => lake && y is >= 15 and <= 19 && x is >= 17 and <= 21 ? 'L' : '.')}],"
            + "\"castle\":{\"x\":15,\"y\":10},"
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
        out ulong digest
    )
    {
        var game = ChainGame(folder, stockLevel, buildings, roads, lake);
        var start = game.CastleStock.Count(good);
        var held = Until(game, ChainCap, () => game.CastleStock.Count(good) >= start + gain);
        digest = game.Digest();

        return held
            ? null
            : $"{string.Join("+", buildings.Select(b => b.Type))}: {start} of good {good.Code} after {ChainCap} ticks";
    }

    // The castle's flag at 16,11 to the flag of a building at (x, 10) three steps east, and so on east.
    static readonly (int X, int Y, int[] Directions) CastleEast = (16, 11, [0, 0, 0]);
    static readonly (int X, int Y, int[] Directions) NextEast = (19, 11, [0, 0, 0]);

    static string? FarmAndMill(string folder, out ulong digest) =>
        Chain(
            folder,
            stockLevel: 0,
            buildings: [("mill", 18, 10), ("farm", 18, 13)],
            roads: [CastleEast, (19, 14, [5, 5, 5])],
            lake: false,
            good: Good.Flour,
            gain: 2,
            out digest
        );

    static string? MillAndBaker(string folder, out ulong digest) =>
        Chain(
            folder,
            stockLevel: GrainLevel,
            buildings: [("mill", 18, 10), ("baker", 21, 10)],
            roads: [CastleEast, NextEast],
            lake: false,
            good: Good.Bread,
            gain: 2,
            out digest
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
            buildings: [("fisher", 18, 10)],
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
                ?? FarmAndMill(folder, out _)
                ?? MillAndBaker(folder, out _)
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

    [TestMethod]
    public void FarmAndMillGiveFlourToTheCastle()
    {
        Assert.IsNull(FarmAndMill(CheckedIn(), out _));
    }

    [TestMethod]
    public void MillAndBakerGiveBreadFromTheCastlesGrain()
    {
        Assert.IsNull(MillAndBaker(CheckedIn(), out _));
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
            Assert.IsNull(FarmAndMill(CheckedIn(), out var flour));
            Assert.IsNull(MillAndBaker(CheckedIn(), out var bread));
            Assert.IsNull(PigFarmAndButcher(CheckedIn(), out var meat));
            Assert.IsNull(Fisher(CheckedIn(), out var fish));
            digests.AddRange([loop, flour, bread, meat, fish]);
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
