using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>One object to draw: its tile row, its sprite entry and the draw position in map pixels.</summary>
public readonly record struct ObjectDraw(int Row, int Entry, int X, int Y);

/// <summary>Where each generated object lies on the map (<c>docs/rules/map-objects.md</c>).</summary>
public static class ObjectDraws
{
    /// <summary>Pixels east of its node an object is drawn (<c>0x44da4d</c> adds 16 to the position).</summary>
    public const int OffsetX = 16;

    /// <summary>Pixels south of its node an object is drawn (<c>0x44da4d</c> adds 8 to the position).</summary>
    public const int OffsetY = 8;

    /// <summary>
    /// The objects of a map in row-major order, which is the order the original paints them. A draw sits at its
    /// tile's node with the node's height taken off, moved by <see cref="OffsetX"/> and <see cref="OffsetY"/>; the
    /// sprite's own header offsets apply where the sprite is drawn. A code without a sprite is left out. Resources
    /// are no objects and are never drawn. With a <paramref name="castle"/>, its sprite is drawn at its node, and each
    /// woodcutter's hut of <paramref name="buildings"/> likewise.
    /// </summary>
    public static List<ObjectDraw> Of(Map map, Castle? castle = null, IReadOnlyList<Building>? buildings = null)
    {
        var heights = map.Layer(MapLayer.Height);
        var width = map.Size.Width;
        var draws = new List<ObjectDraw>(map.Objects.Count);
        foreach (var item in map.Objects)
        {
            if (ObjectSprites.Entry(item.Code) is not { } entry)
            {
                continue;
            }

            var (x, y) = Camera.NodePosition(item.X, item.Y);
            var height = heights[item.Y * width + item.X] & Map.HeightMask;
            draws.Add(new ObjectDraw(item.Y, entry, x + OffsetX, y - height * TerrainTriangles.HeightStep + OffsetY));
        }

        if (castle is { } placed)
        {
            // The castle is no map object of code 8 or more; it is drawn at its node in the order of its row, after
            // the objects of its row. inferred: its alignment to the node is that of an object, which the sprite's own
            // header offsets then adjust.
            var (x, y) = Camera.NodePosition(placed.X, placed.Y);
            var height = heights[placed.Y * width + placed.X] & Map.HeightMask;
            var index = draws.FindIndex(draw => draw.Row > placed.Y);
            draws.Insert(
                index < 0 ? draws.Count : index,
                new ObjectDraw(
                    placed.Y,
                    ObjectSprites.CastleEntry,
                    x + OffsetX,
                    y - height * TerrainTriangles.HeightStep + OffsetY
                )
            );
        }

        foreach (var hut in buildings?.Where(building => building.Type == Building.WoodcutterType) ?? [])
        {
            // A hut is drawn like the castle: at its node in the order of its row. inferred: the alignment is that of an
            // object, as for the castle.
            var (x, y) = Camera.NodePosition(hut.X, hut.Y);
            var height = heights[hut.Y * width + hut.X] & Map.HeightMask;
            var index = draws.FindIndex(draw => draw.Row > hut.Y);
            draws.Insert(
                index < 0 ? draws.Count : index,
                new ObjectDraw(
                    hut.Y,
                    ObjectSprites.WoodcutterEntry,
                    x + OffsetX,
                    y - height * TerrainTriangles.HeightStep + OffsetY
                )
            );
        }

        return draws;
    }
}
