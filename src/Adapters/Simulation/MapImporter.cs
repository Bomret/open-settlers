using OpenSettlers.Domain;

namespace OpenSettlers.Simulation;

/// <summary>
/// Builds the typed <see cref="Map"/> from the layers of the original's encoding (<c>docs/rules/map-form.md</c>): the
/// map generator's output, or a hand-drawn map's. Every bit of a layer lands in a named field, and a value with no
/// recovered meaning stays a raw value (an object code, a terrain code, a deposit type), so
/// <see cref="MapLayerExporter"/> writes the same layers back.
/// </summary>
public static class MapImporter
{
    /// <summary>The typed map of the layers, with their generator and resource total.</summary>
    public static Map Import(MapLayers layers)
    {
        var map = new Map(layers.Size) { Generator = layers.Generator, Resource1Total = layers.Resource1Total };
        var flags = layers.Layer(MapLayer.Flags);
        var heights = layers.Layer(MapLayer.Height);
        var terrain = layers.Layer(MapLayer.Terrain);
        var objects = layers.Layer(MapLayer.Object);
        // The lake detail and the deposit are the same bytes, so the deposit reads them.
        var deposits = layers.Layer(MapLayer.Resource);

        for (var index = 0; index < map.Count; index++)
        {
            ref var node = ref map.At(index);
            node.Roads = new RoadSet((byte)(flags[index] & 0x3f));
            node.LakeMark = (flags[index] & 0x40) != 0;
            node.FlagMark = (flags[index] & 0x80) != 0;
            node.Height = heights[index] & 0x1f;
            node.Owner = new Owner(heights[index] >> 5);
            node.Upper = new Terrain(terrain[index] >> 4);
            node.Lower = new Terrain(terrain[index] & 0xf);
            node.Object = new MapObject((byte)(objects[index] & 0x7f));
            node.WaterTriangle = (objects[index] & 0x80) != 0;
            node.DepositType = deposits[index] >> 5;
            node.DepositAmount = deposits[index] & 0x1f;
        }

        return map;
    }
}
