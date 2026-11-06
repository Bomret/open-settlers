using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OpenSettlers.MapView;

/// <summary>One step of a play script; every step keeps the line of the script it came from.</summary>
public abstract record PlayStep
{
    /// <summary>The 1-based line of the step in the script.</summary>
    public required int Line { get; init; }
}

/// <summary><c>wait &lt;seconds&gt;</c>: lets simulated time pass.</summary>
public sealed record WaitStep : PlayStep
{
    /// <summary>The simulated seconds to wait; zero or more.</summary>
    public required double Seconds { get; init; }
}

/// <summary><c>press &lt;key&gt;</c>: a key press and release; a chord names its modifiers first (<c>Shift+Equal</c>).</summary>
public sealed record PressStep : PlayStep
{
    /// <summary>The key as Godot names it; the executor resolves it.</summary>
    public required string Key { get; init; }
}

/// <summary><c>hold &lt;key&gt; &lt;seconds&gt;</c>: presses a key, lets the time pass like a wait, and releases it.</summary>
public sealed record HoldStep : PlayStep
{
    /// <summary>The key as Godot names it.</summary>
    public required string Key { get; init; }

    /// <summary>The simulated seconds the key stays down; zero or more.</summary>
    public required double Seconds { get; init; }
}

/// <summary>The release of a key a <see cref="HoldStep"/> pressed; the run adds it when the hold's time has passed.</summary>
public sealed record ReleaseStep : PlayStep
{
    /// <summary>The key as Godot names it.</summary>
    public required string Key { get; init; }
}

/// <summary><c>scroll &lt;up|down&gt; [count [x y]]</c>: mouse wheel notches at a canvas point.</summary>
public sealed record ScrollStep : PlayStep
{
    /// <summary>Whether the wheel turns up.</summary>
    public required bool Up { get; init; }

    /// <summary>The number of notches; one or more.</summary>
    public required int Count { get; init; }

    /// <summary>The canvas point of the wheel; null for the middle of the canvas.</summary>
    public (float X, float Y)? At { get; init; }
}

/// <summary><c>drag &lt;x1&gt; &lt;y1&gt; &lt;x2&gt; &lt;y2&gt;</c>: a left-button drag between two canvas points.</summary>
public sealed record DragStep : PlayStep
{
    /// <summary>The start x in canvas pixels.</summary>
    public required float FromX { get; init; }

    /// <summary>The start y in canvas pixels.</summary>
    public required float FromY { get; init; }

    /// <summary>The end x in canvas pixels.</summary>
    public required float ToX { get; init; }

    /// <summary>The end y in canvas pixels.</summary>
    public required float ToY { get; init; }
}

/// <summary><c>pinch &lt;factor&gt;</c>: a touchpad pinch at the middle of the canvas.</summary>
public sealed record PinchStep : PlayStep
{
    /// <summary>The factor the pinch multiplies the zoom by; above zero.</summary>
    public required float Factor { get; init; }
}

/// <summary><c>swipe &lt;dx&gt; &lt;dy&gt;</c>: a two-finger touchpad swipe at the middle of the canvas.</summary>
public sealed record SwipeStep : PlayStep
{
    /// <summary>The swipe's horizontal delta.</summary>
    public required float Dx { get; init; }

    /// <summary>The swipe's vertical delta.</summary>
    public required float Dy { get; init; }
}

/// <summary>
/// <c>shot &lt;name&gt; [&lt;column&gt; &lt;row&gt; [radius] [scale]]</c>: writes the last drawn frame to
/// <c>&lt;name&gt;.png</c> of the output folder; with a tile, only the part around it (<see cref="ShotCrop"/>).
/// </summary>
public sealed record ShotStep : PlayStep
{
    /// <summary>The file stem; it holds no path separator.</summary>
    public required string Name { get; init; }

    /// <summary>The tile to crop around; null for the whole frame.</summary>
    public ShotTile? Tile { get; init; }
}

/// <summary>
/// The tile a <see cref="ShotStep"/> crops around: its column and row, zero or more; the radius in steps, zero or
/// more; and the whole factor that enlarges the crop, 1 or more.
/// </summary>
public sealed record ShotTile(int Column, int Row, int Radius, int Scale);

