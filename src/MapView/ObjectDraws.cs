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

    // The places of a flag's goods (bytes 0xc to 0x13 of the flag item) and where each is drawn, in pixels from the flag's
    // draw position (0x44db26 adds them to the node's x and y; 0x44de9a adds the 16 and 8 of an object and draws the sprite
    // 320 plus the good). The places 0 to 2 are drawn before the flag, the others after it.
    private static readonly (int X, int Y)[] GoodPlaces =
    [
        (6, -4),
        (10, -2),
        (-4, -4),
        (10, 2),
        (-8, -2),
        (6, 4),
        (-8, 2),
        (-4, 4),
    ];

    private const int GoodsBeforeFlag = 3;

    /// <summary>The goods that lie on the flags of a game, by the flag's node; a flag with no good is left out.</summary>
    public static Dictionary<(int X, int Y), IReadOnlyList<int>> FlagGoodsOf(Game game) =>
        game
            .Flags.Select((flag, slot) => (flag, goods: game.FlagGoods(slot)))
            .Where(entry => entry.goods.Any(good => good != 0))
            .ToDictionary(entry => (entry.flag.X, entry.flag.Y), entry => entry.goods);

    /// <summary>
    /// The objects of a map in row-major order, which is the order the original paints them. A draw sits at its
    /// tile's node with the node's height taken off, moved by <see cref="OffsetX"/> and <see cref="OffsetY"/>; the
    /// sprite's own header offsets apply where the sprite is drawn. A code without a sprite is left out. Resources
    /// are no objects and are never drawn. With a <paramref name="castle"/>, its sprite is drawn at its node, and each
    /// woodcutter's hut of <paramref name="buildings"/> likewise.
    /// </summary>
    public static List<ObjectDraw> Of(
        Map map,
        Castle? castle = null,
        IReadOnlyList<Building>? buildings = null,
        IReadOnlyDictionary<(int X, int Y), IReadOnlyList<int>>? flagGoods = null
    )
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
            var top = y - height * TerrainTriangles.HeightStep + OffsetY;
            var goods =
                item.Code == ObjectSprites.FlagCode && flagGoods is not null
                    ? flagGoods.GetValueOrDefault((item.X, item.Y))
                    : null;
            AddGoods(draws, goods, item.Y, x + OffsetX, top, 0, GoodsBeforeFlag);
            draws.Add(new ObjectDraw(item.Y, entry, x + OffsetX, top));
            AddGoods(draws, goods, item.Y, x + OffsetX, top, GoodsBeforeFlag, GoodPlaces.Length);
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

    private static void AddGoods(
        List<ObjectDraw> draws,
        IReadOnlyList<int>? goods,
        int row,
        int x,
        int y,
        int from,
        int to
    )
    {
        for (var place = from; place < to && goods is not null && place < goods.Count; place++)
        {
            if (goods[place] != 0)
            {
                draws.Add(
                    new ObjectDraw(
                        row,
                        ObjectSprites.GoodEntry(goods[place]),
                        x + GoodPlaces[place].X,
                        y + GoodPlaces[place].Y
                    )
                );
            }
        }
    }
}
