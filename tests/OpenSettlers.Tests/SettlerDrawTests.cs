using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenSettlers.Assets;
using OpenSettlers.MapView;
using OpenSettlers.Simulation;

namespace OpenSettlers.Tests;

// The lines of settler-draws.txt are the output of the emulator verb draw (tools/GhidraDump/emulate.py, EmulateDraw.java,
// ADR 0008): the settler draw path of the original's main loop run after the passes of a game, once for each pass of its
// windows. Each scenario starts with a "#" line that names its verb command; the line "install=" names the install the
// run came from (the History Edition, ADR 0004: the verb refuses any other build of Siedler1.exe); a "pass=" line gives
// the clock, the 16 bytes of each settler whose bytes changed since the line before and the height byte of its node
// (s<slot>=bytes/height; the bytes are those the view pass left, after the turn it gives each settler on a shown tile), the
// timer and stamp each drawn settler held before that turn (t<slot>=), and what the pass drew (d=): one record for each
// settler the draw path drew, in the order it drew them, as <slot>:<entry>/<colour>/<x>,<y>;... for the sprites it blitted
// (colour -1 for a sprite blitted as it is).
// A position is the map pixel of the original's draw (the node, lifted by its height, plus the sequence's offsets) plus the
// 0x10 and 8 that 0x44ffe7 adds to a record's position. The engine's Draws, given the settlers' bytes, the map's heights, the
// clock, and the headers of the figures, must return the same sprites in the same order.
[TestClass]
public sealed class SettlerDrawTests
{
    private const string Fixture = "settler-draws.txt";
    private const string ReferenceSha256 = "0113d8b31268a5848e6827da32a970d7c6a0aab33ff28cc8f11a974b3679dbd5";

    // 0x44ffe7 moves a record's position by 0x10 and 8 before the blit.
    private const int OffsetX = 0x10;
    private const int OffsetY = 8;

    private sealed record Sprite(int Entry, int Colour, int X, int Y)
    {
        public override string ToString() => $"{Entry}/{Colour}/{X},{Y}";
    }

    private sealed record Record(int Slot, List<Sprite> Sprites);

    private sealed record Pass(
        int Number,
        ushort Clock,
        Dictionary<int, (byte[] Bytes, int Height)> Changed,
        Dictionary<int, (ushort Timer, ushort Stamp)> Held,
        List<Record> Records
    );

    private sealed record Scenario(string Name, string Command, List<string> Header, List<Pass> Passes);

    public static IEnumerable<object[]> Scenarios => Load().Select(scenario => new object[] { scenario.Name });