/// <summary><c>click-tile &lt;column&gt; &lt;row&gt;</c>: a left click on the map tile where it shows on the canvas.</summary>
public sealed record ClickTileStep : PlayStep
{
    /// <summary>The tile's column; zero or more.</summary>
    public required int Column { get; init; }

    /// <summary>The tile's row; zero or more.</summary>
    public required int Row { get; init; }
}

/// <summary><c>button &lt;name&gt;</c>: a left click on the shown game-view button whose label is the name.</summary>
public sealed record ButtonStep : PlayStep
{
    /// <summary>The button's label, matched exactly, case included.</summary>
    public required string Name { get; init; }

    /// <summary>Null when <paramref name="shown"/>, the labels of the shown buttons, holds the name; else the problem, listing them.</summary>
    public string? Missing(IReadOnlyList<string> shown) =>
        shown.Contains(Name) ? null
        : shown.Count == 0 ? $"no button '{Name}': no button is shown"
        : $"no button '{Name}', shown: {string.Join(", ", shown)}";
}

/// <summary><c>dump &lt;name&gt;</c>: writes the view's state to <c>&lt;name&gt;.txt</c> of the output folder.</summary>
public sealed record DumpStep : PlayStep
{
    /// <summary>The file stem; it holds no path separator.</summary>
    public required string Name { get; init; }
}

/// <summary><c>list &lt;name&gt;</c>: writes the things the view shows to <c>&lt;name&gt;.things.txt</c> of the output folder.</summary>
public sealed record ListStep : PlayStep
{
    /// <summary>The file stem; it holds no path separator.</summary>
    public required string Name { get; init; }
}

/// <summary><c>expect &lt;field&gt; &lt;op&gt; &lt;value&gt;</c>: checks one field of the <see cref="PlayState"/>.</summary>
public sealed record ExpectStep : PlayStep
{
    /// <summary>A name of <see cref="PlayFields"/>.</summary>
    public required string Field { get; init; }

    /// <summary>One of <c>&lt;</c>, <c>&lt;=</c>, <c>=</c>, <c>&gt;=</c>, <c>&gt;</c>; the seed takes only <c>=</c>.</summary>
    public required string Op { get; init; }

    /// <summary>The value as written: a number, or the seed's words joined by single spaces.</summary>
    public required string Value { get; init; }
}

/// <summary>
/// <c>expect-at &lt;column&gt; &lt;row&gt; &lt;what&gt;</c>: checks that a thing matching <see cref="What"/> is at the node. The
/// run checks the node against the map's size.
/// </summary>
public sealed record ExpectAtStep : PlayStep
{
    /// <summary>The node's column; zero or more.</summary>
    public required int Column { get; init; }

    /// <summary>The node's row; zero or more.</summary>
    public required int Row { get; init; }

    /// <summary>What a thing at the node must be.</summary>
    public required ThingSelector What { get; init; }
}

/// <summary><c>expect-count &lt;what&gt; &lt;op&gt; &lt;n&gt;</c>: compares the number of things matching <see cref="What"/> with a count.</summary>
public sealed record ExpectCountStep : PlayStep
{
    /// <summary>What a thing is counted for.</summary>
    public required ThingSelector What { get; init; }

    /// <summary>One of <c>&lt;</c>, <c>&lt;=</c>, <c>=</c>, <c>&gt;=</c>, <c>&gt;</c>.</summary>
    public required string Op { get; init; }

    /// <summary>The count to compare with; zero or more.</summary>
    public required int Count { get; init; }
}

/// <summary><c>name &lt;alias&gt; &lt;column&gt; &lt;row&gt; &lt;what&gt;</c>: saves the ID of the one thing at the node that matches <see cref="What"/>.</summary>
public sealed record NameStep : PlayStep
{
    /// <summary>The alias the ID is saved under.</summary>
    public required string Alias { get; init; }

    /// <summary>The node's column; zero or more.</summary>
    public required int Column { get; init; }

    /// <summary>The node's row; zero or more.</summary>
    public required int Row { get; init; }

    /// <summary>What the thing must be.</summary>
    public required ThingSelector What { get; init; }
}

/// <summary><c>mark &lt;label&gt;</c>: saves the game as it stands now under a label.</summary>
public sealed record MarkStep : PlayStep
{
    /// <summary>The label; not a whole number.</summary>
    public required string Label { get; init; }
}

