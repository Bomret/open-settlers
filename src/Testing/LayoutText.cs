using System.Linq;

namespace OpenSettlers.Testing;

/// <summary>The text of a layout file (<c>docs/rules/layouts.md</c>) that tests read, built from the parts a test changes.</summary>
public static class LayoutText
{
    /// <summary>The seed of the drawn map: the three words as hexadecimal digits.</summary>
    public const string Seed = "1234567812345678";

    /// <summary>A grid of 32 rows with one row replaced; rows[y] is the replacement text of row y.</summary>
    public static string Grid(char fill, params (int Row, string Text)[] rows) =>
        "["
        + string.Join(
            ", ",
            Enumerable
                .Range(0, 32)
                .Select(y => $"\"{rows.FirstOrDefault(r => r.Row == y).Text ?? new string(fill, 32)}\"")
        )
        + "]";

    /// <summary>A row of 32 characters of <paramref name="fill"/> with the one at <paramref name="at"/> replaced.</summary>
    public static string Row(char fill, int at, char with) => new string(fill, at) + with + new string(fill, 31 - at);

    /// <summary>A layout text of a drawn map; each part a test does not give is a flat grass map.</summary>
    public static string Text(
        string size = "1",
        string? height = null,
        string? terrain = null,
        string? lower = null,
        string? lakes = null,
        string objects = "[]",
        string seed = $"\"{Seed}\"",
        string extra = ""
    ) =>
        $$"""
            // a drawn map
            {
              "size": {{size}},
              "seed": {{seed}},
              "height": {{height ?? Grid('1')}},
              "terrain": {{terrain ?? Grid('g')}},
              {{(lower is null ? "" : $"\"terrainLower\": {lower},")}}
              {{(lakes is null ? "" : $"\"lakes\": {lakes},")}}
              "objects": {{objects}},{{extra}}
            }
            """;
}
