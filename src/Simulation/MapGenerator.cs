using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>Called after the seeding and after each pass of the generation with the map and generator as they stand.</summary>
public delegate void MapPassObserver(MapPass pass, Map map, RandomGenerator generator);

/// <summary>
/// The original's map generation, <c>MapGen_Generate</c> (<c>0x4216e9</c>), as far as the height and terrain
/// passes <c>MapGen_ClearMap</c> to <c>MapGen_ScaleHeights</c> (<c>docs/rules/map-form.md</c>). The generation is
/// deterministic: it uses integers only, visits tiles in a fixed order, and reads no clock (INV-0002).
/// </summary>
public static class MapGenerator
{
    /// <summary>
    /// Generates the map of a seed and size, reporting to the observer after the seeding and after each pass. The
    /// class table of <paramref name="tables"/> decides which objects are large.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="tables"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// No land mass covers a quarter of the map. The original searches for one without end.
    /// </exception>
    public static Map Generate(ProgramTables tables, MapSeed seed, MapSize size, MapPassObserver? observer = null)
    {
        ArgumentNullException.ThrowIfNull(tables);

        return new Generation(tables, seed, size, observer).Run();
    }

    /// <summary>
    /// Generates like <see cref="Generate"/> for a size value. Returns false with the reason, and generates
    /// nothing, when the original offers no such size.
    /// </summary>
    public static bool TryGenerate(
        ProgramTables tables,
        MapSeed seed,
        int sizeValue,
        MapPassObserver? observer,
        [NotNullWhen(true)] out Map? map,
        [NotNullWhen(false)] out string? problem
    )
    {
        ArgumentNullException.ThrowIfNull(tables);
        map = null;
        if (!MapSize.TryFromValue(sizeValue, out var size, out problem))
        {
            return false;
        }

        map = Generate(tables, seed, size, observer);
        problem = null;

        return true;
    }

    /// <summary>
    /// <c>MapGen_MarkWaterAdjacent</c> (0x4232ef): sets bit 7 of the object byte on each tile with a water triangle (a
    /// terrain nibble below 4). The generator and a drawn map (<see cref="Layout.BuildMap"/>) share this rule.
    /// </summary>
    internal static void MarkWaterAdjacentTiles(byte[] terrain, byte[] objects)
    {
        for (var tile = 0; tile < terrain.Length; tile++)
        {
            if ((terrain[tile] & 0xc0) == 0 || (terrain[tile] & 0x0c) == 0)
            {
                objects[tile] |= 0x80;
            }
        }
    }

    /// <summary>The column and row offsets around a tile in rings: the tile itself, then rings of 6, 12, and on.</summary>
    internal static IReadOnlyList<(int Dx, int Dy)> SpiralOffsets { get; } = BuildSpiral(256);

    /// <summary>
    /// The same table to its entry 258, which the search for the nearest flag reads (0x4353ef walks entries 1 to 258 from
    /// either end); entries 256 to 258 are the last rotations of the group of six that entry 253 starts.
    /// </summary>
    internal static IReadOnlyList<(int Dx, int Dy)> SearchOffsets { get; } = BuildSpiral(259);

    // The original builds the table at start-up (0x41e726) from a seed offset for each group of six: the six
    // rotations (x, y) -> (x - y, x) of the seed. Ring r holds the seeds (r, j) for j from 1 to r - 1, nearest
    // to the middle of the ring first (ties: the lower j), then (r, 0). The passes index at most 255.
    private static List<(int Dx, int Dy)> BuildSpiral(int count)
    {
        var table = new List<(int Dx, int Dy)> { (0, 0) };
        for (var ring = 1; table.Count < count; ring++)
        {
            var seeds = Enumerable.Range(1, ring - 1).OrderBy(j => Math.Abs(2 * j - ring)).ThenBy(j => j).Append(0);
            foreach (var j in seeds)
            {
                var (x, y) = (ring, j);
                for (var turn = 0; turn < 6; turn++)
                {
                    table.Add((x, y));
                    (x, y) = (x - y, x);
                }
            }
        }

        table.RemoveRange(count, table.Count - count);

        return table;
    }

    private sealed class Generation
    {
        // MapGen_InitParams (0x421530) stores the water level and the lake radius.
        private const int WaterLevel = 20;
        private const int LakeRadius = 14;

