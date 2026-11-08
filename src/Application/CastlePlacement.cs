namespace OpenSettlers.Simulation;

/// <summary>
/// What placing the first castle writes into the map (<c>0x442704</c>, <c>docs/rules/castle.md</c>): the castle on
/// its node, its flag south-east of it, the levelled heights around it, and the player's territory.
/// </summary>
internal static class CastlePlacement
{
    // The territory is the castle's node and every node within 8 steps: the first 217 entries of the spiral.
    private const int TerritoryNodes = 217;

    /// <summary>
    /// Writes the placement of the player's first castle at a node judged a castle site, levelled to
    /// <paramref name="height"/>. The record that links a node to its building or flag holds the index of the
    /// first item of each table, 0.
    /// </summary>
    internal static void Apply(Map map, int x, int y, int height, int player)
    {
        // The castle's node carries a large object and the road to its flag; the flag's node holds the flag's mark and
        // the road back to the castle.
        ref var castle = ref map.At(x, y);
        castle.Roads = castle.Roads.With(1);
        castle.LakeMark = true;
        castle.Object = MapObject.Castle;
        castle.LinkTo(0);

        ref var flag = ref map.At(x + 1, y + 1);
        flag.Roads = flag.Roads.With(4);
        flag.FlagMark = true;
        flag.Object = MapObject.Flag;
        flag.LinkTo(0);

        // The node and its six neighbors, the first seven entries of the spiral, all take the site's height.
        for (var i = 0; i < 7; i++)
        {
            var (dx, dy) = Spiral.Offsets[i];
            map.At(x + dx, y + dy).Height = height;
        }

        var owner = Owner.Player(player);
        for (var i = 0; i < TerritoryNodes; i++)
        {
            var (dx, dy) = Spiral.Offsets[i];
            ref var node = ref map.At(x + dx, y + dy);
            if (node.Owner.IsNobody)
            {
                node.Owner = owner;
            }
        }
    }
}
