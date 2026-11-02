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
    /// Generates the map of a seed and size, reporting to the observer after the seeding and after each pass.
    /// </summary>
    /// <exception cref="ArgumentException">The size is not one of <see cref="MapSize.Offered"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// No land mass covers a quarter of the map. The original searches for one without end.
    /// </exception>
    public static Map Generate(MapSeed seed, MapSize size, MapPassObserver? observer = null)
    {
        if (!MapSize.TryFromValue(size.Value, out var offered, out var problem) || offered != size)
        {
            throw new ArgumentException(problem ?? "the map size is not one the original offers", nameof(size));
        }

        return new Generation(seed, size, observer).Run();
    }

    /// <summary>
    /// Generates like <see cref="Generate"/> for a size value. Returns false with the reason, and generates
    /// nothing, when the original offers no such size.
    /// </summary>
    public static bool TryGenerate(
        MapSeed seed,
        int sizeValue,
        MapPassObserver? observer,
        [NotNullWhen(true)] out Map? map,
        [NotNullWhen(false)] out string? problem
    )
    {
        map = null;
        if (!MapSize.TryFromValue(sizeValue, out var size, out problem))
        {
            return false;
        }

        map = Generate(seed, size, observer);
        problem = null;

        return true;
    }

    private sealed class Generation
    {
        // MapGen_InitParams (0x421530) stores the water level and the lake radius.
        private const int WaterLevel = 20;
        private const int LakeRadius = 14;

        // The six neighbors of a tile in the order the passes visit them: east, south-east, south, west,
        // north-west, north (the offsets of the original's 0x04, 0x08, 0x0c, 0x10, 0x14, 0x18 steps).
        private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (1, 1), (0, 1), (-1, 0), (-1, -1), (0, -1)];

        // The directions of the six sides of a ring around a tile, and the tile after each side.
        private static readonly (int Dx, int Dy)[] RingSides = [(0, 1), (-1, 0), (-1, -1), (0, -1), (1, 0), (1, 1)];

        private readonly MapSeed _seed;
        private readonly MapPassObserver? _observer;
        private readonly Map _map;
        private readonly int _width;
        private readonly int _height;
        private readonly byte[] _flags;
        private readonly byte[] _heights;
        private readonly byte[] _terrain;
        private readonly byte[] _lake;
        private RandomGenerator _generator;

        public Generation(MapSeed seed, MapSize size, MapPassObserver? observer)
        {
            _seed = seed;
            _observer = observer;
            _map = new Map(size);
            _width = size.Width;
            _height = size.Height;
            _flags = _map.Bytes(MapLayer.Flags);
            _heights = _map.Bytes(MapLayer.Height);
            _terrain = _map.Bytes(MapLayer.Terrain);
            _lake = _map.Bytes(MapLayer.Lake);
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
    }
}
