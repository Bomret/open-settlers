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
/// What a building may be. 0: none. 1: a flag only. 2: a mountain site (no building of the plain; inferred: a
/// mine). 3: a small building. 4: a large building. 5: the site of the first castle, which is judged like a large
/// building (the player owns nothing yet).
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
    // The spiral entries that the evaluator reads: the node itself and rings 1, 2, and 3.
    private const int Nodes = 37;

    /// <summary>
    /// Judges a node for a player. <paramref name="hasCastle"/> tells whether the player's castle stands; the
    /// original then compares owners with the player's own and ends in site 4 where it ends in site 5 without one.
    /// A node whose object is a flag or a building gives the result 0, 0, 0: the original judges those from its
    /// flag and building tables, which this port does not hold yet. <paramref name="buildings"/> is the building table, which a
    /// large site that waits for its digger within the third ring of the node is read from (<c>docs/rules/digging.md</c>); none
    /// reads no such site.
    /// </summary>
    public static BuildSiteResult Evaluate(
        RuleTables tables,
        Map map,
        int x,
        int y,
        int player,
        bool hasCastle,
        IReadOnlyList<Building>? buildings = null
    )
    {
        var owner = hasCastle ? Owner.Player(player) : Owner.Nobody;

        var nodes = new Node[Nodes];
        for (var i = 0; i < Nodes; i++)
        {
            var (dx, dy) = Spiral.Offsets[i];
            nodes[i] = map.At(x + dx, y + dy);
        }

        ObjectClass Class(int i) => tables.ClassOf(nodes[i].Object);
        Terrain Up(int i) => nodes[i].Upper;
        Terrain Low(int i) => nodes[i].Lower;
        int Height(int i) => nodes[i].Height;

        if (nodes[0].Owner != owner)
            return default;

        var own = Class(0);
        if (own.IsFlagOrBuilding)
            return default;

        // Node: how the node itself stands.
        int node;
        var edge = nodes[0].Roads;
        if (edge.IsEmpty)
        {
            node =
                Class(2).IsFlag ? 5
                : nodes[2].Roads.IsEmpty ? 7
                : 6;
        }
        else if (edge.IsOnly(1) || edge.IsOnly(4))
        {
            return default;
        }
        else
        {
            node = 4;
        }

        var site = 0;
        if (!own.IsEmpty)
            return new(node, 0, 0);

        if (Up(0).IsWater && Low(0).IsWater && Low(4).IsWater && Up(5).IsWater && Low(5).IsWater && Up(6).IsWater)
            return new(node, 0, 0);

        var flagNear = false;
        for (var i = 1; i <= 6; i++)
        {
            if (Class(i).IsFlag)
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
            if (Class(i).IsBuilding)
                return new(node, site, 0);
        }

        if (node != 5 && !Class(2).IsEmpty)
            return new(node, site, 0);

        foreach (var i in new[] { 7, 8, 14, 1, 3 })
        {
            if (Class(i).IsFlag)
                return new(node, site, 0);
        }

        if (Up(1).IsWater || Low(3).IsWater || Up(2).IsWater || Low(2).IsWater)
            return new(node, site, 0);

        for (var i = 1; i <= 6; i++)
        {
            if (nodes[i].Owner != owner)
                return new(node, site, 0);
        }

        // The six triangles around the node: plain land and mountain can be built on, and no mixture of the two;
        // the other terrain (water, snow, swamp) cannot (0x44be72).
        var kinds = 0;
        foreach (var terrain in new[] { Up(0), Low(0), Low(4), Up(5), Low(5), Up(6) })
        {
            kinds |= terrain.Kind switch
            {
                TerrainKind.Plain => 0,
                TerrainKind.Mountain => 1,
                _ => 2,
            };
        }

        if (kinds > 1)
            return new(node, site, 0);

        if (kinds != 0)
        {
            return new(node, hasCastle ? 2 : site, 0);
        }

        if (hasCastle)
            site = 3;

        for (var i = 1; i <= 6; i++)
        {
            var c = Class(i);
            if (c.IsLarge && !c.IsFlag)
                return new(node, site, 0);
        }

        for (var i = 7; i <= 18; i++)
        {
            if (Class(i).IsLargeBuilding)
                return new(node, site, 0);
        }

        if (
            Up(0) != Terrain.Lowland
            || Low(0) != Terrain.Lowland
            || Low(4) != Terrain.Lowland
            || Up(5) != Terrain.Lowland
            || Low(5) != Terrain.Lowland
            || Up(6) != Terrain.Lowland
        )
            return new(node, site, 0);

        // The heights of the 12 nodes of the second ring bound the levelled height, and so do the target heights of the large building
        // sites that wait for their digger within the third ring (0x44c151 to 0x44c204, in 0x44bd72): a node holding the object code 3
        // whose building is under construction and whose progress word is still 0.
        int low = 0x1f,
            high = 0;
        for (var i = 7; i <= 18; i++)
        {
            var h = Height(i);
            low = Math.Min(low, h);
            high = Math.Max(high, h);
        }

        for (var i = 19; i < Nodes && buildings is not null; i++)
        {
            if (
                nodes[i].Object == MapObject.LargeHut
                && nodes[i].LinkedSlot is var slot
                && slot < buildings.Count
                && buildings[slot] is { Constructing: true, Progress: 0, Site: { } waiting }
            )
            {
                low = Math.Min(low, waiting.TargetHeight);
                high = Math.Max(high, waiting.TargetHeight);
            }
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
}
