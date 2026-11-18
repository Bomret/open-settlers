using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenSettlers.Domain.Testing;

/// <summary>One sprite the original blitted: the entry, the palette index (-1 for a sprite blitted as it is), and the position.</summary>
public sealed record RecordedSprite
{
    /// <summary>The sprite entry.</summary>
    public required int Entry { get; init; }

    /// <summary>The palette index of the fill; -1 for none.</summary>
    public required int Colour { get; init; }

    /// <summary>The horizontal map pixel.</summary>
    public required int X { get; init; }

    /// <summary>The vertical map pixel.</summary>
    public required int Y { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"{Entry}/{Colour}/{X},{Y}";
}

/// <summary>The sprites the original drew for one settler, in the order it drew them.</summary>
public sealed record RecordedRecord
{
    /// <summary>The settler's slot.</summary>
    public required int Slot { get; init; }

    /// <summary>The sprites of the record.</summary>
    public required List<RecordedSprite> Sprites { get; init; }
}

/// <summary>One pass of a recorded run: the clock, the settlers that changed, the timers they held, and what the pass drew.</summary>
public sealed record RecordedPass
{
    /// <summary>The pass number.</summary>
    public required int Number { get; init; }

    /// <summary>The game's clock.</summary>
    public required ushort Clock { get; init; }

    /// <summary>The 16 bytes and the node's height byte of each settler whose bytes changed since the pass before.</summary>
    public required Dictionary<int, (byte[] Bytes, int Height)> Changed { get; init; }

    /// <summary>The timer and stamp each drawn settler held before the view's turn.</summary>
    public required Dictionary<int, (ushort Timer, ushort Stamp)> Held { get; init; }

    /// <summary>The records the pass drew, in the order it drew them.</summary>
    public required List<RecordedRecord> Records { get; init; }
}

/// <summary>One scenario of the recording: its name, the verb command, the header lines, and its passes.</summary>
public sealed record RecordedScenario
{
    /// <summary>The scenario's name.</summary>
    public required string Name { get; init; }

    /// <summary>The emulator verb command: <c>draw:WINDOWS:CX,CY:VERB:N:SEED:...</c>.</summary>
    public required string Command { get; init; }

    /// <summary>The lines before the first pass, among them the <c>install=</c> line.</summary>
    public required List<string> Header { get; init; }

    /// <summary>The passes.</summary>
    public required List<RecordedPass> Passes { get; init; }
}

/// <summary>
/// The reader of <c>settler-draws.txt</c>, the output of the emulator verb <c>draw</c> (<c>tools/GhidraDump/emulate.py</c>,
/// ADR 0008): the settler draw path of the original's main loop run after the passes of a game, once for each pass of its
/// windows. Each scenario starts with a "#" line that names its verb command; the line "install=" names the install the run
/// came from; a "pass=" line gives the clock, the 16 bytes of each settler whose bytes changed since the line before and the
/// height byte of its node (<c>s&lt;slot&gt;=bytes/height</c>), the timer and stamp each drawn settler held before the view's
/// turn (<c>t&lt;slot&gt;=</c>), and what the pass drew (<c>d=</c>): one record for each settler the draw path drew, as
/// <c>&lt;slot&gt;:&lt;entry&gt;/&lt;colour&gt;/&lt;x&gt;,&lt;y&gt;;...</c>.
/// </summary>
public static class RecordedDraws
{
    /// <summary>The scenarios of the file at <paramref name="path"/>.</summary>
    public static List<RecordedScenario> Load(string path)
    {
        var scenarios = new List<RecordedScenario>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                var parts = line[2..].Split(' ', 2);
                scenarios.Add(
                    new RecordedScenario
                    {
                        Name = parts[0],
                        Command = parts[1],
                        Header = [],
                        Passes = [],
                    }
                );
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

    /// <summary>The sha256 of the build the recordings came from: the first <c>install=</c> line of the file.</summary>
    public static string ReferenceSha256(string path) =>
        File.ReadLines(path)
            .First(line => line.StartsWith("install=", StringComparison.Ordinal))
            .Split(' ')
            .Single(part => part.StartsWith("sha256=", StringComparison.Ordinal))["sha256=".Length..];

    static RecordedPass ParsePass(string line)
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
                return new RecordedRecord
                {
                    Slot = int.Parse(halves[0], CultureInfo.InvariantCulture),
                    Sprites =
                    [
                        .. halves[1]
                            .Split(';')
                            .Select(sprite =>
                            {
                                var numbers = sprite.Split('/');
                                var position = numbers[2].Split(',');
                                return new RecordedSprite
                                {
                                    Entry = int.Parse(numbers[0], CultureInfo.InvariantCulture),
                                    Colour = int.Parse(numbers[1], CultureInfo.InvariantCulture),
                                    X = int.Parse(position[0], CultureInfo.InvariantCulture),
                                    Y = int.Parse(position[1], CultureInfo.InvariantCulture),
                                };
                            }),
                    ],
                };
            })
            .ToList();

