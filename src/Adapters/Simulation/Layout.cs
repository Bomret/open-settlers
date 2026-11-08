using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OpenSettlers.Simulation;

/// <summary>What reading a layout gave: the layout, or the problems that kept it from loading.</summary>
/// <param name="Layout">The layout; null when <paramref name="Problems"/> is not empty.</param>
/// <param name="Problems">One line per problem, each naming the file and the entry; empty when the layout loaded.</param>
public sealed record LayoutResult(Layout? Layout, IReadOnlyList<string> Problems);

/// <summary>
/// A layout: the JSONC file that sets up a game in the game view (ADR 0009, <c>CONTEXT.md</c>). This type holds the
/// hand-drawn map and the seed of the game's random generator. The file is one object; each entry is a top-level key
/// and a key this type does not know is refused, so a later entry (the castle, flags, roads, buildings, settlers,
/// goods) is a new key that <see cref="Read"/> learns.
/// <list type="bullet">
/// <item><c>size</c> (required): a map size value the original offers, 1, 2, or 3 (<see cref="MapSize"/>).</item>
/// <item><c>seed</c> (required): the seed of the game's random generator, a string in the free-game screen form.</item>
/// <item><c>height</c> (required): one string for each row of the map, one character for each tile, <c>0</c>-<c>9</c>
/// and <c>a</c>-<c>v</c> for the heights 0 to 31.</item>
/// <item><c>terrain</c> (required): rows like <c>height</c>, one terrain letter for the upper triangle of each tile
/// (<see cref="TerrainLetters"/>).</item>
/// <item><c>terrainLower</c> (optional): the same for the lower triangle; each tile defaults to its upper terrain.</item>
/// <item><c>lakes</c> (optional): rows like <c>height</c>, one character for each tile, <c>.</c> or <c>L</c>; each <c>L</c> sets bit 6 of
/// the Flags layer on its tile, the mark <c>MapGen_FillLakes</c> sets, and the grid then replaces the derived rule
/// (<see cref="BuildMap"/>). A layout with no <c>lakes</c> gets the derived rule.</item>
/// <item><c>objects</c> (optional): a list of <c>{"x": column, "y": row, "code": 1 to 127}</c>, the original's object
/// codes, at most one for each tile.</item>
/// <item><c>castle</c> (optional): <c>{"x", "y"}</c>, the node of the castle; <c>flags</c> and <c>buildings</c>: lists of
/// <c>{"x", "y"}</c>; <c>roads</c>: a list of <c>{"x", "y", "directions"}</c>, the flag the road starts at and one
/// direction, 0 to 5, for each step. <see cref="Apply"/> builds these in the order the file lists them, key by key and
/// entry by entry.</item>
/// <item><c>settlers</c> (optional): entries in an entry state (<c>docs/rules/layouts.md</c>): a woodcutter
/// <c>{"job": "woodcutter", "state": "in-hut", "x", "y"}</c> in the hut at the node, or a carrier
/// <c>{"job": "carrier", "state": "waiting-on-road", "x", "y", "direction"}</c> on the road that leaves the flag at the
/// node in the direction; <c>goods</c> (optional): <c>{"x", "y", "place", "good"}</c>, a good put on a place (0 to 7) of the
/// flag at the node.</item>
/// </list>
/// </summary>
public sealed partial class Layout
{
    /// <summary>
    /// The terrain letters, in the order of the terrain codes 0 to 15 (<c>docs/rules/map-form.md</c>): the letter at
    /// index <c>n</c> draws the terrain code <c>n</c>.
    /// </summary>
    public const string TerrainLetters = "wxyzbghijkpmnoqs";

    /// <summary>The height characters, in the order of the heights 0 to 31.</summary>
    public const string HeightCharacters = "0123456789abcdefghijklmnopqrstuv";

    /// <summary>The characters of the <c>lakes</c> grid: <c>.</c> leaves Flags bit 6 clear, <c>L</c> sets it.</summary>
    public const string LakeCharacters = ".L";

    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string[] _height;
    private readonly string[] _terrain;
    private readonly string[] _terrainLower;
    private readonly string[]? _lakes;
    private readonly IReadOnlyList<PlacedObject> _objects;
    private readonly IReadOnlyList<Step> _steps;
    private readonly string _file;