/// <summary>
/// <c>expect &lt;alias&gt;.&lt;field&gt;[@&lt;label&gt;|@&lt;tick&gt;] &lt;op&gt; &lt;value&gt;</c>: checks a field of a named thing now, at a mark, or at a tick.
/// </summary>
public sealed record ExpectThingStep : PlayStep
{
    /// <summary>The alias of the thing.</summary>
    public required string Alias { get; init; }

    /// <summary>A field of the thing's list line or of its state in an event.</summary>
    public required string Field { get; init; }

    /// <summary>The mark label after <c>@</c>; null for none.</summary>
    public string? Mark { get; init; }

    /// <summary>The tick after <c>@</c>; null for none.</summary>
    public long? Tick { get; init; }

    /// <summary>One of <c>&lt;</c>, <c>&lt;=</c>, <c>=</c>, <c>&gt;=</c>, <c>&gt;</c>; a link field takes only <c>=</c>.</summary>
    public required string Op { get; init; }

    /// <summary>The value as written: a number, a thing ID, or an alias.</summary>
    public required string Value { get; init; }

    /// <summary>The value as a number, read when the script is parsed; 0 for a link field, whose value is no number.</summary>
    public double Number { get; init; }
}

/// <summary><c>expect-event &lt;alias&gt; &lt;type&gt; [cause &lt;cause&gt;]</c>: checks that the thing has an event of the type, with the cause when given.</summary>
public sealed record ExpectEventStep : PlayStep
{
    /// <summary>The alias of the thing.</summary>
    public required string Alias { get; init; }

    /// <summary>One of <see cref="Simulation.EventType.All"/>.</summary>
    public required string Type { get; init; }

    /// <summary>One of <see cref="Simulation.Cause.Kinds"/>; null for any cause.</summary>
    public string? Cause { get; init; }
}

/// <summary><c>expect-gone &lt;alias&gt;</c>: checks that the thing is gone.</summary>
public sealed record ExpectGoneStep : PlayStep
{
    /// <summary>The alias of the thing.</summary>
    public required string Alias { get; init; }
}

/// <summary><c>events &lt;alias&gt; &lt;file&gt;</c>: writes the thing's events to <c>&lt;file&gt;.events.txt</c>.</summary>
public sealed record EventsStep : PlayStep
{
    /// <summary>The alias of the thing.</summary>
    public required string Alias { get; init; }

    /// <summary>The file stem.</summary>
    public required string Name { get; init; }
}

/// <summary>
/// A play script (<c>CONTEXT.md</c>): a text file with one step per line, run in file order. Blank lines and lines
/// that start with <c>#</c> are skipped. A line that cannot be read is a problem naming the script and the line and
/// adds no step. Reading never throws for bad text; the play run reports the problems.
/// </summary>
public sealed class PlayScript
{
    /// <summary>The script's path as given; problems name it.</summary>
    public required string Path { get; init; }

    /// <summary>The script's file name without extension: the name of its output folder under <c>play-out/</c>.</summary>
    public string Stem => System.IO.Path.GetFileNameWithoutExtension(Path);

    /// <summary>The steps in file order.</summary>
    public required IReadOnlyList<PlayStep> Steps { get; init; }

    /// <summary>One line per problem, each naming the script and the line.</summary>
    public required IReadOnlyList<string> Problems { get; init; }

