using System.Collections.Generic;
using System.Text;

namespace OpenSettlers.Formats;

/// <summary>One key of a text file and its value.</summary>
public sealed record TextEntry(string Key, string Value);

/// <summary>A line of a text file that the program's parser does not accept, with its 1-based number and the reason.</summary>
public sealed record RejectedLine(int Line, string Reason);

/// <summary>
/// The keys and values of one language: what the program's text parser reads from a file under <c>loca/</c>
/// (<c>docs/rules/text-files.md</c>), and the lines it does not accept.
/// </summary>
public sealed class TextFile
{
    /// <summary>The longest line the program reads, in bytes before its terminator.</summary>
    public const int LongestLine = 254;

    /// <summary>The fewest bytes a line holds before its terminator for the program to look at it.</summary>
    public const int ShortestLine = 5;

    readonly Dictionary<string, string> _values;

    TextFile(List<TextEntry> entries, List<RejectedLine> rejected, Dictionary<string, string> values)
    {
        Entries = entries;
        Rejected = rejected;
        _values = values;
    }

    /// <summary>The keys in order of first appearance, each with the value of its last line.</summary>
    public IReadOnlyList<TextEntry> Entries { get; }

    /// <summary>The lines the program does not accept, in file order.</summary>
    public IReadOnlyList<RejectedLine> Rejected { get; }

    /// <summary>The value of <c>key</c>, or null when the file has no such key. Keys are case-sensitive.</summary>
    public string? Get(string key) => _values.GetValueOrDefault(key);

    /// <summary>
    /// Reads a text file the way the program does. Never throws: a line the program does not accept is listed in
    /// <see cref="Rejected"/> and reading goes on. Each byte is one character.
    /// </summary>
    public static TextFile Read(byte[] bytes)
    {
        var order = new List<string>();
        var values = new Dictionary<string, string>();
        var rejected = new List<RejectedLine>();

        // The runtime's text mode ends the file at the first Ctrl-Z.
        var end = System.Array.IndexOf(bytes, (byte)0x1A);
        if (end < 0)
        {
            end = bytes.Length;
        }

        var number = 0;
        var start = 0;
        while (start < end)
        {
            var stop = start;
            while (stop < end && bytes[stop] != '\r' && bytes[stop] != '\n')
            {
                stop++;
            }

            number++;
            var length = stop - start;
            var reason = Parse(Encoding.Latin1.GetString(bytes, start, length), length, out var key, out var value);
            if (reason != null)
            {
                rejected.Add(new RejectedLine(number, reason));
            }
            else
            {
                if (!values.ContainsKey(key!))
                {
                    order.Add(key!);
                }

                values[key!] = value!;
            }

            // A carriage return and a line feed together are one terminator.
            start = stop + (stop + 1 < end && bytes[stop] == '\r' && bytes[stop + 1] == '\n' ? 2 : 1);
        }

        var entries = order.ConvertAll(k => new TextEntry(k, values[k]));

        return new TextFile(entries, rejected, values);
    }

    // The reason the line is not accepted, or null with the key and the value.
    static string? Parse(string line, int length, out string? key, out string? value)
    {
        key = value = null;
        if (length > LongestLine)
        {
            return $"too long: {length} bytes, the program stops on a line over {LongestLine}";
        }

        if (length < ShortestLine)
        {
            return $"too short: {length} bytes, the program skips a line under {ShortestLine}";
        }

        // The program searches a C string, so a NUL byte ends what it sees.
        var seen = line.IndexOf('\0') is var nul and >= 0 ? line[..nul] : line;
        var equals = seen.IndexOf('=');
        if (equals < 0)
        {
            return "no '=' between key and value";
        }

        var open = seen.IndexOf('"', equals + 1);
        if (open < 0)
        {
            return "no opening quote after '='";
        }

        var close = seen.IndexOf('"', open + 1);
        if (close < 0)
        {
            return "no closing quote after the opening quote";
        }

        key = seen[..equals];
        value = seen[(open + 1)..close];

        return null;
    }
}