        // The 15 object scatter passes (0x424175 to 0x4246f4), each the parameters it hands MapGen_ScatterObjects
        // (0x424755): clusters per patch count, the mask of the draw that picks a spiral offset, the terrain range
        // from low up to but not including high, the object code base and the mask of the draw added to it, and the
        // objects tried in a cluster. The original writes these as immediates in its code (from 0x4241e3), not as a
        // table.
        private static readonly Scatter[] ScatterPasses =
        [
            new(8, 0xff, 5, 7, 0x08, 0x0f, 10),
            new(1, 0x3f, 5, 7, 0x08, 0x07, 45),
            new(1, 0x3f, 4, 7, 0x10, 0x07, 30),
            new(1, 0x7f, 5, 7, 0x08, 0x0f, 20),
            new(1, 0x3f, 5, 7, 0x48, 0x07, 40),
            new(1, 0xff, 5, 7, 0x48, 0x07, 15),
            new(1, 0xff, 5, 7, 0x5c, 0x00, 2),
            new(1, 0xff, 5, 7, 0x50, 0x01, 6),
            new(1, 0x7f, 2, 4, 0x1c, 0x03, 50),
            new(1, 0xff, 5, 7, 0x53, 0x00, 5),
            new(1, 0xff, 5, 7, 0x54, 0x01, 10),
            new(1, 0x0f, 10, 11, 0x56, 0x01, 2),
            new(1, 0x7f, 8, 11, 0x5a, 0x01, 6),
            new(1, 0x7f, 0, 3, 0x58, 0x01, 8),
            new(1, 0x3f, 10, 11, 0x18, 0x03, 6),
        ];

        // The deposit passes in the order they run (MapGen_PlaceDeposits3, 2, 1, and 4): deposits per patch count
        // and resource type. All four place on terrain from 11 up to but not including 15. The original writes these as
        // immediates in its code (from 0x424a03), not as a table.
        private static readonly Deposit[] DepositPasses = [new(9, 3), new(4, 2), new(2, 1), new(2, 4)];

        private readonly record struct Scatter(
            int CountPerPatch,
            int IndexMask,
            int LowTerrain,
            int HighTerrain,
            int BaseCode,
            int CodeMask,
            int ClusterSize
        );

        private readonly record struct Deposit(int CountPerPatch, int Type);

        // The six neighbors of a tile in the order the passes visit them: east, south-east, south, west,
        // north-west, north (the offsets of the original's 0x04, 0x08, 0x0c, 0x10, 0x14, 0x18 steps).
        private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (1, 1), (0, 1), (-1, 0), (-1, -1), (0, -1)];

        // The directions of the six sides of a ring around a tile, and the tile after each side.
        private static readonly (int Dx, int Dy)[] RingSides = [(0, 1), (-1, 0), (-1, -1), (0, -1), (1, 0), (1, 1)];

        private readonly byte[] _classes;
        private readonly MapSeed _seed;
        private readonly MapPassObserver? _observer;
        private readonly Map _map;
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _flags;
        private readonly byte[] _heights;
        private readonly byte[] _terrain;
        private readonly byte[] _lake;
        private readonly byte[] _objects;
        private readonly byte[] _resource;
        private RandomGenerator _generator;

        public Generation(ProgramTables tables, MapSeed seed, MapSize size, MapPassObserver? observer)
        {
            _classes = tables.Classes;
            _seed = seed;
            _observer = observer;
            _map = new Map(size);
            _width = size.Width;
            _height = size.Height;
            _flags = _map.Bytes(MapLayer.Flags);
            _heights = _map.Bytes(MapLayer.Height);
            _terrain = _map.Bytes(MapLayer.Terrain);
            _lake = _map.Bytes(MapLayer.Lake);
            _objects = _map.Bytes(MapLayer.Object);
            _resource = _map.Bytes(MapLayer.Resource);
        }

