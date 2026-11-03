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

/// <summary><c>shot &lt;name&gt;</c>: writes the last drawn frame to <c>&lt;name&gt;.png</c> of the output folder.</summary>
public sealed record ShotStep : PlayStep
{
    /// <summary>The file stem; it holds no path separator.</summary>
    public required string Name { get; init; }
}

/// <summary><c>dump &lt;name&gt;</c>: writes the view's state to <c>&lt;name&gt;.txt</c> of the output folder.</summary>
public sealed record DumpStep : PlayStep
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
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var (step, problem) = ParseLine(line, i + 1);
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

    static (PlayStep? Step, string? Problem) ParseLine(string line, int number)
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
            case "expect":
                return ParseExpect(args, number);
            case "shot" or "dump":
                if (args.Length != 1)
                    return Wrong(words[0], "1 argument", args.Length);
                if (args[0] is "." or ".." || args[0].IndexOfAny(['/', '\\']) >= 0)
                    return (null, $"'{args[0]}' is no file name");

                return (
                    words[0] == "shot"
                        ? new ShotStep { Line = number, Name = args[0] }
                        : new DumpStep { Line = number, Name = args[0] },
                    null
                );
            default:
                return (null, $"unknown step '{words[0]}'");
        }
    }

    static readonly string[] Ops = ["<", "<=", "=", ">=", ">"];

    // expect <field> <op> <value>; the value of the seed is every word after the operator.
    static (PlayStep?, string?) ParseExpect(string[] args, int number)
    {
        if (args.Length < 3)
            return (null, $"'expect' takes a field, an operator, and a value, found {args.Length} arguments");
        if (!PlayFields.All.Contains(args[0]))
            return (null, $"unknown field '{args[0]}', use {string.Join(", ", PlayFields.All)}");
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

    static (PlayStep?, string?) Wrong(string step, string want, int got) =>
        (null, $"'{step}' takes {want}, found {got}");

    static bool Seconds(string text, out double value) => Number(text, out value) && value >= 0;

    static bool Number(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
}
