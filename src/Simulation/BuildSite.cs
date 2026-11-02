using System;
using System.Collections.Generic;

namespace OpenSettlers.Simulation;

/// <summary>
/// What the build-site evaluator leaves for a node (<c>docs/rules/build-sites.md</c>): the three values the
/// original writes into the player block at <c>+0x100</c>, <c>+0x101</c>, and <c>+0x102</c>.
/// </summary>
/// <param name="Node">
/// How the node itself stands. 0: nothing was judged, because the node belongs to another owner or holds an
/// object. 4: the node has coast or other flag bits. 5: a flag already stands south-east of it. 6: the
/// south-east node has flag bits. 7: the node and the node south-east of it are free.
/// </param>
/// <param name="Site">
/// What a building may be. 0: none. 1: a flag only. 3: a small building. 4: a large building. 5: the site of the
/// first castle, which is judged like a large building (the player owns nothing yet).
/// </param>
/// <param name="Height">
/// The height the building site is levelled to; set with <paramref name="Site"/> 4 or 5, else 0.
/// </param>
public readonly record struct BuildSiteResult(int Node, int Site, int Height)
{
    /// <summary>True when the first castle may stand on the node: node state 7 and site 5.</summary>
    public bool IsCastleSite => Node == 7 && Site == 5;

    /// <summary>True when a flag may be put on the node: a site that is not 0 and node state 4, 6, or 7 (<c>0x442244</c>).</summary>
    public bool AllowsFlag => Site != 0 && Node is 4 or 6 or 7;
}

/// <summary>
/// The build-site evaluator: for a node, whether a flag, a building, or the first castle may be put there. It ports
/// <c>0x44b6e6</c> with its helpers <c>0x44ba52</c>, <c>0x44bd72</c>, and <c>0x44be72</c> for a node that holds no
/// object, which covers every node where a flag or a castle goes. It reads the 37 nodes within two steps of the
/// node (the first three rings of the spiral, <c>0x41f6a5</c>), their flags, heights, owners, terrain, and object
/// classes.
/// </summary>
public static class BuildSite
{
    // The class of an object code: the size byte of the program's table (0x41e640). 1 is a flag, 2 to 4 are
    // buildings (4 is the castle), the trees and stones are 1 and 2, 127 marks a border.
    private static readonly byte[] Classes = BuildClasses();

    // The spiral entries that the evaluator reads: the node itself and rings 1, 2, and 3.
    private const int Nodes = 37;