        public Map Run()
        {
            // MapGen_Generate copies the map seed into the generator and steps it twice.
            _generator = new RandomGenerator(_seed.W0, _seed.W1, _seed.W2).Step().Step();
            Report(MapPass.Seed);

            ClearMap();
            Report(MapPass.ClearMap);
            SeedHeights();
            Report(MapPass.SeedHeights);
            SubdivideHeights();
            Report(MapPass.SubdivideHeights);
            LimitSlopes();
            Report(MapPass.LimitSlopes);
            FillLakes();
            Report(MapPass.FillLakes);
            LowerToWaterLevel();
            Report(MapPass.LowerToWaterLevel);
            ClassifyTerrain();
            Report(MapPass.ClassifyTerrain);
            KeepMainLand();
            Report(MapPass.KeepMainLand);
            ScaleHeights();
            Report(MapPass.ScaleHeights);

            // MapGen_PlaceTerrainAndObjects (0x42336d).
            GradeWaterDepth();
            Report(MapPass.GradeWaterDepth);
            SetShoreTerrain();
            Report(MapPass.SetShoreTerrain);
            PlacePatches();
            Report(MapPass.PlacePatches);
            BlendPatchEdges();
            Report(MapPass.BlendPatchEdges);
            MarkPeaks();
            Report(MapPass.MarkPeaks);
            for (var pass = 0; pass < ScatterPasses.Length; pass++)
            {
                ScatterObjects(ScatterPasses[pass]);
                Report(MapPass.ScatterObjects1 + pass);
            }

            PlaceDeposits(DepositPasses[0]);
            Report(MapPass.PlaceDeposits3);
            PlaceDeposits(DepositPasses[1]);
            Report(MapPass.PlaceDeposits2);
            PlaceDeposits(DepositPasses[2]);
            Report(MapPass.PlaceDeposits1);
            PlaceDeposits(DepositPasses[3]);
            Report(MapPass.PlaceDeposits4);
            ResolveObjectOverlap();
            Report(MapPass.ResolveObjectOverlap);

            MarkWaterAdjacent();
            Report(MapPass.MarkWaterAdjacent);
            CountResource1();
            Report(MapPass.CountResource1);
            _map.Generator = _generator;

            return _map;
        }

        private void Report(MapPass pass) => _observer?.Invoke(pass, _map, _generator);

        // One step of the generator; the draw is the new first word, which Rng_Step (0x466ade) leaves behind.
        private ushort Draw()
        {
            _generator = _generator.Step();

            return _generator.W0;
        }

        private int Tiles => _width * _height;

        // The tile at a position; the map wraps at both edges.
        private int At(int x, int y) => (y & (_height - 1)) * _width + (x & (_width - 1));

        private int Move(int tile, int dx, int dy) => At(tile % _width + dx, tile / _width + dy);

        // MapGen_ClearMap (0x421d3a): zeroes the whole buffer.
        private void ClearMap()
        {
            foreach (var layer in Enum.GetValues<MapLayer>())
            {
                Array.Clear(_map.Bytes(layer));
            }
        }

        // MapGen_SeedHeights (0x421d71): one random height, at most 250, on every 16th column and row.
        private void SeedHeights()
        {
            for (var y = 0; y < _height; y += 16)
            {
                for (var x = 0; x < _width; x += 16)
                {
                    var height = (byte)Draw();
                    _heights[At(x, y)] = height > 250 ? (byte)250 : height;
                }
            }
        }

        // MapGen_SubdivideHeights (0x421e75) with MapGen_DisplaceMidpoint (0x4220ec): midpoint subdivision from
        // a distance of 16 tiles down to 1. One draw sets the displacement for all four sweeps; each sweep
        // halves it.
        private void SubdivideHeights()
        {
            var first = Draw();
            var amplitude = (ushort)((first & 0x7f) + 0x80);
            var spread = (ushort)((amplitude * 0x9999) >> 16);

            // The original keeps the second height of a midpoint in the low byte of a word. Before the first
            // midpoint the word's high byte still holds the high byte of the last draw.
            var word = first;
            for (var half = 8; half >= 1; half >>= 1)
            {
                for (var y = 0; y < _height; y += 2 * half)
                {
                    for (var x = 0; x < _width; x += 2 * half)
                    {
                        var corner = _heights[At(x, y)];
                        _heights[At(x + half, y)] = Displace(corner, _heights[At(x + 2 * half, y)]);
                        _heights[At(x, y + half)] = Displace(corner, _heights[At(x, y + 2 * half)]);
                        _heights[At(x + half, y + half)] = Displace(corner, _heights[At(x + 2 * half, y + 2 * half)]);
                    }
                }

                spread >>= 1;
                amplitude >>= 1;
            }

            byte Displace(byte corner, byte other)
            {
                word = (ushort)((word & 0xff00) | other);
                var mean = (ushort)(corner + word) >> 1;
                var scaled = (ushort)((Draw() * amplitude) >> 16);
                var value = (short)(ushort)(scaled - spread + mean);
                value =
                    value < 0 ? (short)0
                    : (ushort)value > 250 ? (short)250
                    : value;
                word = (ushort)value;

                return (byte)value;
            }
        }

