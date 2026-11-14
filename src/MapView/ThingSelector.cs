using System;
using System.Globalization;
using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// The <c>&lt;what&gt;</c> of an <c>expect-at</c> or <c>expect-count</c> step: a kind (<c>object</c>, <c>settler</c>,
/// <c>building</c>, <c>flag</c>, <c>road</c>), a name from the name table, or <c>object:0xNN</c> for one raw map
/// object code. A name several codes share matches a thing of any of them.
/// </summary>
public sealed record ThingSelector
{
    /// <summary>The selector as written.</summary>
    public required string Text { get; init; }

    /// <summary>The raw object code of <c>object:0xNN</c>; null for a kind or a name.</summary>
    public required int? RawCode { get; init; }

    /// <summary>The kinds a game view lists; the map view lists <see cref="Thing.ObjectKind"/> only.</summary>
    static readonly string[] Kinds = [Thing.ObjectKind, "settler", "building", "flag", "road"];

    const string RawPrefix = "object:0x";

    /// <summary>Reads <paramref name="text"/>; the selector, or the problem when it is no kind, name, or raw code.</summary>
    public static (ThingSelector? Selector, string? Problem) Parse(string text)
    {
        if (text.StartsWith("object:", StringComparison.Ordinal))
        {
            var digits = text.StartsWith(RawPrefix, StringComparison.Ordinal) ? text[RawPrefix.Length..] : "";
            if (digits.Length == 0 || !digits.All(Uri.IsHexDigit))
            {
                return (
                    null,
                    $"'{text}' is a malformed raw code, use object:0x followed by hex digits, such as object:0x0a"
                );
            }

            if (
                !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
                || code > 0xff
            )
            {
                return (null, $"'{text}' is over 0xff");
            }

            return (new ThingSelector { Text = text, RawCode = code }, null);
        }

        return Array.IndexOf(Kinds, text) >= 0 || NameTable.IsName(text)
            ? (
                new ThingSelector
                {
                    Text = text,
                    RawCode = null,
                    UnlistedInMapView = UnlistedKind(text),
                },
                null
            )
            : (
                null,
                $"unknown name '{text}', use a kind ({string.Join(", ", Kinds)}), a name of the name table, or object:0xNN"
            );
    }

    /// <summary>
    /// The kind or name the selector names that a map view cannot list: a kind but <c>object</c>, or a name that is no
    /// object name (a job, state, or building type); null for any other selector.
    /// </summary>
    public string? UnlistedInMapView { get; private init; }

    static string? UnlistedKind(string text) =>
        text != Thing.ObjectKind && (Array.IndexOf(Kinds, text) >= 0 || !NameTable.IsObjectName(text)) ? text : null;

    /// <summary>
    /// Whether <paramref name="thing"/> is what the selector names: its kind, or any one of its names (a settler's name
    /// is its job name and its state name, joined by a space).
    /// </summary>
    public bool Matches(Thing thing)
    {
        if (RawCode is { } code)
        {
            return thing.Kind == Thing.ObjectKind
                && thing.Values.Any(v =>
                    v.Field == "code"
                    && v.Value.StartsWith("0x", StringComparison.Ordinal)
                    && int.TryParse(
                        v.Value.AsSpan(2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var raw
                    )
                    && raw == code
                );
        }

        return thing.Kind == Text || (thing.Name?.Split(' ').Contains(Text) ?? false);
    }
}
