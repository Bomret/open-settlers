using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Simulation;

/// <summary>What reading a layout gave: the layout, or the problems that kept it from loading.</summary>
/// <param name="Layout">The layout; null when <paramref name="Problems"/> is not empty.</param>
/// <param name="Problems">One line per problem, each naming the file and the entry; empty when the layout loaded.</param>
public sealed record LayoutResult(Layout? Layout, IReadOnlyList<string> Problems);

/// <summary>
/// A layout: the JSONC file that sets up a game in the game view (ADR 0009, <c>CONTEXT.md</c>). This type holds the
/// hand-drawn map and the seed of the game's random generator.
/// </summary>
public sealed class Layout
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyList<string> _height;
    private readonly IReadOnlyList<string> _terrain;
    private readonly IReadOnlyList<string> _terrainLower;
    private readonly IReadOnlyList<MapObject> _objects;

    private Layout(
        MapSize size,
        string seedText,
        Seed seed,
        IReadOnlyList<string> height,
        IReadOnlyList<string> terrain,
        IReadOnlyList<string> terrainLower,
        IReadOnlyList<MapObject> objects
    )
    {
        Size = size;
        SeedText = seedText;
        Seed = seed;
        _height = height;
        _terrain = terrain;
        _terrainLower = terrainLower;
        _objects = objects;
    }

    /// <summary>The size of the map.</summary>
    public MapSize Size { get; }

    /// <summary>The seed as written in the file, in the free-game screen form.</summary>
    public string SeedText { get; }

    /// <summary>The seed of the game's random generator.</summary>
    public Seed Seed { get; }

    /// <summary>
    /// Reads a layout from JSONC text (comments and trailing commas allowed). <paramref name="file"/> names the
    /// file in each problem.
    /// </summary>
    public static LayoutResult Read(string jsonc, string file = "layout")
    {
        using var document = JsonDocument.Parse(jsonc, Options);
        var root = document.RootElement;
        MapSize.TryFromValue(root.GetProperty("size").GetInt32(), out var size, out _);
        Seed.TryParse(root.GetProperty("seed").GetString(), out var seed, out _);
        var terrain = Rows(root.GetProperty("terrain"));

        return new LayoutResult(
            new Layout(
                size!,
                root.GetProperty("seed").GetString()!,
                seed,
                Rows(root.GetProperty("height")),
                terrain,
                root.TryGetProperty("terrainLower", out var lower) ? Rows(lower) : terrain,
                [
                    .. root.GetProperty("objects")
                        .EnumerateArray()
                        .Select(o => new MapObject(
                            o.GetProperty("x").GetInt32(),
                            o.GetProperty("y").GetInt32(),
                            (byte)o.GetProperty("code").GetInt32()
                        )),
                ]
            ),
            []
        );
    }

    /// <summary>Builds the map the layout draws.</summary>
    public Map BuildMap()
    {
        var map = new Map(Size);
        var height = map.Bytes(MapLayer.Height);
        var terrain = map.Bytes(MapLayer.Terrain);
        for (var y = 0; y < Size.Height; y++)
        {
            for (var x = 0; x < Size.Width; x++)
            {
                height[map.Index(x, y)] = (byte)"0123456789abcdefghijklmnopqrstuv".IndexOf(_height[y][x]);
                terrain[map.Index(x, y)] = (byte)(
                    "wxyzbghijkpmnoqs".IndexOf(_terrain[y][x]) << 4 | "wxyzbghijkpmnoqs".IndexOf(_terrainLower[y][x])
                );
            }
        }

        var objects = map.Bytes(MapLayer.Object);
        foreach (var drawn in _objects)
        {
            objects[map.Index(drawn.X, drawn.Y)] = drawn.Code;
        }

        MarkWaterAdjacent(terrain, objects);
        MarkLakeMiddles(map);

        return map;
    }

    // MapGen_MarkWaterAdjacent (0x4232ef), the same rule as the generator's: bit 7 of the object byte on each tile
    // with a water triangle, a terrain nibble below 4.
    private static void MarkWaterAdjacent(byte[] terrain, byte[] objects)
    {
        for (var tile = 0; tile < terrain.Length; tile++)
        {
            if ((terrain[tile] & 0xc0) == 0 || (terrain[tile] & 0x0c) == 0)
            {
                objects[tile] |= 0x80;
            }
        }
    }

    // MapGen_FillLakes (0x422a0e): the generator marks a lake's middle tiles with flag bit 6 and its edge
    // tiles without. Its heights before the shift are not drawn, so the middle is inferred: a tile at height 0 whose
    // two triangles are water and whose six neighbors, east, south-east, south, west, north-west, and north, are at
    // height 0 too. The edge ring of a drawn lake has a neighbor that is not at height 0 and stays unmarked.
    private static void MarkLakeMiddles(Map map)
    {
        var height = map.Bytes(MapLayer.Height);
        var terrain = map.Bytes(MapLayer.Terrain);
        var flags = map.Bytes(MapLayer.Flags);
        (int Dx, int Dy)[] around = [(1, 0), (1, 1), (0, 1), (-1, 0), (-1, -1), (0, -1)];
        for (var y = 0; y < map.Size.Height; y++)
        {
            for (var x = 0; x < map.Size.Width; x++)
            {
                var tile = map.Index(x, y);
                if (
                    (height[tile] & Map.HeightMask) == 0
                    && terrain[tile] >> 4 < 4
                    && (terrain[tile] & 0xf) < 4
                    && around.All(step => (height[map.Index(x + step.Dx, y + step.Dy)] & Map.HeightMask) == 0)
                )
                {
                    flags[tile] |= 0x40;
                }
            }
        }
    }

    private static string[] Rows(JsonElement grid) => [.. grid.EnumerateArray().Select(row => row.GetString()!)];
}
