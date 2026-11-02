namespace OpenSettlers.Simulation;

/// <summary>
/// What placing the first castle writes into the map (<c>0x442704</c>, <c>docs/rules/castle.md</c>): the castle on
/// its node, its flag south-east of it, the levelled heights around it, and the player's territory.
/// </summary>
internal static class CastlePlacement
{
    // The territory is the castle's node and every node within 8 steps: the first 217 entries of the spiral.
    private const int TerritoryNodes = 217;

    // The object codes the placement writes: a flag and the castle.
    private const int FlagCode = 1;
    private const int CastleCode = 4;

    /// <summary>
    /// Writes the placement of the player's first castle at a node judged a castle site, levelled to
    /// <paramref name="height"/>. The record that links a node to its building or flag holds the index of the
    /// first item of each table, 0, in the byte the resource layer shares.
    /// </summary>
    internal static void Apply(Map map, int x, int y, int height, int player)
    {
        var flags = map.Bytes(MapLayer.Flags);
        var heights = map.Bytes(MapLayer.Height);
        var objects = map.Bytes(MapLayer.Object);
        var links = map.Bytes(MapLayer.Resource);

        var castle = map.Index(x, y);
        flags[castle] |= 0x42;
        objects[castle] = (byte)((objects[castle] & 0x80) | CastleCode);
        links[castle] = 0;

        var flag = map.Index(x + 1, y + 1);
        flags[flag] |= 0x90;
        objects[flag] = (byte)((objects[flag] & 0x80) | FlagCode);
        links[flag] = 0;

        // The node and its six neighbors, the first seven entries of the spiral, all take the site's height.
        for (var i = 0; i < 7; i++)
        {
            var (dx, dy) = MapGenerator.SpiralOffsets[i];
            var node = map.Index(x + dx, y + dy);
            heights[node] = (byte)((heights[node] & 0xe0) | height);
        }

        // The owner of a node is kept in the top three bits of its height byte: the player's number plus 4.
        var owner = (player + 4) << 5;
        for (var i = 0; i < TerritoryNodes; i++)
        {
            var (dx, dy) = MapGenerator.SpiralOffsets[i];
            var node = map.Index(x + dx, y + dy);
            if ((heights[node] & 0xe0) == 0)
            {
                heights[node] |= (byte)owner;
            }
        }
    }
}
