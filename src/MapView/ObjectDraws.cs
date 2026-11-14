using System.Collections.Generic;
using System.Linq;
using OpenSettlers.Domain;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>Where each generated object lies on the map (<c>docs/rules/map-objects.md</c>), as draw terms.</summary>
public static class ObjectDraws
{
    // The places of a flag's goods (bytes 0xc to 0x13 of the flag item) and where each is drawn, in pixels from the flag's
    // draw position (0x44db26 adds them to the node's x and y; 0x44de9a draws the good at the object's position). The
    // places 0 to 2 are drawn before the flag, the others after it.
    static readonly (int X, int Y)[] GoodPlaces =
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

    const int GoodsBeforeFlag = 3;

    /// <summary>
    /// The geometry of a flag for a click (<see cref="MapPicking.Pick"/>): the box of the History Edition's flag at its
    /// first animation step, 16 by 19 pixels with its top 18 above the node. It is the same under every graphics source
    /// so that a click names the same node, and so the same command, under each (INV-0009).
    /// </summary>
    public static MapPicking.PickBox FlagGeometry { get; } = new(16, 19, 0, -18);

    /// <summary>The goods that lie on the flags of a game, by the flag's node; a flag with no good is left out.</summary>
    public static Dictionary<(int X, int Y), IReadOnlyList<Good>> FlagGoodsOf(Game game) =>
        game
            .Flags.Where(flag => !flag.IsFree && flag.Places.Any(place => !place.IsEmpty))
            .ToDictionary(
                flag => (flag.X, flag.Y),
                flag => (IReadOnlyList<Good>)[.. flag.Places.Select(place => place.Good)]
            );

    /// <summary>
    /// The objects of a map in row-major order, which is the order the original paints them. A draw sits at its
    /// tile's node with the node's height taken off, where its roads meet (<c>docs/rules/map-objects.md</c>); the
    /// sprite's own header offsets apply where the sprite is drawn. Every object is a term, and a source that has no
    /// image for one draws nothing for it. Resources are no objects and are never drawn. With a
    /// <paramref name="castle"/>, it is drawn at its node, and each woodcutter's hut of <paramref name="buildings"/>
    /// likewise. An animated object draws its frame at the frame counter <paramref name="counter"/>.
    /// </summary>
    public static List<ObjectTerm> Of(
        Map map,
        Castle? castle = null,
        IReadOnlyList<Building>? buildings = null,
        IReadOnlyDictionary<(int X, int Y), IReadOnlyList<Good>>? flagGoods = null,
        ushort counter = 0
    )
    {
        var objects = map.Objects;
        var draws = new List<ObjectTerm>(objects.Count);
        foreach (var item in objects)
        {
            var (x, y) = Camera.NodePosition(item.X, item.Y);
            var height = map.At(item.X, item.Y).Height;
            var top = y - height * TerrainTriangles.HeightStep;
            var goods =
                item.Object == MapObject.Flag && flagGoods is not null
                    ? flagGoods.GetValueOrDefault((item.X, item.Y))
                    : null;
            AddGoods(draws, goods, item.Y, x, top, 0, GoodsBeforeFlag);
            draws.Add(new MapObjectDraw(item.Y, x, top, item.Object, counter));
            AddGoods(draws, goods, item.Y, x, top, GoodsBeforeFlag, GoodPlaces.Length);
        }

        if (castle is { } placed)
        {
            // The castle is no map object; it is drawn at its node in the order of its row, after the objects of its
            // row. inferred: its alignment to the node is that of an object, which the image's own offsets then adjust.
            var (x, y) = Camera.NodePosition(placed.X, placed.Y);
            var height = map.At(placed.X, placed.Y).Height;
            var index = draws.FindIndex(draw => draw.Row > placed.Y);
            draws.Insert(
                index < 0 ? draws.Count : index,
                new BuildingDraw(placed.Y, x, y - height * TerrainTriangles.HeightStep, BuildingType.Castle)
            );
        }

        // A building that was taken down burns (docs/rules/removal.md); the burning is not drawn (the sprites of the fire are not
        // read).
        foreach (
            var hut in buildings?.Where(building =>
                BuildingPlacement.Types.Contains(building.Type) && !building.IsDemolishing
            )
                ?? []
        )
        {
            // A building is drawn like the castle: at its node in the order of its row. inferred: the alignment is that of an
            // object, as for the castle. A site is drawn by its progress word (0x44e329 reads it at 0x44fa7c).
            var (x, y) = Camera.NodePosition(hut.X, hut.Y);
            var height = map.At(hut.X, hut.Y).Height;
            var index = draws.FindIndex(draw => draw.Row > hut.Y);
            draws.Insert(
                index < 0 ? draws.Count : index,
                new BuildingDraw(
                    hut.Y,
                    x,
                    y - height * TerrainTriangles.HeightStep,
                    hut.Type,
                    hut.Constructing ? hut.Progress : null
                )
            );
        }

        return draws;
    }

    /// <summary>
    /// The water ripples of a map in row-major order: one on each tile with a water triangle, drawn by <c>0x44ff59</c> at the tile's horizontal field times 8 and its row's top plus 8, with no
    /// height. That is 16 pixels west of where an object of the tile at height 0 is drawn, so in the engine's layout the
    /// node's position less 16 in x. <b>Inferred:</b> the alignment, as for the objects.
    /// </summary>
    public static List<WaterRipple> WaterOf(Map map, ushort counter)
    {
        var width = map.Size.Width;
        var draws = new List<WaterRipple>();
        for (var tile = 0; tile < map.Count; tile++)
        {
            if (map.At(tile).WaterTriangle)
            {
                var (x, y) = Camera.NodePosition(tile % width, tile / width);
                draws.Add(new WaterRipple(tile / width, x - 16, y, tile, counter));
            }
        }

        return draws;
    }

    static void AddGoods(List<ObjectTerm> draws, IReadOnlyList<Good>? goods, int row, int x, int y, int from, int to)
    {
        for (var place = from; place < to && goods is not null && place < goods.Count; place++)
        {
            if (!goods[place].IsNone)
            {
                draws.Add(new GoodAtFlag(row, x + GoodPlaces[place].X, y + GoodPlaces[place].Y, goods[place]));
            }
        }
    }
}
