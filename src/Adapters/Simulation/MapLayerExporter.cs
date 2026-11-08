using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// Encodes the typed <see cref="Map"/> into the layers of the original's encoding (<c>docs/rules/map-form.md</c>), for the
/// digest the game and the emulator comparison use. The layers are exactly what the original keeps, so a map the
/// importer read gives back the layers it read.
/// </summary>
public static class MapLayerExporter
{
    /// <summary>The layers of the map, with its generator and resource total.</summary>
    public static MapLayers Export(Map map)
    {
        var layers = new MapLayers(map.Size) { Generator = map.Generator, Resource1Total = map.Resource1Total };
        var flags = layers.Bytes(MapLayer.Flags);
        var heights = layers.Bytes(MapLayer.Height);
        var terrain = layers.Bytes(MapLayer.Terrain);
        var objects = layers.Bytes(MapLayer.Object);
        var deposits = layers.Bytes(MapLayer.Resource);

        for (var index = 0; index < map.Count; index++)
        {
            var node = map.At(index);
            flags[index] = (byte)(node.Roads.Bits | (node.LakeMark ? 0x40 : 0) | (node.FlagMark ? 0x80 : 0));
            heights[index] = (byte)(node.Height | node.Owner.Code << 5);
            terrain[index] = (byte)(node.Upper.Code << 4 | node.Lower.Code);
            objects[index] = (byte)(node.Object.Code | (node.WaterTriangle ? 0x80 : 0));
            // The link takes the byte of the deposit and the lake detail, which are the same bytes.
            deposits[index] = (byte)(node.Link ?? (node.DepositType << 5 | node.DepositAmount));
        }

        return layers;
    }
}
