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
/// <param name="Text">The selector as written.</param>
/// <param name="RawCode">The raw object code of <c>object:0xNN</c>; null for a kind or a name.</param>
public sealed record ThingSelector(string Text, int? RawCode)
{
    /// <summary>The kinds a game view lists; the map view lists <see cref="Thing.ObjectKind"/> only.</summary>
    static readonly string[] Kinds = [Thing.ObjectKind, "settler", "building", "flag", "road"];

    const string RawPrefix = "object:0x";

    /// <summary>Reads <paramref name="text"/>; the selector, or the problem when it is no kind, name, or raw code.</summary>
    public static (ThingSelector? Selector, string? Problem) Parse(string text)
    {
        if (text.StartsWith("object:", StringComparison.Ordinal))
        {
            var digits = text.StartsWith(RawPrefix, StringComparison.Ordinal) ? text[RawPrefix.Length..] : "";
            if (digits.Length == 0 || !Array.TrueForAll(digits.ToCharArray(), Uri.IsHexDigit))
                return (
                    null,
                    $"'{text}' is a malformed raw code, use object:0x followed by hex digits, such as object:0x0a"
                );
            if (
                !int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code)
                || code > 0xff
            )
                return (null, $"'{text}' is over 0xff");

            return (new ThingSelector(text, code), null);
        }

        return Array.IndexOf(Kinds, text) >= 0 || NameTable.IsName(text)
            ? (new ThingSelector(text, null), null)
            : (
                null,
                $"unknown name '{text}', use a kind ({string.Join(", ", Kinds)}), a name of the name table, or object:0xNN"
            );
    }

    /// <summary>The kind the selector names that a map view cannot list; null for any other selector.</summary>
    public string? UnlistedInMapView =>
        RawCode is null && Text != Thing.ObjectKind && Array.IndexOf(Kinds, Text) >= 0 ? Text : null;

    /// <summary>Whether <paramref name="thing"/> is what the selector names.</summary>
    public bool Matches(Thing thing)
    {
        if (RawCode is { } code)
        {
            return thing.Kind == Thing.ObjectKind
                && thing.Values.Any(v =>
                    v.Field == "code"
                    && int.TryParse(
                        v.Value.AsSpan(2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var raw
                    )
                    && raw == code
                );
        }

        return thing.Kind == Text || thing.Name == Text;
    }
}