    /// <summary>Reads the script at <c>path</c>; a file that cannot be read is one problem and no steps.</summary>
    public static PlayScript Read(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path), path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new PlayScript
            {
                Path = path,
                Steps = [],
                Problems = [$"{path}: script cannot be read: {e.Message}"],
            };
        }
    }

    /// <summary>Parses <c>text</c>; <c>path</c> only names the script in problems and in <see cref="Path"/>.</summary>
    public static PlayScript Parse(string text, string path)
    {
        var steps = new List<PlayStep>();
        var problems = new List<string>();
        var lines = text.Split('\n');
        var declared = new Declared();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var (step, problem) = ParseLine(line, i + 1, declared);
            if (step is not null)
                steps.Add(step);
            else
                problems.Add($"{path} line {i + 1}: {problem}");
        }

        return new PlayScript
        {
            Path = path,
            Steps = steps,
            Problems = problems,
        };
    }

    // The aliases and marks the steps before the current line declared.
    sealed class Declared
    {
        public HashSet<string> Aliases { get; } = [];

        public HashSet<string> Marks { get; } = [];
    }

    static (PlayStep? Step, string? Problem) ParseLine(string line, int number, Declared declared)
    {
        var words = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var args = words.Skip(1).ToArray();
        switch (words[0])
        {
            case "wait":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (!Seconds(args[0], out var seconds))
                    return (null, $"'{args[0]}' is not a number of seconds of zero or more");

                return (new WaitStep { Line = number, Seconds = seconds }, null);
            case "press":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);

                return KeyProblem(args[0]) is { } bad
                    ? (null, bad)
                    : (new PressStep { Line = number, Key = args[0] }, null);
            case "hold":
                if (args.Length != 2)
                    return Wrong(words[0], "2 arguments", args.Length);
                if (KeyProblem(args[0]) is { } badKey)
                    return (null, badKey);
                if (!Seconds(args[1], out var held))
                    return (null, $"'{args[1]}' is not a number of seconds of zero or more");

                return (
                    new HoldStep
                    {
                        Line = number,
                        Key = args[0],
                        Seconds = held,
                    },
                    null
                );
            case "scroll":
                return ParseScroll(args, number);
            case "drag":
                if (args.Length != 4)
                    return Wrong(words[0], "4 arguments", args.Length);
                var (edges, edge) = Numbers(args);
                if (edges is null)
                    return (null, $"'{edge}' is not a number");

                return (
                    new DragStep
                    {
                        Line = number,
                        FromX = edges[0],
                        FromY = edges[1],
                        ToX = edges[2],
                        ToY = edges[3],
                    },
                    null
                );
            case "pinch":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                var (factor, _) = Numbers(args);
                if (factor is null || factor[0] <= 0)
                    return (null, $"'{args[0]}' is not a positive number");

                return (new PinchStep { Line = number, Factor = factor[0] }, null);
            case "swipe":
                if (args.Length != 2)
                    return Wrong(words[0], "2 arguments", args.Length);
                var (delta, notNumber) = Numbers(args);
                if (delta is null)
                    return (null, $"'{notNumber}' is not a number");

                return (
                    new SwipeStep
                    {
                        Line = number,
                        Dx = delta[0],
                        Dy = delta[1],
                    },
                    null
                );
            case "click-tile":
                if (args.Length != 2)
                    return Wrong(words[0], "2 arguments", args.Length);
                foreach (var arg in args)
                {
                    if (!int.TryParse(arg, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                        return (null, $"'{arg}' is not a whole number of zero or more");
                }

                return (
                    new ClickTileStep
                    {
                        Line = number,
                        Column = int.Parse(args[0], CultureInfo.InvariantCulture),
                        Row = int.Parse(args[1], CultureInfo.InvariantCulture),
                    },
                    null
                );
            case "button":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);

                return (new ButtonStep { Line = number, Name = args[0] }, null);
            case "fast-forward":
                return ParseFastForward(args, number);
            case "expect":
                return args.Length > 0 && args[0].Contains('.')
                    ? ParseExpectThing(args, number, declared)
                    : ParseExpect(args, number);
            case "name":
                return ParseName(args, number, declared);
            case "mark":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    return (null, $"'{args[0]}' is a number, a mark's label is not");
                if (!declared.Marks.Add(args[0]))
                    return (null, $"mark '{args[0]}' is given twice");

                return (new MarkStep { Line = number, Label = args[0] }, null);
            case "expect-event":
                return ParseExpectEvent(args, number, declared);
            case "expect-gone":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);

                return declared.Aliases.Contains(args[0])
                    ? (new ExpectGoneStep { Line = number, Alias = args[0] }, null)
                    : (null, $"unknown alias '{args[0]}'");
            case "events":
                if (args.Length != 2)
                    return Wrong(words[0], "2 arguments", args.Length);
                if (!declared.Aliases.Contains(args[0]))
                    return (null, $"unknown alias '{args[0]}'");
                if (FileNameProblem(args[1]) is { } badEvents)
                    return (null, badEvents);

                return (
                    new EventsStep
                    {
                        Line = number,
                        Alias = args[0],
                        Name = args[1],
                    },
                    null
                );
            case "expect-at":
                return ParseExpectAt(args, number);
            case "expect-count":
                return ParseExpectCount(args, number);
            case "shot":
                if (args.Length is not (1 or 3 or 4 or 5))
                    return Wrong(words[0], "1, 3, 4, or 5 arguments", args.Length);
                if (FileNameProblem(args[0]) is { } badName)
                    return (null, badName);
                if (args.Length == 1)
                    return (new ShotStep { Line = number, Name = args[0] }, null);

                var whole = new int[4] { 0, 0, 1, 1 };
                for (var i = 1; i < args.Length; i++)
                {
                    // The scale, the fifth argument, is 1 or more; the others are zero or more.
                    var least = i == 4 ? 1 : 0;
                    if (
                        !int.TryParse(args[i], NumberStyles.None, CultureInfo.InvariantCulture, out whole[i - 1])
                        || whole[i - 1] < least
                    )
                        return (null, $"'{args[i]}' is not a whole number of {(least == 0 ? "zero" : "1")} or more");
                }

                return (
                    new ShotStep
                    {
                        Line = number,
                        Name = args[0],
                        Tile = new ShotTile(whole[0], whole[1], whole[2], whole[3]),
                    },
                    null
                );
            case "dump":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (FileNameProblem(args[0]) is { } badDump)
                    return (null, badDump);

                return (new DumpStep { Line = number, Name = args[0] }, null);
            case "list":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (FileNameProblem(args[0]) is { } badList)
                    return (null, badList);

                return (new ListStep { Line = number, Name = args[0] }, null);
            default:
                return (null, $"unknown step '{words[0]}'");
        }
    }

    // fast-forward <job> state <n> max <ticks> | fast-forward <job> carrying max <ticks>; <n> is decimal or 0x hex.
    static (PlayStep?, string?) ParseFastForward(string[] args, int number)
    {
        const string Usage = "'fast-forward' takes <job> state <n> max <ticks> or <job> carrying max <ticks>";
        var state = args.Length == 5 && args[1] == "state" && args[3] == "max";
        if (!state && !(args.Length == 4 && args[1] == "carrying" && args[2] == "max"))
            return (null, Usage);

        // The job names are the name table's (ADR 0010); fast-forward watches only these two jobs.
        var job = new[] { Simulation.Settler.WoodcutterJob, Simulation.Settler.CarrierJob }.FirstOrDefault(
            code => Simulation.NameTable.JobName(code) == args[0],
            -1
        );
        if (job < 0)
            return (null, $"'{args[0]}' is not woodcutter or carrier");

        var n = 0;
        if (state && !Whole(args[2], out n))
            return (null, $"'{args[2]}' is not a whole number of zero or more");
        if (!Whole(args[^1], out var max))
            return (null, $"'{args[^1]}' is not a whole number of zero or more");

        return (
            new FastForwardStep
            {
                Line = number,
                Job = job,
                State = state ? n : null,
                MaxTicks = max,
                Text = "fast-forward " + string.Join(' ', args),
            },
            null
        );
    }

    // A whole number of zero or more, decimal or 0x hex.
    internal static bool Whole(string text, out int value) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(text[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
                && value >= 0
            : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    static readonly string[] Ops = ["<", "<=", "=", ">=", ">"];

    // expect <field> <op> <value>; the value of the seed is every word after the operator.
    static (PlayStep?, string?) ParseExpect(string[] args, int number)
    {
        if (args.Length < 3)
            return (null, $"'expect' takes a field, an operator, and a value, found {args.Length} arguments");
        if (!PlayFields.IsKnown(args[0]))
            return (
                null,
                $"unknown field '{args[0]}', use {string.Join(", ", PlayFields.All.Concat(PlayFields.Game))}, {PlayFields.FlagGoods}<column>,<row>"
            );
        if (!Ops.Contains(args[1]))
            return (null, $"unknown operator '{args[1]}', use one of {string.Join(" ", Ops)}");

        var value = string.Join(' ', args[2..]);
        if (!PlayFields.IsNumeric(args[0]))
        {
            if (args[1] != "=")
                return (null, $"'{args[0]}' takes only '=', not '{args[1]}'");
        }
        else if (args.Length != 3 || !Number(value, out _))
        {
            return (null, $"'{value}' is not a number");
        }

        return (
            new ExpectStep
            {
                Line = number,
                Field = args[0],
                Op = args[1],
                Value = value,
            },
            null
        );
    }

    // name <alias> <column> <row> <what>
    static (PlayStep?, string?) ParseName(string[] args, int number, Declared declared)
    {
        if (args.Length != 4)
            return Wrong("name", "4 arguments", args.Length);
        if (!NamedThings.IsAliasText(args[0]))
            return (null, $"'{args[0]}' is no alias, use letters, digits, '-', and '_'");
        if (PlayFields.IsKnown(args[0]))
            return (null, $"'{args[0]}' is a field of 'expect' and cannot be an alias");
        if (declared.Aliases.Contains(args[0]))
            return (null, $"alias '{args[0]}' is given twice");
        if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var column))
            return (null, $"'{args[1]}' is not a whole number of zero or more");
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
            return (null, $"'{args[2]}' is not a whole number of zero or more");

        var (what, problem) = ThingSelector.Parse(args[3]);
        if (what is null)
            return (null, problem);

        declared.Aliases.Add(args[0]);

        return (
            new NameStep
            {
                Line = number,
                Alias = args[0],
                Column = column,
                Row = row,
                What = what,
            },
            null
        );
    }

    // expect <alias>.<field>[@<label>|@<tick>] <op> <value>
    static (PlayStep?, string?) ParseExpectThing(string[] args, int number, Declared declared)
    {
        if (args.Length != 3)
            return Wrong("expect", "a field, an operator, and a value", args.Length);

        var dot = args[0].IndexOf('.');
        var alias = args[0][..dot];
        var rest = args[0][(dot + 1)..];
        var at = rest.IndexOf('@');
        var field = at < 0 ? rest : rest[..at];
        if (!declared.Aliases.Contains(alias))
            return (null, $"unknown alias '{alias}'");
        if (!NamedThings.IsField(field))
            return (null, $"unknown field '{field}', use one of {string.Join(", ", NamedThings.Fields)}");

        string? mark = null;
        long? tick = null;
        if (at >= 0)
        {
            var target = rest[(at + 1)..];
            if (long.TryParse(target, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                tick = parsed;
            else if (declared.Marks.Contains(target))
                mark = target;
            else
                return (null, $"unknown mark '{target}'");
        }

        if (!Ops.Contains(args[1]))
            return (null, $"unknown operator '{args[1]}', use one of {string.Join(" ", Ops)}");
        var wanted = 0.0;
        if (NamedThings.IsLink(field))
        {
            if (args[1] != "=")
                return (null, $"'{field}' takes only '=', not '{args[1]}'");
            if (args[2].Contains('#'))
            {
                if (!NamedThings.IsThingId(args[2]))
                    return (null, $"'{args[2]}' is not a thing ID like settler#12.3");
            }
            else if (!declared.Aliases.Contains(args[2]) && !args[2].Contains('@'))
            {
                return (null, $"unknown alias '{args[2]}'");
            }
        }
        else if (!NamedThings.Numeric(args[2], out wanted))
        {
            return (null, $"'{args[2]}' is not a number");
        }

        return (
            new ExpectThingStep
            {
                Line = number,
                Alias = alias,
                Field = field,
                Mark = mark,
                Tick = tick,
                Op = args[1],
                Value = args[2],
                Number = wanted,
            },
            null
        );
    }

    // expect-event <alias> <type> [cause <cause>]
    static (PlayStep?, string?) ParseExpectEvent(string[] args, int number, Declared declared)
    {
        if (args.Length is not (2 or 4))
            return (
                null,
                $"'expect-event' takes an alias, a type, and optionally 'cause' and a cause, found {args.Length} arguments"
            );
        if (!declared.Aliases.Contains(args[0]))
            return (null, $"unknown alias '{args[0]}'");
        if (!Simulation.EventType.All.Contains(args[1]))
            return (null, $"unknown event type '{args[1]}', use one of {string.Join(", ", Simulation.EventType.All)}");
        if (args.Length == 4 && args[2] != "cause")
            return (null, $"'{args[2]}' is not 'cause'");
        if (args.Length == 4 && !Simulation.Cause.Kinds.Contains(args[3]))
            return (null, $"unknown cause '{args[3]}', use one of {string.Join(", ", Simulation.Cause.Kinds)}");

        return (
            new ExpectEventStep
            {
                Line = number,
                Alias = args[0],
                Type = args[1],
                Cause = args.Length == 4 ? args[3] : null,
            },
            null
        );
    }

    // expect-at <column> <row> <what>; the node's place in the map is checked when the run reaches the step.
    static (PlayStep?, string?) ParseExpectAt(string[] args, int number)
    {
        if (args.Length != 3)
            return Wrong("expect-at", "3 arguments", args.Length);
        if (!int.TryParse(args[0], NumberStyles.None, CultureInfo.InvariantCulture, out var column))
            return (null, $"'{args[0]}' is not a whole number of zero or more");
        if (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var row))
            return (null, $"'{args[1]}' is not a whole number of zero or more");

        var (what, problem) = ThingSelector.Parse(args[2]);
        if (what is null)
            return (null, problem);

        return (
            new ExpectAtStep
            {
                Line = number,
                Column = column,
                Row = row,
                What = what,
            },
            null
        );
    }

    // expect-count <what> <op> <n>
    static (PlayStep?, string?) ParseExpectCount(string[] args, int number)
    {
        if (args.Length != 3)
            return Wrong("expect-count", "3 arguments", args.Length);

        var (what, problem) = ThingSelector.Parse(args[0]);
        if (what is null)
            return (null, problem);
        if (!Ops.Contains(args[1]))
            return (null, $"unknown operator '{args[1]}', use one of {string.Join(" ", Ops)}");
        if (!int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
            return (null, $"'{args[2]}' is not a whole number of zero or more");

        return (
            new ExpectCountStep
            {
                Line = number,
                What = what,
                Op = args[1],
                Count = count,
            },
            null
        );
    }

    // scroll <up|down> [count [x y]]
    static (PlayStep?, string?) ParseScroll(string[] args, int number)
    {
        if (args.Length is not (1 or 2 or 4))
            return (null, $"'scroll' takes 1 to 4 arguments (up|down, a count, a point), found {args.Length}");
        if (args[0] is not ("up" or "down"))
            return (null, $"'{args[0]}' is not up or down");

        var count = 1;
        if (
            args.Length > 1
            && (!int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1)
        )
            return (null, $"'{args[1]}' is not a whole number of one or more");

        (float, float)? at = null;
        if (args.Length == 4)
        {
            var (point, bad) = Numbers(args[2..]);
            if (point is null)
                return (null, $"'{bad}' is not a number");
            at = (point[0], point[1]);
        }

        return (
            new ScrollStep
            {
                Line = number,
                Up = args[0] == "up",
                Count = count,
                At = at,
            },
            null
        );
    }

    static readonly string[] Modifiers = ["Shift", "Ctrl", "Alt", "Meta", "Option", "Command", "Windows"];

    // A key is a Godot key name, with modifiers first, joined by '+' (Shift+Equal). The executor resolves the name.
    static string? KeyProblem(string key)
    {
        var parts = key.Split('+');
        if (parts[^1].Length == 0)
            return $"'{key}' names no key";

        var unknown = parts[..^1].FirstOrDefault(p => !Modifiers.Contains(p));

        return unknown is null ? null : $"'{unknown}' is no modifier, use {string.Join(", ", Modifiers)}";
    }

    // The arguments as floats, or the first one that is no number.
    static (float[]?, string) Numbers(string[] args)
    {
        var numbers = new float[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            if (!Number(args[i], out var value) || float.IsInfinity((float)value))
                return (null, args[i]);
            numbers[i] = (float)value;
        }

        return (numbers, "");
    }

    // A file stem is a name of its own, with no path in it.
    static string? FileNameProblem(string name) =>
        name is "." or ".." || name.IndexOfAny(['/', '\\']) >= 0 ? $"'{name}' is no file name" : null;

    static (PlayStep?, string?) Wrong(string step, string want, int got) =>
        (null, $"'{step}' takes {want}, found {got}");

    static bool Seconds(string text, out double value) => Number(text, out value) && value >= 0;

    static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
