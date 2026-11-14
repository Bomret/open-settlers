using System;
using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>The mask and the texture the original draws a road strip with (entries of <c>loca/SPAD.PA</c>).</summary>
public readonly record struct RoadDraw(int Mask, int Texture);

/// <summary>
/// How the original picks the mask and the texture of a road strip (<c>docs/rules/roads.md</c>): the strip routines
/// <c>0x428008</c>, <c>0x428105</c>, and <c>0x4281cc</c>. The History Edition source applies it to a
/// <see cref="RoadStrip"/>.
/// </summary>
public static class RoadRules
{
    // The first mask entry of a strip for the height difference 0: the east strips take 230 to 238, the south-east
    // 239 to 247, and the south 248 to 256, a difference of -4 to 4 each.
    const int EastMask = 234;
    const int SouthEastMask = 243;
    const int SouthMask = 252;
    const int MaxDifference = 4;

    // The texture entry for a slope of 5 or more, and the entries of the other two slopes follow it; the entry
    // of a strip beside water; and the steps up from the land textures to those of mountain and of snow.
    const int SteepTexture = 300;
    const int WaterTexture = 309;
    const int MountainStep = 3;
    const int SnowStep = 6;

    /// <summary>
    /// The draw of a strip: the mask of its direction and height difference, and the texture of its slope and ground.
    /// Null where the difference is more than 4 either way: the archive has no mask for it.
    /// </summary>
    public static RoadDraw? Of(RoadStrip strip)
    {
        if (Math.Abs(strip.Difference) > MaxDifference)
        {
            return null;
        }

        var maskBase = strip.Direction switch
        {
            RoadDirection.East => EastMask,
            RoadDirection.SouthEast => SouthEastMask,
            _ => SouthMask,
        };
        var texture =
            strip.Slope >= 5 ? SteepTexture
            : strip.Slope >= -5 ? SteepTexture + 1
            : SteepTexture + 2;
        if (strip.Ground.IsWater)
        {
            texture = WaterTexture;
        }
        else if (strip.Ground.IsAbovePlain)
        {
            texture += strip.Ground.IsSnow ? SnowStep : MountainStep;
        }

        return new RoadDraw(maskBase + strip.Difference, texture);
    }
}
