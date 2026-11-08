using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenSettlers.Simulation;

/// <summary>
/// Seed info (<c>CONTEXT.md</c>): the text report of the map of one seed and size. The report has a fixed order and
/// no clock, path, or locale in it, so it is the same bytes on every run. It holds the seed and size as given, the
/// FNV-1a digest of each layer in <see cref="MapLayer"/> order, the tile count per terrain type (both triangles of
/// each tile, ascending type), the count per object code, and the count and amount total per resource type.
/// </summary>
public static class SeedInfo
{
    /// <summary>The process exit code of a seed whose map cannot be generated.</summary>
    public const int NoMapExitCode = 1;

    /// <summary>The process exit code where the program's rule tables cannot be read: the missing-install code of the other modes.</summary>
    public const int NoTablesExitCode = 2;

    /// <summary>Builds the report of a map. <c>seedText</c> is the seed as the user gave it.</summary>
    public static string Report(string seedText, Map map)
    {
        var text = new StringBuilder();
        text.Append($"Seed: {seedText}\n");
        text.Append($"Size: {map.Size.Value} ({map.Size.Width} x {map.Size.Height} tiles)\n");
        text.Append("Layer digests (FNV-1a 64, row-major):\n");
        var layers = MapLayerExporter.Export(map);
        foreach (var layer in Enum.GetValues<MapLayer>())
        {
            var note = layer == MapLayer.Resource ? " (same bytes as Lake, so the digests match)" : "";
            text.Append($"  {layer}: {layers.Digest(layer):x16}{note}\n");
        }

        Section(
            text,
            "Terrain tiles (both triangles of each tile, by type):",
            Enumerable
                .Range(0, map.Count)
                .SelectMany(index => new[] { map.At(index).Upper.Code, map.At(index).Lower.Code })
                .GroupBy(type => type),
            group => $"  {group.Key}: {group.Count()}"
        );
        Section(
            text,
            "Objects (by code):",
            map.Objects.GroupBy(o => (int)o.Code),
            group => $"  {group.Key}: {group.Count()}"
        );
        Section(
            text,
            "Deposits (by resource type):",
            map.Deposits.GroupBy(d => d.Type),
            group => $"  {group.Key}: {group.Count()} tiles, amount {group.Sum(d => d.Amount)}"
        );

        return text.ToString();
    }

    /// <summary>
    /// Reads the program's rule tables, generates the map of a seed and size, and reports it. Returns 0 with the report
    /// in <c>text</c>; <see cref="NoTablesExitCode"/> with the problem, and no map, where <c>readTables</c> fails with
    /// an <see cref="IOException"/>, an <see cref="InvalidDataException"/>, or an
    /// <see cref="UnauthorizedAccessException"/> (no install, or an unreadable program); or
    /// <see cref="NoMapExitCode"/> with a message where the generator refuses the seed (no land mass) or fails in any
    /// other way, so the run always ends with a code. <c>generate</c> replaces the generator; null uses
    /// <see cref="MapGenerator.Generate"/>.
    /// </summary>
    public static int Run(
        Func<ProgramTables> readTables,
        string seedText,
        Seed seed,
        MapSize size,
        out string text,
        Func<MapSeed, MapSize, Map>? generate = null
    )
    {
        ProgramTables tables;
        try
        {
            tables = readTables();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            text = e.Message;

            return NoTablesExitCode;
        }

        try
        {
            text = Report(
                seedText,
                (generate ?? ((s, z) => MapGenerator.Generate(tables, s, z)))(seed.ToMapSeed(), size)
            );

            return 0;
        }
        catch (Exception e)
        {
            text = $"no map for this seed: {e.Message}";

            return NoMapExitCode;
        }
    }

    static void Section<T>(
        StringBuilder text,
        string heading,
        IEnumerable<IGrouping<int, T>> groups,
        Func<IGrouping<int, T>, string> line
    )
    {
        text.Append(heading).Append('\n');
        foreach (var group in groups.OrderBy(g => g.Key))
        {
            text.Append(line(group)).Append('\n');
        }
    }
}