    /// <summary>
    /// Judges a node for a player. <paramref name="hasCastle"/> tells whether the player's castle stands; the
    /// original then compares owners with the player's own and ends in site 4 where it ends in site 5 without one.
    /// A node whose object is a flag or a building gives the result 0, 0, 0: the original judges those from its
    /// flag and building tables, which this port does not hold yet.
    /// </summary>
    public static BuildSiteResult Evaluate(Map map, int x, int y, int player, bool hasCastle)
    {
        var width = map.Size.Width;
        var flags = map.Bytes(MapLayer.Flags);
        var heights = map.Bytes(MapLayer.Height);
        var terrain = map.Bytes(MapLayer.Terrain);
        var objects = map.Bytes(MapLayer.Object);
        var owner = hasCastle ? (player + 4) << 5 : 0;

        var p = new int[Nodes];
        for (var i = 0; i < Nodes; i++)
        {
            var (dx, dy) = MapGenerator.SpiralOffsets[i];
            p[i] = map.Index(x + dx, y + dy);
        }

        int Class(int i) => Classes[objects[p[i]] & 0x7f];
        int Terrain(int i) => terrain[p[i]];
        int Height(int i) => heights[p[i]] & 0x1f;

        if ((heights[p[0]] & 0xe0) != owner)
            return default;

        var own = Class(0);
        if (own >= 3)
            return default;

        // Node: how the node itself stands.
        int node;
        var edge = flags[p[0]] & 0x3f;
        if (edge == 0)
        {
            node = Class(2) == 3 ? 5 : (flags[p[2]] & 0x3f) == 0 ? 7 : 6;
        }
        else if (edge is 2 or 0x10)
        {
            return default;
        }
        else
        {
            node = 4;
        }

        var site = 0;
        if (own != 0)
            return new(node, 0, 0);

        if ((Terrain(0) & 0xcc) == 0 && (Terrain(4) & 0x0c) == 0 && (Terrain(5) & 0xcc) == 0 && (Terrain(6) & 0xc0) == 0)
            return new(node, 0, 0);

        var flagNear = false;
        for (var i = 1; i <= 6; i++)
        {
            if (Class(i) == 3)
            {
                if (node == 4)
                    return new(node, 0, 0);

                flagNear = true;
                break;
            }
        }

        if (!flagNear && hasCastle)
        {
            site = 1;
            if (node == 4)
                return new(node, site, 0);
        }

        for (var i = 1; i <= 6; i++)
        {
            if (Class(i) > 3)
                return new(node, site, 0);
        }

        if (node != 5 && Class(2) != 0)
            return new(node, site, 0);

        foreach (var i in new[] { 7, 8, 14, 1, 3 })
        {
            if (Class(i) == 3)
                return new(node, site, 0);
        }

        if ((Terrain(1) & 0xc0) == 0 || (Terrain(3) & 0x0c) == 0 || (Terrain(2) & 0xc0) == 0 || (Terrain(2) & 0x0c) == 0)
            return new(node, site, 0);

        for (var i = 1; i <= 6; i++)
        {
            if ((heights[p[i]] & 0xe0) != owner)
                return new(node, site, 0);
        }

        // The six triangles around the node: terrain codes 4 to 7 are plain land, 11 to 14 mountain, others
        // (water, snow, swamp) cannot be built on (0x44be72).
        var kinds = 0;
        foreach (var nibble in new[]
        {
            Terrain(0) >> 4,
            Terrain(0) & 0xf,
            Terrain(4) & 0xf,
            Terrain(5) >> 4,
            Terrain(5) & 0xf,
            Terrain(6) >> 4,
        })
        {
            kinds |= nibble is >= 4 and < 8 ? 0 : nibble is > 10 and < 15 ? 1 : 2;
        }

        if (kinds > 1)
            return new(node, site, 0);

        if (kinds != 0)
        {
            return new(node, hasCastle ? 0 : site, 0);
        }

        if (hasCastle)
            site = 3;

        for (var i = 1; i <= 6; i++)
        {
            var c = Class(i);
            if (c > 1 && c != 3)
                return new(node, site, 0);
        }

        for (var i = 7; i <= 18; i++)
        {
            if (Class(i) > 4)
                return new(node, site, 0);
        }

        if (Terrain(0) != 0x55 || (Terrain(4) & 0xf) != 5 || Terrain(5) != 0x55 || (Terrain(6) & 0xf0) != 0x50)
            return new(node, site, 0);

        // The heights of the 12 nodes of the second ring bound the levelled height. simplicity: the original also
        // folds in the height of a code 3 building within the third ring; no such object exists before the
        // construction slices, so that term is left out.
        int low = 0x1f,
            high = 0;
        for (var i = 7; i <= 18; i++)
        {
            var h = Height(i);
            low = Math.Min(low, h);
            high = Math.Max(high, h);
        }

        if (high - low >= 9)
            return new(node, site, 0);

        var sum = 2 * Height(0);
        for (var i = 1; i <= 6; i++)
        {
            sum += Height(i);
        }

        var level = sum >> 3;
        var ceiling = Math.Max(high - 4, 0);
        level = Math.Max(level, ceiling);
        level = Math.Min(level, low + 4);
        if (level == 0)
            level = 1;

        return new(node, hasCastle ? 4 : 5, level);
    }

    /// <summary>The class of the object in a tile's object byte (the low 7 bits are the code).</summary>
    internal static int ObjectClass(byte objectByte) => Classes[objectByte & 0x7f];

    private static byte[] BuildClasses()
    {
        var classes = new byte[128];
        foreach (var (first, last, value) in new (int, int, byte)[]
        {
            (1, 1, 3),
            (2, 2, 4),
            (3, 3, 5),
            (4, 4, 6),
            (8, 27, 1),
            (28, 31, 2),
            (72, 81, 2),
            (82, 82, 1),
            (88, 89, 2),
            (90, 96, 1),
            (98, 101, 1),
            (103, 104, 1),
            (105, 110, 2),
            (121, 126, 2),
            (127, 127, 255),
        })
        {
            Array.Fill(classes, value, first, last - first + 1);
        }

        return classes;
    }
}