        // MapGen_LimitSlopes (0x42215a) with its two identical steps (0x42223b, 0x4222ad): repeats until no
        // neighbor to the south, south-east, and east differs by more than 32 from the tile.
        private void LimitSlopes()
        {
            bool changed;
            do
            {
                changed = false;
                for (var tile = 0; tile < Tiles; tile++)
                {
                    var height = _heights[tile];
                    changed |= Clamp(height, Move(tile, 0, 1));
                    changed |= Clamp(height, Move(tile, 1, 1));
                    changed |= Clamp(height, Move(tile, 1, 0));
                }
            } while (changed);

            bool Clamp(int height, int neighbor)
            {
                var difference = height - _heights[neighbor];
                if (difference < 0 && -difference > 32)
                {
                    _heights[neighbor] = (byte)(height + 32);

                    return true;
                }

                if (difference > 32)
                {
                    _heights[neighbor] = (byte)(height - 32);

                    return true;
                }

                return false;
            }
        }

        // MapGen_FillLakes (0x422a0e): floods each level up to the water level, then turns the lake marks into
        // water and shore.
        private void FillLakes()
        {
            for (var level = 0; level <= WaterLevel; level++)
            {
                for (var tile = 0; tile < Tiles; tile++)
                {
                    if (_heights[tile] == level)
                    {
                        GrowLake(tile);
                    }
                }
            }

            for (var tile = 0; tile < Tiles; tile++)
            {
                switch (_heights[tile])
                {
                    case 0:
                        _heights[tile] = WaterLevel + 1;
                        break;
                    case 0xfd:
                        _lake[tile] = (byte)(Draw() & 7);
                        _flags[tile] |= 0x40;
                        _heights[tile] = WaterLevel - 1;
                        break;
                    case 0xfc:
                        _heights[tile] = WaterLevel;
                        break;
                }
            }
        }

        // MapGen_GrowLake (0x422bf3): marks a lake around a tile whose six neighbors are all at or below the
        // water level, and grows it ring by ring. The marks are 0xff for the lake, 0xfe for its edge, and after
        // the lake is done 0xfd and 0xfc. A tile that cannot start a lake gets height 0.
        private void GrowLake(int middle)
        {
            if (Neighbors.Any(side => _heights[Move(middle, side.Dx, side.Dy)] > WaterLevel))
            {
                _heights[middle] = 0;

                return;
            }

            _heights[middle] = 0xff;
            foreach (var side in Neighbors)
            {
                _heights[Move(middle, side.Dx, side.Dy)] = 0xfe;
            }

            var ring = 0;
            bool grew;
            do
            {
                grew = false;
                foreach (var tile in RingTiles(middle, ring))
                {
                    grew |= GrowLakeTile(tile);
                }
            } while (grew && ++ring != LakeRadius);

            if (_heights[middle] > 0xfd)
            {
                _heights[middle] -= 2;
            }

            for (ring = 0; ring <= LakeRadius; ring++)
            {
                foreach (var tile in RingTiles(middle, ring))
                {
                    if (_heights[tile] > 0xfd)
                    {
                        _heights[tile] -= 2;
                    }
                }
            }
        }

        // MapGen_GrowLakeTile (0x423041): adds a tile to the lake when it touches the lake, and none of its
        // neighbors is above the water level.
        private bool GrowLakeTile(int tile)
        {
            var touchesLake = false;
            foreach (var side in Neighbors)
            {
                var height = _heights[Move(tile, side.Dx, side.Dy)];
                if (height < 0xfe)
                {
                    if (height > WaterLevel)
                    {
                        return false;
                    }
                }
                else if (height == 0xff)
                {
                    touchesLake = true;
                }
            }

            if (!touchesLake)
            {
                return false;
            }

            _heights[tile] = 0xff;
            foreach (var side in Neighbors)
            {
                var neighbor = Move(tile, side.Dx, side.Dy);
                if (_heights[neighbor] != 0xff)
                {
                    _heights[neighbor] = 0xfe;
                }
            }

            return true;
        }

        // The tiles of the ring at a distance of ring + 1 around a tile: six sides of ring + 1 tiles each,
        // starting east of the tile and turning at each corner.
        private IEnumerable<int> RingTiles(int middle, int ring)
        {
            var tile = Move(middle, ring + 1, 0);
            foreach (var side in RingSides)
            {
                for (var step = 0; step <= ring; step++)
                {
                    yield return tile;

                    tile = Move(tile, side.Dx, side.Dy);
                }
            }
        }