    private Layout(
        string file,
        MapSize size,
        Seed seed,
        string[] height,
        string[] terrain,
        string[] terrainLower,
        string[]? lakes,
        IReadOnlyList<PlacedObject> objects,
        IReadOnlyList<Step> steps
    )
    {
        _file = file;
        _steps = steps;
        Size = size;
        Seed = seed;
        _height = height;
        _terrain = terrain;
        _terrainLower = terrainLower;
        _lakes = lakes;
        _objects = objects;
    }

    /// <summary>The size of the map.</summary>
    public MapSize Size { get; }

    /// <summary>The seed of the game's random generator.</summary>
    public Seed Seed { get; }

    /// <summary>
    /// Reads a layout from JSONC text (comments and trailing commas allowed). <paramref name="file"/> names the
    /// file in each problem; each problem line is <c>file: entry: reason</c>. A layout with any problem loads
    /// nothing. Never throws for the text it is given.
    /// </summary>
    public static LayoutResult Read(string jsonc, string file = "layout")
    {
        var reader = new Reader(file);
        var layout = reader.Read(jsonc);

        return layout is not null && reader.Problems.Count == 0
            ? new LayoutResult(layout, [])
            : new LayoutResult(null, reader.Problems);
    }

    /// <summary>
    /// Reads the layout file at <paramref name="path"/> like <see cref="Read"/>. A file that is missing or cannot be
    /// read is a problem naming the path.
    /// </summary>
    public static LayoutResult ReadFile(string path)
    {
        try
        {
            return Read(File.ReadAllText(path), path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new LayoutResult(null, [$"{path}: file: cannot be read: {e.Message}"]);
        }
    }

    /// <summary>
    /// Builds the map the layout draws, with the random generator at the state the map generator leaves it in for
    /// the layout's seed (<c>MapGen_Generate</c> loads the seed words and steps twice). The Height, Terrain, and
    /// Object layers hold what the layout draws, and these bytes are derived from them as the generator derives
    /// them:
    /// <list type="bullet">
    /// <item>bit 7 of the Object layer on each tile with a water triangle (<c>MapGen_MarkWaterAdjacent</c>, the
    /// generator's own rule, <see cref="MapGenerator.MarkWaterAdjacentTiles"/>);</item>
    /// <item>bit 6 of the Flags layer (<c>MapGen_FillLakes</c>): where the layout draws a <c>lakes</c> grid, on each tile the
    /// grid marks and on no other, so a layout matches the generator tile for tile; where it draws none, by the rule
    /// <see cref="MarkLakeMiddles"/> states, a superset of the generator's marks: every middle the generator marks is
    /// marked, and so is each water tile at height 0 whose six neighbors are at height 0 that the generator leaves
    /// unmarked.</item>
    /// </list>
    /// Not derived: the lake detail byte stays 0 (the generator draws it from the random generator, and a drawn
    /// map keeps the game's draws its own), and bit 6 of the Flags layer for a large object
    /// (<c>MapGen_ResolveObjectOverlap</c>), which needs the program's class table and which no rule reads yet; a
    /// <c>lakes</c> grid can mark it by hand.
    /// </summary>
    public Map BuildMap()
    {
        var map = new MapLayers(Size);
        var seed = Seed.ToMapSeed();
        map.Generator = new RandomGenerator(seed.W0, seed.W1, seed.W2).Step().Step();
        var height = map.Bytes(MapLayer.Height);
        var terrain = map.Bytes(MapLayer.Terrain);
        var objects = map.Bytes(MapLayer.Object);
        for (var y = 0; y < Size.Height; y++)
        {
            for (var x = 0; x < Size.Width; x++)
            {
                var tile = map.Index(x, y);
                height[tile] = (byte)HeightCharacters.IndexOf(_height[y][x]);
                terrain[tile] = (byte)(
                    TerrainLetters.IndexOf(_terrain[y][x]) << 4 | TerrainLetters.IndexOf(_terrainLower[y][x])
                );
            }
        }

        foreach (var drawn in _objects)
        {
            objects[map.Index(drawn.X, drawn.Y)] = drawn.Code;
        }

        MapGenerator.MarkWaterAdjacentTiles(terrain, objects);
        if (_lakes is null)
        {
            MarkLakeMiddles(map);
        }
        else
        {
            var flags = map.Bytes(MapLayer.Flags);
            for (var y = 0; y < Size.Height; y++)
            {
                for (var x = 0; x < Size.Width; x++)
                {
                    if (_lakes[y][x] == 'L')
                    {
                        flags[map.Index(x, y)] |= 0x40;
                    }
                }
            }
        }

        return MapImporter.Import(map);
    }

    // MapGen_FillLakes (0x422a0e): the generator marks the tiles of a lake (those it reaches by growing from a tile
    // whose six neighbors lie at or below the water level) with flag bit 6 and the ring around them without, and the
    // lake tiles end at height 0 with two water triangles. The state the generator marks from is not drawn: the ring
    // ends at height 0 as well, and MapGen_KeepMainLand drops land to height 0 without a mark. So the rule reads the
    // drawn bytes: a tile at height 0 whose two triangles are water and whose six neighbors, east, south-east, south,
    // west, north-west, and north, are at height 0 too. Against generated maps (LayoutTests) this marks every tile
    // the generator marks, and also tiles at height 0 that it leaves unmarked; a layout that draws the marks in its
    // lakes grid has no need of the rule.
    private static void MarkLakeMiddles(MapLayers map)
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
                    (height[tile] & 0x1f) == 0
                    && terrain[tile] >> 4 < 4
                    && (terrain[tile] & 0xf) < 4
                    && around.All(step => (height[map.Index(x + step.Dx, y + step.Dy)] & 0x1f) == 0)
                )
                {
                    flags[tile] |= 0x40;
                }
            }
        }
    }

    // Reads one file's text and collects every problem it can find; entries that depend on a bad entry are skipped.
    private sealed partial class Reader(string file)
    {
        private readonly List<string> _problems = [];

        public IReadOnlyList<string> Problems => _problems;

        public Layout? Read(string jsonc)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(jsonc, Options);
            }
            catch (JsonException e)
            {
                Fail("syntax", e.Message);

                return null;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    Fail("layout", "the file must hold one object");

                    return null;
                }

                var entries = new Dictionary<string, JsonElement>();
                foreach (var property in root.EnumerateObject())
                {
                    if (!entries.TryAdd(property.Name, property.Value))
                    {
                        Fail(property.Name, "given twice");
                    }
                    else if (
                        property.Name
                        is not (
                            "size"
                            or "seed"
                            or "height"
                            or "terrain"
                            or "terrainLower"
                            or "lakes"
                            or "objects"
                            or "castle"
                            or "flags"
                            or "roads"
                            or "buildings"
                            or "settlers"
                            or "goods"
                        )
                    )
                    {
                        Fail(property.Name, "unknown entry");
                    }
                }

                foreach (
                    var name in new[] { "size", "seed", "height", "terrain" }.Where(name => !entries.ContainsKey(name))
                )
                {
                    Fail(name, "missing");
                }

                var size = entries.TryGetValue("size", out var sizeEntry) ? ReadSize(sizeEntry) : null;
                var seed = entries.TryGetValue("seed", out var seedEntry) ? ReadSeed(seedEntry) : null;
                var height = entries.TryGetValue("height", out var heightEntry)
                    ? ReadGrid("height", heightEntry, size, HeightCharacters, "a height character, 0-9 or a-v")
                    : null;
                var terrain = ReadTerrain("terrain");
                var lower = entries.ContainsKey("terrainLower") ? ReadTerrain("terrainLower") : terrain;
                var lakes = entries.TryGetValue("lakes", out var lakesEntry)
                    ? ReadGrid("lakes", lakesEntry, size, LakeCharacters, "a lake character, . or L")
                    : null;
                var objects = entries.TryGetValue("objects", out var objectsEntry)
                    ? ReadObjects(objectsEntry, size)
                    : [];
                var steps = ReadSteps(root);

                return
                    size is null
                    || seed is null
                    || height is null
                    || terrain is null
                    || lower is null
                    || (lakes is null && entries.ContainsKey("lakes"))
                    || objects is null
                    || steps is null
                    ? null
                    : new Layout(file, size, seed.Value, height, terrain, lower, lakes, objects, steps);

                string[]? ReadTerrain(string name) =>
                    entries.TryGetValue(name, out var entry)
                        ? ReadGrid(name, entry, size, TerrainLetters, $"a terrain letter, one of {TerrainLetters}")
                        : null;
            }
        }

        private void Fail(string entry, string reason) => _problems.Add($"{file}: {entry}: {reason}");

        private MapSize? ReadSize(JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetInt32(out var value))
            {
                Fail("size", "must be a whole number, 1, 2, or 3");

                return null;
            }

            if (!MapSize.TryFromValue(value, out var size, out var problem))
            {
                Fail("size", problem);
            }

            return size;
        }

        private Seed? ReadSeed(JsonElement entry)
        {
            var text = entry.ValueKind == JsonValueKind.String ? entry.GetString() : null;
            if (text is null)
            {
                Fail("seed", "must be a string of 16 digits from 1 to 8");

                return null;
            }

            if (!Simulation.Seed.TryParse(text, out var seed, out var problem))
            {
                Fail("seed", problem);

                return null;
            }

            return seed;
        }

        // A grid of one string for each row; the shape is checked against the size where the size is good.
        private string[]? ReadGrid(string name, JsonElement entry, MapSize? size, string alphabet, string expected)
        {
            if (
                entry.ValueKind != JsonValueKind.Array
                || entry.EnumerateArray().Any(r => r.ValueKind != JsonValueKind.String)
            )
            {
                Fail(name, "must be a list of strings, one for each row");

                return null;
            }

            var rows = entry.EnumerateArray().Select(row => row.GetString()!).ToArray();
            var good = true;
            if (size is not null && rows.Length != size.Height)
            {
                Fail(name, $"has {rows.Length} rows, the map has {size.Height}");
                good = false;
            }

            for (var y = 0; y < rows.Length; y++)
            {
                if (size is not null && rows[y].Length != size.Width)
                {
                    Fail($"{name}[{y}]", $"has {rows[y].Length} characters, the map is {size.Width} wide");
                    good = false;
                }
                else if (rows[y].FirstOrDefault(c => !alphabet.Contains(c)) is var bad and not '\0')
                {
                    Fail($"{name}[{y}]", $"column {rows[y].IndexOf(bad)}: '{bad}' is not {expected}");
                    good = false;
                }
            }

            return good ? rows : null;
        }

        private List<PlacedObject>? ReadObjects(JsonElement entry, MapSize? size)
        {
            if (entry.ValueKind != JsonValueKind.Array)
            {
                Fail("objects", "must be a list of {\"x\", \"y\", \"code\"}");

                return null;
            }

            var objects = new List<PlacedObject>();
            var good = true;
            var index = -1;
            foreach (var drawn in entry.EnumerateArray())
            {
                index++;
                var name = $"objects[{index}]";
                if (drawn.ValueKind != JsonValueKind.Object)
                {
                    Fail(name, "must be {\"x\", \"y\", \"code\"}");
                    good = false;

                    continue;
                }

                var unknown = drawn
                    .EnumerateObject()
                    .Select(p => p.Name)
                    .FirstOrDefault(n => n is not ("x" or "y" or "code"));
                if (unknown is not null)
                {
                    Fail(name, $"unknown key {unknown}, an object has x, y, and code");
                    good = false;

                    continue;
                }

                if (
                    !Whole(drawn, "x", out var x)
                    || !Whole(drawn, "y", out var y)
                    || !Whole(drawn, "code", out var code)
                )
                {
                    Fail(name, "x, y, and code must each be a whole number");
                    good = false;

                    continue;
                }

                if (code is < 1 or > 127)
                {
                    Fail(name, $"the object code {code} is not from 1 to 127");
                    good = false;
                }
                else if (size is not null && (x < 0 || x >= size.Width || y < 0 || y >= size.Height))
                {
                    Fail(name, $"the tile {x},{y} lies outside the {size.Width} by {size.Height} map");
                    good = false;
                }
                else if (objects.Any(o => o.X == x && o.Y == y))
                {
                    Fail(name, $"the tile {x},{y} already holds an object");
                    good = false;
                }
                else
                {
                    objects.Add(new PlacedObject(x, y, new MapObject((byte)code)));
                }
            }

            return good ? objects : null;
        }

        private static bool Whole(JsonElement drawn, string key, out int value)
        {
            value = 0;

            return drawn.TryGetProperty(key, out var element)
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out value);
        }
    }
}
