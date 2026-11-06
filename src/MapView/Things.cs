using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// A thing (<c>CONTEXT.md</c>) as a <c>list</c> step shows it: its kind, its node, the raw values the engine holds for
/// it in the engine's form, and its name where the name table has one.
/// </summary>
/// <param name="Kind">The kind, such as <c>object</c>; a list orders by it first.</param>
/// <param name="X">The node's column.</param>
/// <param name="Y">The node's row.</param>
/// <param name="Values">The raw values as a field name and its text, in the order a line shows them.</param>
/// <param name="Name">The name from the name table; null where it has none.</param>
public sealed record Thing(string Kind, int X, int Y, IReadOnlyList<(string Field, string Value)> Values, string? Name)
{
    /// <summary>The kind of a map object.</summary>
    public const string ObjectKind = "object";

    /// <summary>The map object as a thing: its code in hex, and its name where <paramref name="names"/> has one.</summary>
    public static Thing Of(MapObject mapObject, NameTable? names) =>
        new(
            ObjectKind,
            mapObject.X,
            mapObject.Y,
            [("code", $"0x{mapObject.Code:x2}")],
            names?.ObjectName(mapObject.Code)
        );

    /// <summary>The line of the thing: <c>&lt;kind&gt; &lt;column&gt;,&lt;row&gt; &lt;field&gt;=&lt;raw&gt; ... &lt;name&gt;</c>, no newline.</summary>
    public string Line()
    {
        var text = new StringBuilder();
        text.Append(Kind).Append(' ').Append(X.ToString(CultureInfo.InvariantCulture)).Append(',').Append(Y);
        foreach (var (field, value) in Values)
        {
            text.Append(' ').Append(field).Append('=').Append(value);
        }

        if (Name is not null)
        {
            text.Append(' ').Append(Name);
        }

        return text.ToString();
    }

    /// <summary>
    /// The text of a <c>.things.txt</c> file: one line for each thing, each ending in a newline, by kind, then row, then
    /// column, and by line text for things on one node; empty for no things.
    /// </summary>
    public static string File(IEnumerable<Thing> things) =>
        string.Concat(
            things
                .OrderBy(t => t.Kind, StringComparer.Ordinal)
                .ThenBy(t => t.Y)
                .ThenBy(t => t.X)
                .ThenBy(t => t.Line(), StringComparer.Ordinal)
                .Select(t => t.Line() + "\n")
        );
}
