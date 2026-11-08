using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>The mask and the texture the original draws a terrain triangle with (entries of <c>loca/SPAD.PA</c>).</summary>
public readonly record struct TerrainDraw(int Mask, int Texture);

/// <summary>
/// How the original picks the mask and the texture of a terrain triangle (<c>docs/rules/graphics-images.md</c>,
/// "Terrain drawing"): <c>0x4278be</c> for the upper triangle of a tile and <c>0x427a25</c> for the lower. Both
/// read five-bit heights and form <c>(a + 4 - b) + 9 * (a' + 4 - b')</c> of the three nodes; the sum indexes a
/// table of 81 values that picks the texture variant, and the terrain of the triangle picks a row of eight.
/// </summary>
public static class TerrainRules
{
    const int FirstUpperMask = 60;
    const int FirstLowerMask = 141;
    const int FirstTexture = 260;

    // DAT_004279d4 and DAT_00427b38 of the program: 9 x 9 values, each a multiple of 4; 0xff is no texture.
    static readonly byte[] UpperVariants =
    [
        0x00,
        0x04,
        0x0c,
        0x18,
        0x1c,
        0xff,
        0xff,
        0xff,
        0xff,
        0x00,
        0x04,
        0x08,
        0x14,
        0x18,
        0x1c,
        0xff,
        0xff,
        0xff,
        0x00,
        0x04,
        0x08,
        0x0c,
        0x14,
        0x18,
        0x1c,
        0xff,
        0xff,
        0x00,
        0x04,
        0x08,
        0x0c,
        0x10,
        0x14,
        0x18,
        0x1c,
        0xff,
        0x00,
        0x04,
        0x08,
        0x0c,
        0x10,
        0x10,
        0x14,
        0x18,
        0x1c,
        0xff,
        0x00,
        0x04,
        0x08,
        0x0c,
        0x10,
        0x14,
        0x18,
        0x1c,
        0xff,
        0xff,
        0x00,
        0x04,
        0x08,
        0x10,
        0x14,
        0x18,
        0x1c,
        0xff,
        0xff,
        0xff,
        0x00,
        0x04,
        0x08,
        0x14,
        0x18,
        0x1c,
        0xff,
        0xff,
        0xff,
        0xff,
        0x00,
        0x04,
        0x10,
        0x18,
        0x1c,
    ];

    static readonly byte[] LowerVariants =
    [
        0x00,
        0x00,
        0x00,
        0x00,
        0x00,
        0xff,
        0xff,
        0xff,
        0xff,
        0x04,
        0x04,
        0x04,
        0x04,
        0x04,
        0x00,
        0xff,
        0xff,
        0xff,
        0x0c,
        0x08,
        0x08,
        0x08,
        0x08,
        0x04,
        0x00,
        0xff,
        0xff,
        0x18,
        0x14,
        0x0c,
        0x0c,
        0x0c,
        0x08,
        0x04,
        0x00,
        0xff,
        0x1c,
        0x18,
        0x14,
        0x10,
        0x10,
        0x0c,
        0x08,
        0x04,
        0x00,
        0xff,
        0x1c,
        0x18,
        0x14,
        0x10,
        0x10,
        0x10,
        0x08,
        0x04,
        0xff,
        0xff,
        0x1c,
        0x18,
        0x14,
        0x14,
        0x14,
        0x14,
        0x10,
        0xff,
        0xff,
        0xff,
        0x1c,
        0x18,
        0x18,
        0x18,
        0x18,
        0x18,
        0xff,
        0xff,
        0xff,
        0xff,
        0x1c,
        0x1c,
        0x1c,
        0x1c,
        0x1c,
    ];

    // The 128 bytes at 0x427b89, eight for each terrain code: the texture number minus 260; 0x20 is the water
    // texture, 0xff would be no texture (none of the 128 is). The variant (a multiple of 4) divided by 4 picks
    // the entry within a terrain's row.
    static readonly byte[] TextureOfTerrain = BuildTextureOfTerrain();

    /// <summary>
    /// The draw of the upper triangle of a tile, whose apex is the tile's node and whose base nodes are the nodes
    /// south and south-east of it, with the five low bits of their heights and the terrain code of the triangle.
    /// Null where a base node differs from the apex by more than four either way: the original has no mask for it.
    /// </summary>
    public static TerrainDraw? Upper(int apex, int left, int right, Terrain terrain) =>
        Pick(
            (apex & 31) + 4 - (left & 31) + 9 * ((apex & 31) + 4 - (right & 31)),
            UpperVariants,
            FirstUpperMask,
            terrain.Code
        );

    /// <summary>
    /// The draw of the lower triangle of a tile, whose apex is the node south-east of the tile's node and whose top
    /// nodes are the tile's node (left) and the node east of it (right), arguments as for <see cref="Upper"/>.
    /// </summary>
    public static TerrainDraw? Lower(int apex, int left, int right, Terrain terrain) =>
        Pick(
            (left & 31) + 4 - (apex & 31) + 9 * ((right & 31) + 4 - (apex & 31)),
            LowerVariants,
            FirstLowerMask,
            terrain.Code
        );

    /// <summary>
    /// Whether some triangle is drawn with this mask entry (60 to 221). The entries the tables never reach are
    /// absent from the archive.
    /// </summary>
    public static bool UsesMask(int entry) =>
        entry >= FirstLowerMask
            ? entry - FirstLowerMask is >= 0 and <= 80 && LowerVariants[entry - FirstLowerMask] != 0xff
            : entry - FirstUpperMask is >= 0 and <= 80 && UpperVariants[entry - FirstUpperMask] != 0xff;

    static TerrainDraw? Pick(int index, byte[] variants, int firstMask, int terrain)
    {
        if (index is < 0 or > 80 || variants[index] == 0xff)
        {
            return null;
        }

        var texture = TextureOfTerrain[(terrain & 15) * 8 + (variants[index] >> 2)];

        return new TerrainDraw(firstMask + index, FirstTexture + texture);
    }

    static byte[] BuildTextureOfTerrain()
    {
        var table = new byte[128];
        // Terrain codes 0 to 3: water. 4 to 7: variants 0 to 7. 8 to 10: 24 to 31. 11 to 13: 8 to 15. 14 and 15: 16 to 23.
        (int FirstCode, int LastCode, int FirstTexture)[] rows =
        [
            (0, 3, 0x20),
            (4, 7, 0),
            (8, 10, 0x18),
            (11, 13, 8),
            (14, 15, 0x10),
        ];
        foreach (var (first, last, start) in rows)
        {
            for (var code = first; code <= last; code++)
            {
                for (var k = 0; k < 8; k++)
                {
                    table[code * 8 + k] = (byte)(start == 0x20 ? 0x20 : start + k);
                }
            }
        }

        return table;
    }
}
