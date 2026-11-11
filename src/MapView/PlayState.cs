using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// What a play run can observe of the map view: the camera's wrapped integer position, the zoom scale, and the seed
/// and size last applied. In the game view it also holds the game's tick count, the castle's log count, and the count of
/// goods on each flag; its dump also gives the game's digest. A <c>dump</c> step writes it and an <c>expect</c> step checks
/// one field of it.
/// </summary>
/// <param name="CameraX">The camera's west-east position in map pixels.</param>
/// <param name="CameraY">The camera's north-south position in map pixels.</param>
/// <param name="Zoom">The zoom scale: screen pixels per map pixel.</param>
/// <param name="Seed">The seed as applied, in the form it was given.</param>
/// <param name="Size">The size value of the map.</param>
/// <param name="Ticks">The game's tick count; null in the map view.</param>
/// <param name="Logs">The castle's log count, 0 before a castle stands; null in the map view.</param>
/// <param name="FlagGoods">
/// The count of goods on each flag that holds some, by the flag's node; a flag left out holds none. Null in the map view.
/// </param>
/// <param name="Things">The things a <c>list</c> step writes; null lists none.</param>
/// <param name="Game">The running game the named-thing steps read events from; null in the map view.</param>
/// <param name="Planks">The castle's plank count, 0 before a castle stands; null in the map view.</param>
/// <param name="Stones">The castle's stone count, 0 before a castle stands; null in the map view.</param>
public sealed record PlayState(
    int CameraX,
    int CameraY,
    float Zoom,
    string Seed,
    int Size,
    long? Ticks = null,
    int? Logs = null,
    IReadOnlyDictionary<(int X, int Y), int>? FlagGoods = null,
    IReadOnlyList<Thing>? Things = null,
    Game? Game = null,
    int? Planks = null,
    int? Stones = null
)
{
    /// <summary>The decimals zoom is written with and compared at.</summary>
    public const int ZoomDecimals = 4;

    /// <summary>
    /// The state as one <c>field=value</c> line per field, each ending in a newline: <see cref="PlayFields.All"/>, then
    /// in the game view <see cref="PlayFields.Game"/>, the <see cref="PlayFields.Digest"/> where a game runs, and a <see cref="PlayFields.FlagGoods"/> line for each flag that holds
    /// goods, by row and then column.
    /// </summary>
    public string Dump()
    {
        var text = new StringBuilder();
        foreach (var field in Ticks is null ? PlayFields.All : [.. PlayFields.All, .. PlayFields.Game])
        {
            text.Append(field).Append('=').Append(Text(field)).Append('\n');
        }

        if (Game is not null)
        {
            text.Append(PlayFields.Digest)
                .Append('=')
                .Append(Game.Digest().ToString("x16", CultureInfo.InvariantCulture))
                .Append('\n');
        }

        foreach (
            var ((x, y), count) in (FlagGoods ?? new Dictionary<(int, int), int>())
                .OrderBy(e => e.Key.Y)
                .ThenBy(e => e.Key.X)
        )
        {
            text.Append(PlayFields.FlagGoodsAt(x, y)).Append('=').Append(count).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Checks an <c>expect</c> step; null when it holds, else the problem with expected and actual. A game field in the
    /// map view is a problem.
    /// </summary>
    public string? Check(ExpectStep expect)
    {
        if (Ticks is null && PlayFields.IsGame(expect.Field))
            return $"expect {expect.Field}: the map view has no {expect.Field}, use --game-view";

        var holds = PlayFields.IsNumeric(expect.Field)
            ? Compare(Number(expect.Field), double.Parse(expect.Value, CultureInfo.InvariantCulture), expect)
            : Text(expect.Field) == expect.Value;

        return holds ? null : $"expect {expect.Field} {expect.Op} {expect.Value} failed: actual {Text(expect.Field)}";
    }

    /// <summary>
    /// Checks an <c>expect-at</c> step; null when a thing at the node matches, else the problem. A node outside the map
    /// and a kind the view cannot list are problems; a node that holds no match states what it holds.
    /// </summary>
    public string? Check(ExpectAtStep expect)
    {
        var step = $"expect-at {expect.Column} {expect.Row} {expect.What.Text}";
        if (Unlisted(expect.What) is { } unlisted)
            return $"{step}: {unlisted}";
        if (!MapSize.TryFromValue(Size, out var map, out _))
            return $"{step}: the map size {Size} is not one the original offers";
        if (expect.Column >= map.Width || expect.Row >= map.Height)
            return $"{step}: the node is outside the {map.Width} by {map.Height} map";

        var here = (Things ?? []).Where(t => t.X == expect.Column && t.Y == expect.Row).ToList();

        return here.Any(expect.What.Matches)
            ? null
            : $"{step} failed: the node holds {(here.Count == 0 ? "nothing" : string.Join("; ", here.Select(t => t.Line())))}";
    }

    /// <summary>
    /// Checks an <c>expect-count</c> step; null when the count of matching things holds, else the problem with the
    /// actual count. A kind the view cannot list is a problem.
    /// </summary>
    public string? Check(ExpectCountStep expect)
    {
        var step = $"expect-count {expect.What.Text} {expect.Op} {expect.Count}";
        if (Unlisted(expect.What) is { } unlisted)
            return $"{step}: {unlisted}";

        var actual = (Things ?? []).Count(expect.What.Matches);

        return Compare(actual, expect.Count, expect.Op) ? null : $"{step} failed: actual {actual}";
    }

    // The map view lists map objects only, so a count of any other kind would pass for the wrong reason.
    string? Unlisted(ThingSelector what) =>
        Ticks is null && what.UnlistedInMapView is { } kind
            ? $"the map view cannot list {kind}, use --game-view"
            : null;

    double Number(string field) =>
        field switch
        {
            PlayFields.CameraX => CameraX,
            PlayFields.CameraY => CameraY,
            PlayFields.Zoom => Math.Round(Zoom, ZoomDecimals),
            PlayFields.Size => Size,
            PlayFields.Ticks => Ticks ?? throw new InvalidOperationException("the map view has no ticks"),
            PlayFields.Logs => Logs ?? throw new InvalidOperationException("the map view has no logs"),
            PlayFields.Planks => Planks ?? throw new InvalidOperationException("the map view has no planks"),
            PlayFields.Stones => Stones ?? throw new InvalidOperationException("the map view has no stones"),
            _ when PlayFields.FlagNode(field) is { } node => FlagGoods?.GetValueOrDefault(node)
                ?? throw new InvalidOperationException("the map view has no flag goods"),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "no numeric play field"),
        };

    string Text(string field) =>
        field switch
        {
            PlayFields.Seed => Seed,
            PlayFields.Zoom => Zoom.ToString($"F{ZoomDecimals}", CultureInfo.InvariantCulture),
            _ => Number(field).ToString(CultureInfo.InvariantCulture),
        };

    static bool Compare(double actual, double wanted, ExpectStep expect)
    {
        if (expect.Field == PlayFields.Zoom)
            wanted = Math.Round(wanted, ZoomDecimals);

        return Compare(actual, wanted, expect.Op);
    }

    internal static bool Compare(double actual, double wanted, string op) =>
        op switch
        {
            "<" => actual < wanted,
            "<=" => actual <= wanted,
            ">=" => actual >= wanted,
            ">" => actual > wanted,
            _ => actual == wanted,
        };
}

/// <summary>The fields of a <see cref="PlayState"/>, as <c>dump</c> and <c>expect</c> name them.</summary>
public static class PlayFields
{
    /// <summary>The camera's x.</summary>
    public const string CameraX = "camera-x";

    /// <summary>The camera's y.</summary>
    public const string CameraY = "camera-y";

    /// <summary>The zoom scale.</summary>
    public const string Zoom = "zoom";

    /// <summary>The seed.</summary>
    public const string Seed = "seed";

    /// <summary>The size value.</summary>
    public const string Size = "size";

    /// <summary>The game's tick count, in the game view only.</summary>
    public const string Ticks = "ticks";

    /// <summary>The castle's log count, in the game view only.</summary>
    public const string Logs = "logs";

    /// <summary>The castle's plank count, in the game view only.</summary>
    public const string Planks = "planks";

    /// <summary>The castle's stone count, in the game view only.</summary>
    public const string Stones = "stones";

    /// <summary>
    /// The game's digest (<c>Game.Digest()</c>) as 16 hex digits, in the game view's dump only after <see cref="Stones"/>. A
    /// dump only: no <c>expect</c> step names it.
    /// </summary>
    public const string Digest = "digest";

    /// <summary>Every field of both views, in the order a dump lists them.</summary>
    public static readonly string[] All = [CameraX, CameraY, Zoom, Seed, Size];

    /// <summary>The fields the game view dumps after <see cref="All"/>.</summary>
    public static readonly string[] Game = [Ticks, Logs, Planks, Stones];

    /// <summary>
    /// The start of the field of a flag's goods, in the game view only: <c>flag-goods:&lt;column&gt;,&lt;row&gt;</c> is the
    /// count of goods on the flag at that node, 0 where none lie there.
    /// </summary>
    public const string FlagGoods = "flag-goods:";

    /// <summary>The field of the goods on the flag at the node.</summary>
    public static string FlagGoodsAt(int x, int y) => $"{FlagGoods}{x},{y}";

    /// <summary>The node a <see cref="FlagGoods"/> field names; null for any other field.</summary>
    public static (int X, int Y)? FlagNode(string field)
    {
        if (!field.StartsWith(FlagGoods, StringComparison.Ordinal))
            return null;

        var parts = field[FlagGoods.Length..].Split(',');
        return
            parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var x)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var y)
            ? (x, y)
            : null;
    }

    /// <summary>Whether an <c>expect</c> step can name the field.</summary>
    public static bool IsKnown(string field) => All.Contains(field) || IsGame(field);

    /// <summary>Whether the field belongs to the game view only.</summary>
    public static bool IsGame(string field) => Game.Contains(field) || FlagNode(field) is not null;

    /// <summary>Whether the field is a number; the seed is text and takes only <c>=</c>.</summary>
    public static bool IsNumeric(string field) => field != Seed;
}
