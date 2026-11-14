using System;
using System.Linq;
using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// The map in the canonical form of <c>docs/rules/map-form.md</c>: the layers of <see cref="MapLayer"/>, one byte for
/// each tile, in row-major order, with the generator and the resource total beside them. The map generator writes
/// them, <see cref="MapImporter"/> turns them into the typed <see cref="Map"/>, and <see cref="MapLayerExporter"/>
/// turns a typed map back into them. They exist only at the edge of the adapters.
/// </summary>
public sealed class MapLayers
{
    readonly byte[][] _layers;

    internal MapLayers(MapSize size)
    {
        Size = size;
        _layers = [.. Enum.GetValues<MapLayer>().Select(_ => new byte[size.Width * size.Height])];
        // The original keeps the lake detail and the resource deposit in one byte of each tile.
        _layers[(int)MapLayer.Resource] = _layers[(int)MapLayer.Lake];
    }

    /// <summary>
    /// The random generator as the generation leaves it. A map made by hand has the generator of all zeros.
    /// </summary>
    internal RandomGenerator Generator { get; set; }

    /// <summary>The sum of the amounts of resource type 1 (<c>MapGen_CountResource1</c>); 0 before that pass.</summary>
    internal int Resource1Total { get; set; }

    /// <summary>The size the map was generated for.</summary>
    public MapSize Size { get; }

    /// <summary>The bytes of a layer: the tile in column <c>x</c> and row <c>y</c> is at <c>y * width + x</c>.</summary>
    public ReadOnlySpan<byte> Layer(MapLayer layer) => _layers[(int)layer];

    /// <summary>The FNV-1a 64-bit digest of a layer's bytes in row-major order.</summary>
    public ulong Digest(MapLayer layer)
    {
        var hash = Fnv1a.Offset;
        foreach (var value in _layers[(int)layer])
        {
            hash = Fnv1a.Add(hash, value);
        }

        return hash;
    }

    internal byte[] Bytes(MapLayer layer) => _layers[(int)layer];

    /// <summary>The index of a node in a layer; the column and the row wrap at the edges.</summary>
    internal int Index(int x, int y) => (y & (Size.Height - 1)) * Size.Width + (x & (Size.Width - 1));
}
