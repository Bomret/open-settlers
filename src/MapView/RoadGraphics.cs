using System.Collections.Generic;
using System.IO;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// The road graphics of an install as the GPU takes them, the way <see cref="TerrainGraphics"/> takes the terrain's:
/// each strip mask (entries 230 to 256; <see cref="RoadDraws"/> says which) as a layer of
/// <see cref="TerrainGraphics.LayerWidth"/> x <see cref="TerrainGraphics.MaskLayerHeight"/>, and each road texture (entries
/// 300 to 309) as a 32 x 20 layer of palette indices. The palette is the terrain's.
/// </summary>
public sealed class RoadGraphics
{
    /// <summary>The first and last entry of the strip masks.</summary>
    public const int FirstMask = 230;

    /// <inheritdoc cref="FirstMask"/>
    public const int LastMask = 256;

    /// <summary>The first and last entry of the road textures.</summary>
    public const int FirstTexture = 300;

    /// <inheritdoc cref="FirstTexture"/>
    public const int LastTexture = 309;

    /// <summary>The masks from <see cref="FirstMask"/> on, one per entry.</summary>
    public required IReadOnlyList<MaskImage> Masks { get; init; }

    /// <summary>The textures from <see cref="FirstTexture"/> on, 32 x 20 palette indices each.</summary>
    public required IReadOnlyList<byte[]> Textures { get; init; }

    /// <summary>The layer of a mask entry.</summary>
    public static int MaskLayer(int entry) => entry - FirstMask;

    /// <summary>The layer of a texture entry.</summary>
    public static int TextureLayer(int entry) => entry - FirstTexture;

    /// <summary>
    /// Reads the road graphics from the archive of <see cref="TerrainGraphics"/>. Throws
    /// <see cref="FileNotFoundException"/> for a missing archive and <see cref="InvalidDataException"/>, naming the
    /// entry and the reason, for an entry that is absent or unreadable or of a shape the drawing cannot take.
    /// </summary>
    public static RoadGraphics Load(Install install)
    {
        if (!install.Exists(TerrainGraphics.ArchiveFile))
        {
            throw new FileNotFoundException(
                $"the archive {TerrainGraphics.ArchiveFile} is missing: the install must hold it"
            );
        }

        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var masks = new List<MaskImage>();
        for (var entry = FirstMask; entry <= LastMask; entry++)
        {
            masks.Add(
                TerrainGraphics.Entry(
                    archive,
                    entry,
                    "the road mask",
                    data => TerrainGraphics.ToLayer(MaskFile.Read(data))
                )
            );
        }

        var textures = new List<byte[]>();
        for (var entry = FirstTexture; entry <= LastTexture; entry++)
        {
            textures.Add(
                TerrainGraphics.Entry(
                    archive,
                    entry,
                    "the road texture",
                    data => TerrainGraphics.ToTexture(GraphicFile.ReadRaw(data))
                )
            );
        }

        return new RoadGraphics { Masks = masks, Textures = textures };
    }

    /// <summary>
    /// The rectangles to draw for the strips: each mask hangs from the strip's position by its header offsets. A strip
    /// whose mask is empty is left out.
    /// </summary>
    public List<TerrainQuad> Quads(IReadOnlyList<RoadStrip> strips)
    {
        var quads = new List<TerrainQuad>(strips.Count);
        foreach (var strip in strips)
        {
            var maskLayer = MaskLayer(strip.Mask);
            var mask = Masks[maskLayer];
            if (mask.Width > 0 && mask.Height > 0)
            {
                quads.Add(
                    new TerrainQuad(
                        strip.X + mask.OffsetX,
                        strip.Y + mask.OffsetY,
                        mask.Width,
                        mask.Height,
                        maskLayer,
                        TextureLayer(strip.Texture)
                    )
                );
            }
        }

        return quads;
    }
}