    private static List<Scenario> Load()
    {
        var scenarios = new List<Scenario>();
        foreach (var line in File.ReadAllLines(Fixture))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(new Scenario(parts[0], parts[1], [], []));
            }
            else if (!line.StartsWith("pass=", StringComparison.Ordinal))
            {
                scenarios[^1].Header.Add(line);
            }
            else
            {
                scenarios[^1].Passes.Add(ParsePass(line));
            }
        }

        return scenarios;
    }

    private static Pass ParsePass(string line)
    {
        var fields = line.Split(' ')
            .Select(field => field.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        var changed = new Dictionary<int, (byte[], int)>();
        foreach (var (key, value) in fields.Where(pair => pair.Key[0] == 's'))
        {
            var halves = value.Split('/');
            changed[int.Parse(key[1..], CultureInfo.InvariantCulture)] = (
                Convert.FromHexString(halves[0]),
                Convert.ToInt32(halves[1], 16)
            );
        }

        var held = new Dictionary<int, (ushort, ushort)>();
        foreach (var (key, value) in fields.Where(pair => pair.Key[0] == 't'))
        {
            var bytes = Convert.FromHexString(value);
            held[int.Parse(key[1..], CultureInfo.InvariantCulture)] = (
                BitConverter.ToUInt16(bytes, 0),
                BitConverter.ToUInt16(bytes, 2)
            );
        }

        var records = fields["d"]
            .Split('|')
            .Select(text =>
            {
                var halves = text.Split(':');
                return new Record(
                    int.Parse(halves[0], CultureInfo.InvariantCulture),
                    [
                        .. halves[1]
                            .Split(';')
                            .Select(sprite =>
                            {
                                var numbers = sprite.Split('/');
                                var position = numbers[2].Split(',');
                                return new Sprite(
                                    int.Parse(numbers[0], CultureInfo.InvariantCulture),
                                    int.Parse(numbers[1], CultureInfo.InvariantCulture),
                                    int.Parse(position[0], CultureInfo.InvariantCulture),
                                    int.Parse(position[1], CultureInfo.InvariantCulture)
                                );
                            }),
                    ]
                );
            })
            .ToList();

        return new Pass(
            int.Parse(fields["pass"], CultureInfo.InvariantCulture),
            ushort.Parse(fields["clock"], CultureInfo.InvariantCulture),
            changed,
            held,
            records
        );
    }

    // The settler of the original's 16 bytes (Table16), as in CarrierWalkTests: the node is the dword at 4, (y * 2 * width + x) * 4.
    private static Settler FromBytes(byte[] bytes, int width)
    {
        var node = BitConverter.ToInt32(bytes, 4) / 4;
        var state = bytes[10];

        // The job is the bits 2 to 6 of byte 0 (0x43f537 masks with 0x7c); the byte's bit 7 is not the job's.
        return new Settler(
            (bytes[0] >> 2) & 0x1f,
            bytes[0] & 3,
            node % (2 * width),
            node / (2 * width),
            BitConverter.ToUInt16(bytes, 8),
            state,
            0
        )
        {
            Frame = bytes[1],
            Timer = BitConverter.ToUInt16(bytes, 2),
            Mode = bytes[11],
            Target =
                state == 0x42
                    ? (BitConverter.ToInt32(bytes, 12) - 0x23000000) / 0x46
                    : BitConverter.ToUInt16(bytes, 12),
            Heading = (sbyte)bytes[14],
            Next = bytes[15],
        };
    }

    private static string Describe(IEnumerable<Sprite> sprites) => string.Join(" ", sprites);

    [TestMethod]
    public void EveryRecordingNamesTheReferenceInstallItCameFrom()
    {
        foreach (var scenario in Load())
        {
            var install = scenario.Header.Single(line => line.StartsWith("install=", StringComparison.Ordinal));
            StringAssert.Contains(install, "History Edition", scenario.Name);
            StringAssert.Contains(install, $"sha256={ReferenceSha256}", scenario.Name);
        }
    }

    [TestMethod]
    [DynamicData(nameof(Scenarios))]
    public void TheEnginesDrawsAreTheOriginalsPassForPass(string name)
    {
        var scenario = Load().Single(candidate => candidate.Name == name);
        var install = TestProgram.OpenInstall();
        if (
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(install.ReadAll(Install.Program)))
            != ReferenceSha256.ToUpperInvariant()
        )
        {
            Assert.Inconclusive(
                "The recordings come from the History Edition's Siedler1.exe (ADR 0004); this install holds another build."
            );
        }

        var graphics = SettlerGraphics.Load(install, TestProgram.Tables(), Settler.CarrierJob, Settler.WoodcutterJob);
        // draw:WINDOWS:CX,CY:VERB:N:SEED:...
        var parts = scenario.Command.Split(':');
        MapSize.TryFromValue(int.Parse(parts[4], CultureInfo.InvariantCulture), out var size, out _);
        var seed = parts[5].Split(',').Select(word => Convert.ToUInt16(word, 16)).ToArray();
        var map = MapGenerator.Generate(TestProgram.Tables(), new MapSeed(seed[0], seed[1], seed[2]), size!);
        var width = map.Size.Width;
        var heights = map.Bytes(MapLayer.Height);

        var known = new SortedDictionary<int, byte[]>();
        foreach (var pass in scenario.Passes)
        {
            foreach (var (slot, (bytes, height)) in pass.Changed)
            {
                known[slot] = bytes;
                var settler = FromBytes(bytes, width);
                heights[map.Index(settler.X, settler.Y)] = (byte)height;
            }

            // The settlers as the original's view pass left them, which it drew.
            Check($"{name}: pass {pass.Number}", known);

            // The settlers as they stood before that pass gave them a turn, where the turn only ran the timer down by the
            // clock units since the stamp and set the stamp to the clock: the engine, whose settlers are not given a turn
            // for the view, takes the time left from the clock.
            var before = new SortedDictionary<int, byte[]>(known);
            foreach (var (slot, (timer, stamp)) in pass.Held)
            {
                var ranDown = (ushort)(timer - (ushort)(pass.Clock - stamp));
                if (
                    BitConverter.ToUInt16(known[slot], 2) == ranDown
                    && BitConverter.ToUInt16(known[slot], 8) == pass.Clock
                )
                {
                    var bytes = (byte[])known[slot].Clone();
                    BitConverter.GetBytes(timer).CopyTo(bytes, 2);
                    BitConverter.GetBytes(stamp).CopyTo(bytes, 8);
                    before[slot] = bytes;
                }
            }

            Check($"{name}: pass {pass.Number}, before the view's turn", before);

            void Check(string where, SortedDictionary<int, byte[]> state)
            {
                var settlers = state.Values.Select(bytes => FromBytes(bytes, width)).ToList();
                var drawn = graphics.Figures.Draws(map, settlers, pass.Clock, graphics.Shifts);
                var actual = drawn
                    .Select(draw => new Sprite(draw.Entry, draw.Colour, draw.X + OffsetX, draw.Y + OffsetY))
                    .ToList();

                var next = 0;
                foreach (var record in pass.Records)
                {
                    var got = actual.Skip(next).Take(record.Sprites.Count).ToList();
                    Assert.IsTrue(
                        got.SequenceEqual(record.Sprites),
                        $"{where} settler {record.Slot} (state {state[record.Slot][10]:X}, frame {state[record.Slot][1]:X}): "
                            + $"the original draws {Describe(record.Sprites)}, the engine draws {(got.Count == 0 ? "nothing" : Describe(got))}"
                    );
                    next += record.Sprites.Count;
                }

                Assert.AreEqual(
                    next,
                    actual.Count,
                    $"{where}: the engine draws more than the original: {Describe(actual.Skip(next))} "
                        + $"(settlers {string.Join(",", state.Keys)})"
                );
            }
        }
    }
}
