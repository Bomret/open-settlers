using System;
using System.Collections.Generic;
using OpenSettlers.Simulation;

namespace OpenSettlers.MapView;

/// <summary>
/// One strip of road to draw: the position of the top left of its mask in map pixels (before the mask's own header
/// offset), the mask entry, and the texture entry, both of <c>loca/SPAD.PA</c>.
/// </summary>
public readonly record struct RoadStrip(int X, int Y, int Mask, int Texture);

/// <summary>
/// Where the roads of a map are drawn and with which mask and texture (<c>docs/rules/roads.md</c>). The original draws
/// a road a step at a time, as a strip of the road texture over the terrain, from each node that holds the bit of
/// the direction east, south-east, or south (<c>0x427d3e</c>, <c>0x427e3a</c>, <c>0x427f1f</c> with <c>0x428008</c>,
/// <c>0x428105</c>, and <c>0x4281cc</c>); the other three directions are drawn from the node at the other end.
/// </summary>
public static class RoadDraws
{
    // The first mask entry of a strip for the height difference 0: the east strips take 230 to 238, the south-east
    // 239 to 247, and the south 248 to 256, a difference of -4 to 4 each.
    private const int EastMask = 234;
    private const int SouthEastMask = 243;
    private const int SouthMask = 252;
    private const int MaxDifference = 4;

    // Pixels the strip reaches above its higher node (0x428008 subtracts 2 more after the height); the level strip is
    // 5 rows high, so it is centered on the line.
    private const int Above = 2;

    // The texture entry for a slope of 5 or more, and the entries of the other two slopes follow it; the entry
    // of a strip beside water; and the steps up from the land textures to those of mountain and of snow.
    private const int SteepTexture = 300;
    private const int WaterTexture = 309;
    private const int MountainStep = 3;
    private const int SnowStep = 6;

    /// <summary>
    /// The strips of the map's roads in row-major order of the node each is drawn from. A strip whose height difference
    /// is more than 4 either way has no mask in the archive and is left out, as the terrain masks are.
    /// </summary>
    public static List<RoadStrip> Of(Map map)
    {
        var flags = map.Layer(MapLayer.Flags);
        var strips = new List<RoadStrip>();
        var width = map.Size.Width;
        for (var row = 0; row < map.Size.Height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var bits = flags[row * width + column] & 7;
                for (var direction = 0; direction < 3; direction++)
                {
                    if ((bits >> direction & 1) != 0 && Strip(map, column, row, direction) is { } strip)
                    {
                        strips.Add(strip);
                    }
                }
            }
        }

        return strips;
    }

    // The strip of the road that leaves a node east, south-east, or south.
    private static RoadStrip? Strip(Map map, int column, int row, int direction)
    {
        var heights = map.Bytes(MapLayer.Height);
        var terrain = map.Bytes(MapLayer.Terrain);
        int H(int dx, int dy) => heights[map.Index(column + dx, row + dy)] & Map.HeightMask;
        int Low(int dx, int dy) => terrain[map.Index(column + dx, row + dy)] & 0xf;
        int High(int dx, int dy) => terrain[map.Index(column + dx, row + dy)] >> 4;

        var (x, y) = Camera.NodePosition(column, row);
        var here = H(0, 0);
        int difference,
            slope,
            ground,
            maskBase,
            top,
            left;
        switch (direction)
        {
            case 0:
                // East: the node and its east neighbour, the strip as high as the higher of them.
                difference = here - H(1, 0);
                slope = -3 * difference + H(0, -1) - H(1, 1);
                ground = Math.Max(Low(0, 0), High(0, -1));
                maskBase = EastMask;
                top = y - Math.Max(here, H(1, 0)) * TerrainTriangles.HeightStep;
                left = x;
                break;
            case 1:
                // South-east: the strip starts at the node's own height.
                difference = here - H(1, 1);
                slope = 2 * (H(1, 0) - H(0, 1));
                ground = Math.Max(Low(0, 0), High(0, 0));
                maskBase = SouthEastMask;
                top = y - here * TerrainTriangles.HeightStep;
                left = x;
                break;
            default:
                // South: the strip lies west of the node.
                difference = here - H(0, 1);
                slope = 3 * difference - H(-1, 0) + H(1, 1);
                ground = Math.Max(High(0, 0), Low(-1, 0));
                maskBase = SouthMask;
                top = y - here * TerrainTriangles.HeightStep;
                left = x - Camera.NodeSpacing / 2;
                break;
        }

        if (Math.Abs(difference) > MaxDifference)
        {
            return null;
        }

        var texture =
            slope >= 5 ? SteepTexture
            : slope >= -5 ? SteepTexture + 1
            : SteepTexture + 2;
        if (ground < 4)
        {
            texture = WaterTexture;
        }
        else if (ground > 7)
        {
            texture += ground < 14 ? MountainStep : SnowStep;
        }

        return new RoadStrip(left, top - Above, maskBase + difference, texture);
    }
}
