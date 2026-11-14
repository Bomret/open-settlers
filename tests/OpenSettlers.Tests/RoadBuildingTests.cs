using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of road-builds.txt are the output of the emulator verb road: (tools/GhidraDump/emulate.py), without the
// leading "EMU ". The verb places a castle on a generated map, puts flags on listed tiles with the flag button
// (road-flag lines), and then draws roads with the original's road code (ADR 0008): the road button's check, and for
// each step the cursor update (0x44b6ce, the allowed directions) and the click on the next node (0x443f62 to 0x44477f
// of 0x440c24). The roads run one after the other on the same map. A line with a step names the direction, the mask
// of allowed directions the cursor update left, the sound the step asked for (8 a step, 4 a refusal, 2 the finished
// road), and the map bytes the step changed. The line of a road's outcome names the bytes the finished road changed
// in the map and in the flag table's items.
[TestClass]
public sealed class RoadBuildingTests
{
    const string Fixture = "road-builds.txt";

    public static IEnumerable<object[]> Runs =>
        File.ReadAllLines(Fixture).Select(Run).Distinct().Select(run => new object[] { run });

    [TestMethod]
    [DynamicData(nameof(Runs))]
    [TestCategory(Needs.Install)]
    public void EachStepIsAllowedOrRefusedAndTheRoadIsWrittenAsTheEmulatorsRoadDrawingDoes(string run)
    {
        var lines = File.ReadAllLines(Fixture).Where(line => Run(line) == run).ToArray();
        var header = Fields(lines[0]);
        MapSize.TryFromValue(int.Parse(header["size"], CultureInfo.InvariantCulture), out var size, out _);
        var map = MapGenerator.Generate(TestProgram.Tables(), ParseSeed(header["seed"]), size!);
        var castle = header["castle"].Split(',').Select(int.Parse).ToArray();
        var game = GameFactory.Create(
            TestProgram.Tables(),
            map,
            int.Parse(header["level"], CultureInfo.InvariantCulture)
        );
        game.Enqueue(
            new Command
            {
                Tick = 0,
                Player = 0,
                Kind = CommandKind.PlaceCastle,
                X = castle[0],
                Y = castle[1],
            }
        );
        game.ApplyCommands();

        foreach (var line in lines.Where(line => line.StartsWith("road-flag ", StringComparison.Ordinal)))
        {
            var tile = Fields(line)["tile"].Split(',').Select(int.Parse).ToArray();
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.PlaceFlag,
                    X = tile[0],
                    Y = tile[1],
                }
            );
            game.ApplyCommands();
        }

        foreach (var attempt in Attempts(lines))
        {
            var first = Fields(attempt.First());
            var start = first["start"].Split(',').Select(int.Parse).ToArray();
            var steps = attempt.Where(line => line.Contains(" step=", StringComparison.Ordinal)).ToArray();
            // The original ends the drawing at the first flag it reaches or the first refusal: later directions are not run.
            var directions = first["dirs"].Select(c => c - '0').Take(steps.Length).ToArray();
            var label = $"{run} start {first["start"]} dirs {first["dirs"]}";
            var outcome = attempt.Last();
            var before = Snapshot(map);
            var roads = game.Network.Roads.Count;

            if (outcome.Contains(" START-REFUSED ", StringComparison.Ordinal))
            {
                Assert.AreEqual(
                    RoadStatus.Refused,
                    game.RoadPlanner.Check(start[0], start[1], first["dirs"].Select(c => c - '0').ToArray()),
                    label
                );
                continue;
            }

            var corrupted = outcome.Contains(" outcome=CORRUPTED ", StringComparison.Ordinal);
            foreach (var step in steps)
            {
                var fields = Fields(step);
                var index = int.Parse(fields["step"], CultureInfo.InvariantCulture);
                var expected = fields["sounds"] switch
                {
                    "8" => RoadStatus.Open,
                    "2" => RoadStatus.Complete,
                    "4" => RoadStatus.Refused,
                    var other => throw new InvalidDataException(
                        $"{label}: the sound {other} is not one the harness checks"
                    ),
                };
                if (corrupted && index == steps.Length - 1)
                {
                    // The step the original lets through and corrupts the old road with: the engine refuses it.
                    expected = RoadStatus.Refused;
                }

                var digest = game.Digest();
                Assert.AreEqual(
                    expected,
                    game.RoadPlanner.Check(start[0], start[1], directions[..(index + 1)]),
                    $"{label} step {index}"
                );
                Assert.AreEqual(digest, game.Digest(), $"{label} step {index} checks without changing the game");
            }

            var built = outcome.Contains(" outcome=BUILT ", StringComparison.Ordinal);
            game.Enqueue(
                new Command
                {
                    Tick = game.TickCount,
                    Player = 0,
                    Kind = CommandKind.BuildRoad,
                    X = start[0],
                    Y = start[1],
                    Directions = directions,
                }
            );
            game.ApplyCommands();

            Assert.AreEqual(built ? roads + 1 : roads, game.Network.Roads.Count, label);
            // No state behind where the road is not built: the changes the harness lists are empty.
            Assert.AreEqual(
                Between(outcome, " roadchanges=", " items="),
                Changes(before, Snapshot(map), map.Size.Width),
                label
            );
            if (built)
            {
                AssertItems(label, game.Network.Roads[^1], outcome);
            }
        }
    }

    [TestMethod]
    public void TheEmulatorValuesHoldRoadsOfEveryOutcomeAndBuiltRoadsAcrossTheWrappingEdge()
    {
        var lines = File.ReadAllLines(Fixture);
        var outcomes = lines.Where(line => line.Contains(" outcome=", StringComparison.Ordinal)).ToArray();
        foreach (var kind in new[] { "BUILT", "CANCELLED", "STEP-REFUSED", "REFUSED-AT-END" })
        {
            Assert.IsTrue(
                outcomes.Any(line => line.Contains(" outcome=" + kind + " ", StringComparison.Ordinal)),
                kind
            );
        }

        Assert.IsTrue(lines.Any(line => line.Contains(" START-REFUSED ", StringComparison.Ordinal)));
        Assert.IsTrue(
            outcomes.Where(line => line.Contains(" outcome=BUILT ", StringComparison.Ordinal)).Any(CrossesTheWrap)
        );
    }

    // Whether a built road's changed nodes lie on both sides of the map's edge: two of them are further apart than
    // half the map in columns or in rows.
    static bool CrossesTheWrap(string line)
    {
        var width = int.Parse(Fields(line)["size"], CultureInfo.InvariantCulture) == 1 ? 32 : 64;
        var nodes = Between(line, " roadchanges=", " items=")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(':')[0].Split(',').Select(int.Parse).ToArray())
            .ToArray();

        return nodes.Any(a => nodes.Any(b => Math.Abs(a[0] - b[0]) > width / 2 || Math.Abs(a[1] - b[1]) > 16));
    }

    // The flag table's items the road changed: the bit of each end's direction in the item's road bits (+3), the same
    // bit in +4 for a land road (a water road leaves it clear), and in the byte of the end's direction (+0x3c +
    // direction) the direction of the other end in bits 3 to 5.
    static void AssertItems(string label, Road road, string outcome)
    {
        var items = outcome[(outcome.IndexOf(" items=", StringComparison.Ordinal) + " items=".Length)..]
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(change => change.Split(':', '=', '>'))
            .Select(parts =>
                (
                    Item: int.Parse(parts[0], CultureInfo.InvariantCulture),
                    Offset: Convert.ToInt32(parts[1], 16),
                    Old: Convert.ToInt32(parts[2], 16),
                    New: Convert.ToInt32(parts[3], 16)
                )
            )
            .ToArray();

        var ends = new[]
        {
            (Slot: road.FromSlot, Direction: road.FromDirection.Value, Other: road.ToDirection.Value),
            (Slot: road.ToSlot, Direction: road.ToDirection.Value, Other: road.FromDirection.Value),
        };
        foreach (var (slot, direction, otherDirection) in ends)
        {
            // A road from a flag back to itself sets two bits of one item.
            var bits = ends.Where(end => end.Slot == slot).Sum(end => 1 << end.Direction);
            Assert.IsTrue(
                items.Any(i => i.Item == slot && i.Offset == 3 && (i.Old ^ i.New) == bits),
                $"{label}: road bits of item {slot}"
            );
            Assert.AreEqual(
                !road.Water,
                items.Any(i => i.Item == slot && i.Offset == 4 && (i.Old ^ i.New) == bits),
                $"{label}: land bits of item {slot}"
            );
            // The byte stays 0 where the other end's direction is 0, so the harness lists no change for it.
            Assert.AreEqual(
                otherDirection,
                items
                    .Where(i => i.Item == slot && i.Offset == 0x3c + direction)
                    .Select(i => i.New >> 3 & 7)
                    .SingleOrDefault(),
                $"{label}: the other end's direction in item {slot}"
            );
        }
    }

    // The map, castle, and stock level a line belongs to: its fields after the line's own name.
    static string Run(string line) => string.Join(' ', line.Split(' ').Skip(1).Take(4));

    // The road lines in groups: the step lines of one road and the line that ends it, its outcome or a refused start.
    static List<List<string>> Attempts(IEnumerable<string> lines)
    {
        var attempts = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in lines.Where(line => line.StartsWith("road ", StringComparison.Ordinal)))
        {
            current.Add(line);
            if (
                line.Contains(" outcome=", StringComparison.Ordinal)
                || line.Contains(" START-REFUSED ", StringComparison.Ordinal)
            )
            {
                attempts.Add(current);
                current = [];
            }
        }

        return attempts;
    }

    static string Between(string line, string from, string to)
    {
        var begin = line.IndexOf(from, StringComparison.Ordinal) + from.Length;

        return line[begin..line.IndexOf(to, begin, StringComparison.Ordinal)];
    }

    static Dictionary<string, string> Fields(string line) =>
        line.Split(' ')
            .Select(word => word.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0], pair => pair[1]);

    static byte[][] Snapshot(Map map) =>
        [
            .. new[] { MapLayer.Flags, MapLayer.Height, MapLayer.Terrain, MapLayer.Object, MapLayer.Resource }.Select(
                layer => Layers.Of(map, layer)
            ),
        ];

    // The changed bytes as the harness lists them: each changed tile in row-major order with "position=old>new".
    static string Changes(byte[][] before, byte[][] after, int width) =>
        string.Join(
            ' ',
            Enumerable
                .Range(0, before[0].Length)
                .Select(node =>
                    (
                        Node: node,
                        Diff: string.Join(
                            '/',
                            Enumerable
                                .Range(0, before.Length)
                                .Where(i => before[i][node] != after[i][node])
                                .Select(i => $"{i}={before[i][node]:X2}>{after[i][node]:X2}")
                        )
                    )
                )
                .Where(entry => entry.Diff.Length > 0)
                .Select(entry => $"{entry.Node % width},{entry.Node / width}:{entry.Diff}")
        );

    // The cursor update (0x44b6ce) refuses a step onto an object of class 2 or 4 of the class table (0x41e640) and lets
    // a tree (class 1) through. The steps go forward onto nodes that hold no road bits, so the class check decides.
    const byte TreeCode = 0x08;
    const byte StoneCode = 0x48;
    const byte SmallBuildingCode = 0x02;

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void ARoadDrawnForwardAcrossATreeNodeCompletesAtAFlag()
    {
        var map = OwnedLandWithFlags();
        map.At(11, 10).Object = new MapObject { Code = TreeCode };

        var (status, _) = RoadBuilding.Walk(TestProgram.Tables(), map, 10, 10, [0, 0], keep: false);

        Assert.AreEqual(RoadStatus.Complete, status);
    }

    // Codes 3 and 4 are classes 5 and 6: the class check refuses classes 2 and 4 alone.
    [TestMethod]
    [DataRow((byte)3)]
    [DataRow((byte)4)]
    [TestCategory(Needs.Install)]
    public void ARoadDrawnForwardAcrossAClassFiveOrSixNodeCompletesAtAFlag(byte code)
    {
        var map = OwnedLandWithFlags();
        map.At(11, 10).Object = new MapObject { Code = code };

        var (status, _) = RoadBuilding.Walk(TestProgram.Tables(), map, 10, 10, [0, 0], keep: false);

        Assert.AreEqual(RoadStatus.Complete, status);
    }

    [TestMethod]
    [DataRow(StoneCode)]
    [DataRow(SmallBuildingCode)]
    [TestCategory(Needs.Install)]
    public void ARoadStepOntoAStoneOrASmallBuildingIsRefused(byte code)
    {
        var map = OwnedLandWithFlags();
        map.At(11, 10).Object = new MapObject { Code = code };

        var (status, _) = RoadBuilding.Walk(TestProgram.Tables(), map, 10, 10, [0, 0], keep: false);

        Assert.AreEqual(RoadStatus.Refused, status);
    }

    [TestMethod]
    [TestCategory(Needs.Install)]
    public void AFlagMayNotBePlacedOnATreeNode()
    {
        var map = OwnedLandWithFlags();
        map.At(11, 12).Object = new MapObject { Code = TreeCode };

        Assert.AreEqual(0, BuildSite.Evaluate(TestProgram.Tables(), map, 11, 12, 0, hasCastle: true).Site);
    }

    // A map whose every node is player 0's and land, with flags (object code 1) at 10,10 and 12,10.
    static Map OwnedLandWithFlags()
    {
        var map = new Map(MapSize.Offered[0]);
        for (var index = 0; index < map.Count; index++)
        {
            map.At(index).Owner = Owner.Player(0);
            map.At(index).Upper = Terrain.Lowland;
            map.At(index).Lower = Terrain.Lowland;
        }

        map.At(10, 10).Object = MapObject.Flag;
        map.At(12, 10).Object = MapObject.Flag;

        return map;
    }

    static MapSeed ParseSeed(string text)
    {
        var words = text.Split(',').Select(word => ushort.Parse(word, NumberStyles.HexNumber)).ToArray();

        return new MapSeed
        {
            W0 = words[0],
            W1 = words[1],
            W2 = words[2],
        };
    }
}
