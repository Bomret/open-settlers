using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenSettlers.Simulation;

/// <summary>
/// A generated map in the canonical form of <c>docs/rules/map-form.md</c>: the layers of <see cref="MapLayer"/>,
/// one byte for each tile, in row-major order. The map wraps at its edges.
/// </summary>
public sealed class Map
{
    private const ulong FnvOffset = 0xcbf29ce484222325;
    private const ulong FnvPrime = 0x100000001b3;

    /// <summary>The bits of a height byte that hold the height; the top 3 bits hold the owner.</summary>
    public const int HeightMask = 0x1f;

    private readonly byte[][] _layers;

    internal Map(MapSize size)
    {
        Size = size;
        _layers = [.. Enum.GetValues<MapLayer>().Select(_ => new byte[size.Width * size.Height])];
        // The original keeps the lake detail and the resource deposit in one byte of each tile.
        _layers[(int)MapLayer.Resource] = _layers[(int)MapLayer.Lake];
    }

    /// <summary>The sum of the amounts of resource type 1 (<c>MapGen_CountResource1</c>); 0 before that pass.</summary>
    public int Resource1Total { get; internal set; }

    /// <summary>The objects on the map in row-major tile order: every tile whose object code is not 0.</summary>
    public IReadOnlyList<MapObject> Objects =>
        [
            .. _layers[(int)MapLayer.Object]
                .Select((value, tile) => (value, tile))
                .Where(entry => (entry.value & 0x7f) != 0)
                .Select(entry => new MapObject(
                    entry.tile % Size.Width,
                    entry.tile / Size.Width,
                    (byte)(entry.value & 0x7f)
                )),
        ];

    /// <summary>The resource deposits on the map in row-major tile order: every tile whose resource type is not 0.</summary>
    public IReadOnlyList<ResourceDeposit> Deposits =>
        [
            .. _layers[(int)MapLayer.Resource]
                .Select((value, tile) => (value, tile))
                .Where(entry => entry.value >> 5 != 0)
                .Select(entry => new ResourceDeposit(
                    entry.tile % Size.Width,
                    entry.tile / Size.Width,
                    entry.value >> 5,
                    entry.value & 0x1f
                )),
        ];

    /// <summary>The size the map was generated for.</summary>
    public MapSize Size { get; }

    /// <summary>The bytes of a layer: the tile in column <c>x</c> and row <c>y</c> is at <c>y * width + x</c>.</summary>
    public ReadOnlySpan<byte> Layer(MapLayer layer) => _layers[(int)layer];

    /// <summary>The FNV-1a 64-bit digest of a layer's bytes in row-major order.</summary>
    public ulong Digest(MapLayer layer)
    {
        var hash = FnvOffset;
        foreach (var value in _layers[(int)layer])
        {
            hash = (hash ^ value) * FnvPrime;
        }

        return hash;
    }

    internal byte[] Bytes(MapLayer layer) => _layers[(int)layer];

    /// <summary>The index of a node in a layer; the column and the row wrap at the edges.</summary>
    internal int Index(int x, int y) => (y & (Size.Height - 1)) * Size.Width + (x & (Size.Width - 1));
}
