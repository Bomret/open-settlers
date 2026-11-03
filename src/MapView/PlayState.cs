using System;
using System.Globalization;
using System.Text;

namespace OpenSettlers.MapView;

/// <summary>
/// What a play run can observe of the map view: the camera's wrapped integer position, the zoom scale, and the seed
/// and size last applied. A <c>dump</c> step writes it and an <c>expect</c> step checks one field of it.
/// </summary>
/// <param name="CameraX">The camera's west-east position in map pixels.</param>
/// <param name="CameraY">The camera's north-south position in map pixels.</param>
/// <param name="Zoom">The zoom scale: screen pixels per map pixel.</param>
/// <param name="Seed">The seed as applied, in the form it was given.</param>
/// <param name="Size">The size value of the map.</param>
public sealed record PlayState(int CameraX, int CameraY, float Zoom, string Seed, int Size)
{
    /// <summary>The decimals zoom is written with and compared at.</summary>
    public const int ZoomDecimals = 4;

    /// <summary>The state as one <c>field=value</c> line per <see cref="PlayStep"/> field, each ending in a newline.</summary>
    public string Dump()
    {
        var text = new StringBuilder();
        foreach (var field in PlayFields.All)
        {
            text.Append(field).Append('=').Append(Text(field)).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Checks an <c>expect</c> step; null when it holds, else the problem with expected and actual.</summary>
    public string? Check(ExpectStep expect)
    {
        var holds = PlayFields.IsNumeric(expect.Field)
            ? Compare(Number(expect.Field), double.Parse(expect.Value, CultureInfo.InvariantCulture), expect)
            : Text(expect.Field) == expect.Value;

        return holds ? null : $"expect {expect.Field} {expect.Op} {expect.Value} failed: actual {Text(expect.Field)}";
    }

    double Number(string field) =>
        field switch
        {
            PlayFields.CameraX => CameraX,
            PlayFields.CameraY => CameraY,
            PlayFields.Zoom => Math.Round(Zoom, ZoomDecimals),
            _ => Size,
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

        return expect.Op switch
        {
            "<" => actual < wanted,
            "<=" => actual <= wanted,
            ">=" => actual >= wanted,
            ">" => actual > wanted,
            _ => actual == wanted,
        };
    }
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

    /// <summary>Every field, in the order a dump lists them.</summary>
    public static readonly string[] All = [CameraX, CameraY, Zoom, Seed, Size];

    /// <summary>Whether the field is a number; the seed is text and takes only <c>=</c>.</summary>
    public static bool IsNumeric(string field) => field != Seed;
}
