using System;
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

    private readonly byte[][] _layers;

    internal Map(MapSize size)
    {
        Size = size;
        _layers = [.. Enum.GetValues<MapLayer>().Select(_ => new byte[size.Width * size.Height])];
    }

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
}