        // MapGen_LowerToWaterLevel (0x42231f): lowers every height so that water is 0 and the shore is 1.
        private void LowerToWaterLevel()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                _heights[tile] = (byte)(_heights[tile] - (WaterLevel - 1));
            }
        }

        // MapGen_ClassifyTerrain (0x422857): the terrain of the two triangles of each tile from the sums of
        // their three corner heights. The upper triangle takes the high nibble.
        private void ClassifyTerrain()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                int corner = _heights[tile];
                int east = _heights[Move(tile, 1, 0)];
                int southEast = _heights[Move(tile, 1, 1)];
                int south = _heights[Move(tile, 0, 1)];
                _terrain[tile] = (byte)(
                    (TerrainFromHeightSum(corner + southEast + south) << 4)
                    | TerrainFromHeightSum(corner + east + southEast)
                );
            }
        }

        // MapGen_TerrainFromHeightSum (0x42297f).
        private static int TerrainFromHeightSum(int sum) =>
            sum switch
            {
                < 0x003 => 0x0,
                < 0x180 => 0x5,
                < 0x1a0 => 0x6,
                < 0x1c0 => 0xb,
                < 0x1e0 => 0xc,
                < 0x210 => 0xd,
                < 0x230 => 0xe,
                _ => 0xf,
            };

        // MapGen_KeepMainLand (0x4223de): keeps the first land mass that covers a quarter of the map and turns
        // the rest into water. Where the scan finds none, the original scans without end; this throws.
        private void KeepMainLand()
        {
            var reached = new byte[Tiles];
            var start = 0;
            var flood = _heights[0] != 0;
            var wraps = 0;
            while (true)
            {
                if (flood && Flood(reached, start))
                {
                    RemoveUnreachedLand(reached);

                    return;
                }

                start = Move(start, 1, 0);
                if (start % _width != 0)
                {
                    flood = _heights[start] != 0;
                    continue;
                }

                start = Move(start, 0, 1);
                if (start != 0)
                {
                    flood = _heights[start] != 0;
                    continue;
                }

                if (++wraps > 1)
                {
                    throw new InvalidOperationException("no land mass covers a quarter of the map");
                }

                flood = true;
            }
        }

        // Marks the land mass around a tile in the reached layer; true when it covers a quarter of the map.
        // A tile joins the mass through the corners whose triangles hold land.
        private bool Flood(byte[] reached, int start)
        {
            reached[start] = 0xff;
            var count = 0;
            bool changed;
            do
            {
                changed = false;
                for (var tile = 0; tile < Tiles; tile++)
                {
                    if (reached[tile] < 0x80)
                    {
                        continue;
                    }

                    count++;
                    reached[tile] = 1;
                    var west = Move(tile, -1, 0);
                    var northWest = Move(west, 0, -1);
                    var north = Move(northWest, 1, 0);
                    var sides = 0;
                    sides |= (_terrain[tile] & 0x0c) != 0 ? 0x03 : 0;
                    sides |= (_terrain[tile] & 0xc0) != 0 ? 0x06 : 0;
                    sides |= (_terrain[west] & 0x0c) != 0 ? 0x0c : 0;
                    sides |= (_terrain[northWest] & 0xc0) != 0 ? 0x18 : 0;
                    sides |= (_terrain[northWest] & 0x0c) != 0 ? 0x30 : 0;
                    sides |= (_terrain[north] & 0xc0) != 0 ? 0x21 : 0;
                    for (var side = 0; side < Neighbors.Length; side++)
                    {
                        if ((sides >> side & 1) == 0)
                        {
                            continue;
                        }

                        var neighbor = Move(tile, Neighbors[side].Dx, Neighbors[side].Dy);
                        if (reached[neighbor] == 0)
                        {
                            reached[neighbor] = 0xff;
                            changed = true;
                        }
                    }
                }
            } while (changed);

            return count * 4 >= Tiles;
        }

        private void RemoveUnreachedLand(byte[] reached)
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                if (_heights[tile] == 0 || reached[tile] != 0)
                {
                    continue;
                }

                _heights[tile] = 0;
                _terrain[tile] = 0;
                var west = Move(tile, -1, 0);
                _terrain[west] &= 0xf0;
                var northWest = Move(west, 0, -1);
                _terrain[northWest] = 0;
                _terrain[Move(northWest, 1, 0)] &= 0x0f;
            }
        }

        // MapGen_ScaleHeights (0x42237d): scales the heights down to 0 to 32.
        private void ScaleHeights()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                _heights[tile] = (byte)((byte)(_heights[tile] + 6) >> 3);
            }
        }

        // Map_SetGeometry (0x42195c) stores the base count of patches, clusters, and deposits: the map's columns
        // and rows each divided by 32, multiplied.
        private int PatchCount => (_width >> 5) * (_height >> 5);

        // MapGen_GradeWaterDepth (0x4234bd): water terrain 0 next to terrain 5 becomes 3, then 0 next to 3
        // becomes 2, then 0 next to 2 becomes 1.
        private void GradeWaterDepth()
        {
            ReplaceTerrainNear(0, 5, 3);
            ReplaceTerrainNear(0, 3, 2);
            ReplaceTerrainNear(0, 2, 1);
        }

        // MapGen_SetShoreTerrain (0x423536): terrain 5 next to terrain 3 becomes 4.
        private void SetShoreTerrain() => ReplaceTerrainNear(5, 3, 4);

        // MapGen_BlendPatchEdges (0x423d74): patch terrain 10 gives way to 5 through the blend types 7, 8, and 9;
        // then the blend types go back to 5 and terrain 5 next to a patch takes them in the other direction.
        private void BlendPatchEdges()
        {
            ReplaceTerrainNear(10, 5, 7);
            ReplaceTerrainNear(10, 7, 8);
            ReplaceTerrainNear(10, 8, 9);
            ResetTypes7To9();
            ReplaceTerrainNear(5, 10, 9);
            ReplaceTerrainNear(5, 9, 8);
            ReplaceTerrainNear(5, 8, 7);
        }

        // MapGen_ReplaceTerrainNear (0x42355f): in each triangle of the target terrain, in tile order, upper
        // triangle first, the replacement takes the target's place when the neighborhood holds the near terrain.
        // The neighborhood is the 3 by 3 tiles around the tile as they stood before the tile's own change, so
        // the lower triangle's test does not see the upper triangle's replacement.
        private void ReplaceTerrainNear(int target, int near, int replacement)
        {
            Span<byte> around = stackalloc byte[9];
            for (var tile = 0; tile < Tiles; tile++)
            {
                if ((_terrain[tile] & 0x0f) != target && _terrain[tile] >> 4 != target)
                {
                    continue;
                }

                for (var cell = 0; cell < 9; cell++)
                {
                    around[cell] = _terrain[Move(tile, cell % 3 - 1, cell / 3 - 1)];
                }

                if (_terrain[tile] >> 4 == target && NearUpper(around, near))
                {
                    _terrain[tile] = (byte)((_terrain[tile] & 0x0f) | (replacement << 4));
                }

                if ((_terrain[tile] & 0x0f) == target && NearLower(around, near))
                {
                    _terrain[tile] = (byte)((_terrain[tile] & 0xf0) | replacement);
                }
            }
        }

        // MapGen_HasTerrainNearUpper (0x423789) and MapGen_HasTerrainNearLower (0x4238b2). The cells are the 3 by 3
        // terrain bytes in row-major order; the lists name the triangles (cell, high nibble or not) each tests.
        private static bool NearUpper(ReadOnlySpan<byte> around, int near) =>
            Holds(
                around,
                near,
                (0, false),
                (0, true),
                (1, true),
                (3, false),
                (3, true),
                (4, false),
                (5, true),
                (6, false),
                (7, false),
                (7, true),
                (8, false),
                (8, true)
            );

        private static bool NearLower(ReadOnlySpan<byte> around, int near) =>
            Holds(
                around,
                near,
                (0, false),
                (0, true),
                (1, false),
                (1, true),
                (2, true),
                (3, false),
                (4, true),
                (5, false),
                (5, true),
                (7, false),
                (8, false),
                (8, true)
            );

        private static bool Holds(ReadOnlySpan<byte> around, int near, params (int Cell, bool High)[] triangles)
        {
            foreach (var (cell, high) in triangles)
            {
                if ((high ? around[cell] >> 4 : around[cell] & 0x0f) == near)
                {
                    return true;
                }
            }

            return false;
        }

        // MapGen_ResetTypes7To9 (0x423e6a): the blend types 7 to 9 in either triangle become 5.
        private void ResetTypes7To9()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                var low = _terrain[tile] & 0x0f;
                var high = _terrain[tile] >> 4;
                _terrain[tile] = (byte)((high is > 6 and < 10 ? 5 : high) << 4 | (low is > 6 and < 10 ? 5 : low));
            }
        }

        // MapGen_RandomPos (0x423aa3): draws a column, then a row.
        private (int X, int Y) RandomPos()
        {
            var x = Draw() & (_width - 1);
            var y = Draw() & (_height - 1);

            return (x, y);
        }

        // MapGen_SpiralPos (0x423b30): the tile at an index of the spiral table around a center.
        private int SpiralPos((int X, int Y) center, int index) =>
            At(center.X + SpiralOffsets[index].Dx, center.Y + SpiralOffsets[index].Dy);

        // MapGen_PlacePatches (0x4239db): patch count tries, each up to 200 random tiles for one of terrain 5 in
        // both triangles, around which a patch of terrain 10 grows over the spiral from its outer end to its middle.
        private void PlacePatches()
        {
            var count = PatchCount;
            do
            {
                var center = (X: 0, Y: 0);
                var found = false;
                for (var tries = 200; tries > 0 && !found; tries--)
                {
                    center = RandomPos();
                    found = _terrain[At(center.X, center.Y)] == 0x55;
                }

                if (!found)
                {
                    continue;
                }

                for (var index = 0xff; index >= 0; index--)
                {
                    var tile = SpiralPos(center, index);
                    if (CanPatchLower(tile))
                    {
                        _terrain[tile] = (byte)((_terrain[tile] & 0x0f) | 0xa0);
                    }

                    if (CanPatchUpper(tile))
                    {
                        _terrain[tile] = (byte)((_terrain[tile] & 0xf0) | 0x0a);
                    }
                }
            } while (--count != 0);
        }

        private static bool Patchable(int nibble) => nibble is 5 or 10;

        // MapGen_CanPatchLower (0x423c10): both triangles of the tile and the lower triangles of the tiles to
        // the west and south are plain (5) or patch (10).
        private bool CanPatchLower(int tile) =>
            Patchable(_terrain[tile] & 0x0f)
            && Patchable(_terrain[tile] >> 4)
            && Patchable(_terrain[Move(tile, -1, 0)] & 0x0f)
            && Patchable(_terrain[Move(tile, 0, 1)] & 0x0f);

        // MapGen_CanPatchUpper (0x423cc7): both triangles of the tile and the upper triangles of the tiles to the
        // east and north are plain or patch.
        private bool CanPatchUpper(int tile) =>
            Patchable(_terrain[tile] & 0x0f)
            && Patchable(_terrain[tile] >> 4)
            && Patchable(_terrain[Move(tile, 1, 0)] >> 4)
            && Patchable(_terrain[Move(tile, 0, -1)] >> 4);

        // MapGen_MarkPeaks (0x423fec): an object 0x52 on every tile above 25 that is not lower than its east,
        // south-east, and south neighbors and higher than its west, north-west, and north neighbors. The
        // original compares the low 5 bits of the heights, so a height of 32 reads as 0.
        private void MarkPeaks()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                var height = _heights[tile] & 0x1f;
                if (height <= 25)
                {
                    continue;
                }

                bool Level(int dx, int dy) => (_heights[Move(tile, dx, dy)] & 0x1f) <= height;
                bool Lower(int dx, int dy) => (_heights[Move(tile, dx, dy)] & 0x1f) < height;
                if (Level(1, 0) && Level(1, 1) && Level(0, 1) && Lower(-1, 0) && Lower(-1, -1) && Lower(0, -1))
                {
                    _objects[tile] = 0x52;
                }
            }
        }

        // MapGen_FitsTerrain (0x424802): both triangles of the tile, the lower triangle of the tile to the west,
        // both triangles of the tile to the north-west, and the lower triangle of the tile to the north all lie
        // from low up to but not including high.
        private bool FitsTerrain(int tile, int low, int high)
        {
            bool In(int nibble) => nibble >= low && nibble < high;
            int Terrain(int dx, int dy) => _terrain[Move(tile, dx, dy)];

            return In(_terrain[tile] & 0x0f)
                && In(_terrain[tile] >> 4)
                && In(Terrain(-1, 0) & 0x0f)
                && In(Terrain(-1, -1) & 0x0f)
                && In(Terrain(-1, -1) >> 4)
                && In(Terrain(0, -1) & 0x0f);
        }

        // MapGen_ScatterObjects (0x424755): clusters of objects. Each cluster takes up to 100 random tiles for
        // one that fits the terrain, then draws a spiral offset for each of its objects. An object goes on a
        // fitting tile that holds none, with a code of the pass's base plus a draw masked.
        private void ScatterObjects(Scatter pass)
        {
            var count = PatchCount * pass.CountPerPatch;
            do
            {
                var center = (X: 0, Y: 0);
                var found = false;
                for (var tries = 100; tries > 0 && !found; tries--)
                {
                    center = RandomPos();
                    found = FitsTerrain(At(center.X, center.Y), pass.LowTerrain, pass.HighTerrain);
                }

                if (!found)
                {
                    continue;
                }

                for (var left = pass.ClusterSize; left > 0; left--)
                {
                    // MapGen_RandomSpiralPos (0x423f07): one draw masked picks the offset.
                    var tile = SpiralPos(center, Draw() & pass.IndexMask);
                    if (FitsTerrain(tile, pass.LowTerrain, pass.HighTerrain) && _objects[tile] == 0)
                    {
                        _objects[tile] = (byte)(((byte)Draw() & pass.CodeMask) + pass.BaseCode);
                    }
                }
            } while (--count != 0);
        }

        // MapGen_PlaceDeposits (0x424a9e): for each of the pass's deposits, up to 100 random tiles for one that fits
        // terrain 11 to 14, then rings of 1, 6, 12, 18, and 24 tiles around it on the spiral, the first with an
        // amount of 8, 12, 16, or 20 and each next 4 less, until the amount runs out. A sixth ring would get
        // amount 0 even from 20 (20, 16, 12, 8, 4), so the walk ends after the fifth. The original also tests the
        // byte after the resource byte of the tile (the site 0x424ad8); no pass writes that byte, so the test
        // always holds and the engine leaves it out.
        private void PlaceDeposits(Deposit pass)
        {
            var count = PatchCount * pass.CountPerPatch;
            do
            {
                var center = (X: 0, Y: 0);
                var found = false;
                for (var tries = 100; tries > 0 && !found; tries--)
                {
                    center = RandomPos();
                    found = FitsTerrain(At(center.X, center.Y), 11, 15);
                }

                if (!found)
                {
                    continue;
                }

                var amount = (Draw() & 0x0c) + 8;
                var index = 0;
                foreach (var ring in new[] { 1, 6, 12, 18, 24 })
                {
                    for (var step = 0; step < ring; step++)
                    {
                        var tile = SpiralPos(center, index++);
                        var present = _resource[tile];
                        if (present == 0 || (present & 0x1f) < amount)
                        {
                            _resource[tile] = (byte)((pass.Type << 5) + amount);
                        }
                    }

                    amount -= 4;
                    if (amount == 0)
                    {
                        break;
                    }
                }
            } while (--count != 0);
        }

        // MapGen_ResolveObjectOverlap (0x424c57): a large object, one whose byte in the class table (0x41e640) is
        // above 1 unsigned, stays only where the tiles to its west, north-west, and north carry no object mark (flag
        // bit 6); it then sets its own mark. Otherwise the object code is removed and the water bit stays.
        private void ResolveObjectOverlap()
        {
            for (var tile = 0; tile < Tiles; tile++)
            {
                if (_classes[_objects[tile] & 0x7f] <= 1)
                {
                    continue;
                }

                if (
                    (_flags[Move(tile, -1, 0)] & 0x40) == 0
                    && (_flags[Move(tile, -1, -1)] & 0x40) == 0
                    && (_flags[Move(tile, 0, -1)] & 0x40) == 0
                )
                {
                    _flags[tile] |= 0x40;
                }
                else
                {
                    _objects[tile] &= 0x80;
                }
            }
        }

        // MapGen_MarkWaterAdjacent (0x4232ef): bit 7 of the object byte on each tile with a water triangle (a
        // terrain nibble below 4).
        private void MarkWaterAdjacent() => MarkWaterAdjacentTiles(_terrain, _objects);

        // MapGen_CountResource1 (0x421841): the sum of the amounts of the deposits of resource type 1.
        private void CountResource1()
        {
            var total = 0;
            foreach (var value in _resource)
            {
                if ((value & 0xe0) == 0x20)
                {
                    total += value & 0x1f;
                }
            }

            _map.Resource1Total = total;
        }
    }
}