        return new RecordedPass
        {
            Number = int.Parse(fields["pass"], CultureInfo.InvariantCulture),
            Clock = ushort.Parse(fields["clock"], CultureInfo.InvariantCulture),
            Changed = changed,
            Held = held,
            Records = records,
        };
    }

    /// <summary>
    /// The settler of the original's 16 bytes (<c>Table16</c>): the node is the dword at 4, <c>(y * 2 * width + x) * 4</c>, on a
    /// map <paramref name="width"/> nodes wide.
    /// </summary>
    public static Settler FromBytes(byte[] bytes, int width)
    {
        var node = BitConverter.ToInt32(bytes, 4) / 4;
        var state = bytes[10];

        // The job is the bits 2 to 6 of byte 0 (0x43f537 masks with 0x7c); the byte's bit 7 is not the job's.
        return new Settler(
            Job.Of((bytes[0] >> 2) & 0x1f),
            bytes[0] & 3,
            node % (2 * width),
            node / (2 * width),
            BitConverter.ToUInt16(bytes, 8),
            ActivityOf(bytes, state)
        )
        {
            Frame = bytes[1],
            Timer = BitConverter.ToUInt16(bytes, 2),
        };
    }

    // The importer of a recorded item's state bytes into the activity they stand for. A state the engine has no activity for
    // (a settler at work, or a state of the original's that the engine does not reach) takes the activity that is not drawn.
    static Activity ActivityOf(byte[] bytes, int state)
    {
        var mode = bytes[11];
        var target =
            state == 0x42 ? (BitConverter.ToInt32(bytes, 12) - 0x23000000) / 0x46 : BitConverter.ToUInt16(bytes, 12);
        var heading = new Heading { Value = (sbyte)bytes[14] };
        var next = bytes[15];
        var approach = new Heading { Value = (sbyte)BitConverter.ToUInt16(bytes, 8) };
        var load = mode > 0 ? new Load { Good = TestCatalog.GoodOf(mode), Destination = null } : (Load?)null;
        var vector = new Vector((sbyte)mode, (sbyte)target);
        var mark = target >> 8;

        return state switch
        {
            1 => new WaitsInCastle { Storage = heading.Value },
            2 => new Walks
            {
                Errand = new ToStorage(),
                Heading = heading,
                Tries = next,
            },
            3 => new WalksRoad
            {
                Load = load,
                Heading = heading,
                Waits = next == 0xff ? 0 : next,
                Marked = next == 0xff,
            },
            4 => new Door { TimeLeft = target, EnteringStorage = mode == 0xfe },
            9 => new Builds
            {
                Site = BitConverter.ToUInt16(bytes, 12),
                Blowing = mode == 0xff,
                Materials = bytes[14] & 0x0f,
                SecondFigure = (bytes[14] & 0x80) != 0,
                Blows = next,
            },
            0x4a => new LeavesSite(),
            8 => new Digs
            {
                Pass = mode,
                Target = bytes[12],
                Node = bytes[13],
                Phase = bytes[14],
            },
            5 => new WaitsOutStep
            {
                Then =
                    next == 0xd
                        ? new PutsDownGood { Good = TestCatalog.GoodOf(mode) }
                        : new Walks { Errand = new ToStorage(), Heading = heading },
            },
            0xb => new StepsOutWithGood { Good = TestCatalog.GoodOf(mode) },
            0xd => new PutsDownGood { Good = TestCatalog.GoodOf(mode) },
            0xe => new HandsOver
            {
                Load = load,
                Heading = heading,
                Marked = next == 0xff,
            },
            0xf => new Leaving { Errand = new ToStorage() },
            0x10 => new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = vector,
                    Purpose =
                        mark != 0x80 ? new ToTree { Back = new Vector((sbyte)mark, heading.Value) }
                        : heading.Value < 0 ? new Home()
                        : new BackToHut { Carrying = heading.Value != 0 },
                    Memory = next,
                },
            },
            0x11 => new Cuts
            {
                Back = new Vector((sbyte)mode, (sbyte)target),
                SecondVariant = mark != 0,
                Count = heading.Value,
                Memory = next,
            },
            0x12 => new LooksForTree(),
            0x13 => new LooksForSpot(),
            0x14 => new Plants
            {
                Back = vector,
                Planted = bytes[14] != 0,
                Memory = next,
            },
            0x15 => new LooksForStone(),
            0x16 => new WalksFree
            {
                Trip = new Trip
                {
                    Remaining = vector,
                    Purpose =
                        mark != 0x80 ? new ToHeap { Back = new Vector((sbyte)mark, heading.Value) }
                        : heading.Value < 0 ? new Home()
                        : new BackToHut { Carrying = heading.Value != 0 },
                    Memory = next,
                },
                EyesHeaps = true,
            },
            0x17 => new CutsStone
            {
                Back = vector,
                Phase = mark,
                Quarter = (bytes[14] | (next << 8)),
            },
            0x18 => new Saws { Sawing = mode != 0 },
            0x23 => new Grinds { Phase = mode },
            0x24 => new Bakes { Phase = mode },
            0x26 => new Butchers { Working = mode != 0 },
            0x25 => new TendsPigs { Phase = mode },
            0x19 => new SearchesHome { FarEndFirst = mode != 0, FarEndMark = mode },
            0x42 => new WaitsAtFlag
            {
                Flag = target,
                Direction = mode,
                Approach = approach,
            },
            0x43 => new ClaimsNode { Heading = heading, Approach = approach },
            0x44 => new WaitsToGoHome { Approach = approach },
            0x45 => new WaitsToRejoin { Approach = approach },
            _ => new BuiltCastle(),
        };
    }
}
