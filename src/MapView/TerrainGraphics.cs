using System;
using System.Collections.Generic;
using System.IO;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>A fill mask as one layer of an array texture: <see cref="Width"/> by <see cref="Height"/> pixels at the top left.</summary>
public sealed record MaskImage(int Width, int Height, int OffsetX, int OffsetY, byte[] Layer);

/// <summary>
/// The terrain graphics of an install as the GPU takes them: each fill mask
/// (entries 60 to 221, an empty layer where the archive has none and no triangle needs it) as a <see cref="LayerWidth"/> x <see cref="MaskLayerHeight"/> layer holding 255 on a covered
/// pixel, and each fill texture (entries 260 to 292) as a 32 x 20 layer of RGBA pixels, the palette applied at load.
/// </summary>
public sealed class TerrainGraphics
{
    /// <summary>The archive that holds the graphics (the German one; the three carry the same terrain).</summary>
    public const string ArchiveFile = "loca/SPAD.PA";

    /// <summary>The first and last entry of the textured-fill masks the engine draws.</summary>
    public const int FirstMask = 60;

    /// <inheritdoc cref="FirstMask"/>
    public const int LastMask = 221;

    /// <summary>The first and last entry of the fill textures.</summary>
    public const int FirstTexture = 260;

    /// <inheritdoc cref="FirstTexture"/>
    public const int LastTexture = 292;

    /// <summary>The width of a mask layer and of a texture layer.</summary>
    public const int LayerWidth = 32;

    /// <summary>The height of a mask layer: the tallest mask of the range (a triangle of 41 rows).</summary>
    public const int MaskLayerHeight = 41;

    /// <summary>The height of a texture layer; the texture repeats down after this many rows.</summary>
    public const int TextureHeight = 20;

    /// <summary>The masks from <see cref="FirstMask"/> on, one per entry.</summary>
    public required IReadOnlyList<MaskImage> Masks { get; init; }

    /// <summary>The textures from <see cref="FirstTexture"/> on, 32 x 20 RGBA pixels each, four bytes a pixel, all opaque.</summary>
    public required IReadOnlyList<byte[]> Textures { get; init; }

    /// <summary>The layer of a mask entry.</summary>
    public static int MaskLayer(int entry) => entry - FirstMask;

    /// <summary>The layer of a texture entry.</summary>
    public static int TextureLayer(int entry) => entry - FirstTexture;

    /// <summary>
    /// Reads the graphics from the install. Throws <see cref="FileNotFoundException"/> for a missing archive and
    /// <see cref="InvalidDataException"/>, naming the entry and the reason, for an entry that is absent or unreadable
    /// or a graphic of a shape the drawing cannot take.
    /// </summary>
    public static TerrainGraphics Load(Install install)
    {
        if (!install.Exists(ArchiveFile))
        {
            throw new FileNotFoundException($"the archive {ArchiveFile} is missing: the install must hold it");
        }

        var archive = Formats.ArchiveFile.Read(install.ReadAll(ArchiveFile));
        var palette = Entry(archive, PaletteKind.GamePalette, "the palette", data => PaletteFile.Read(data.Span));
        var masks = new List<MaskImage>();
        for (var entry = FirstMask; entry <= LastMask; entry++)
        {
            // The archive leaves out the masks no combination of heights reaches.
            masks.Add(
                !TerrainRules.UsesMask(entry) && archive[entry].State == EntryState.Absent
                    ? new MaskImage(0, 0, 0, 0, new byte[LayerWidth * MaskLayerHeight])
                    : Entry(archive, entry, "the mask", data => ToLayer(MaskFile.Read(data)))
            );
        }

        var textures = new List<byte[]>();
        for (var entry = FirstTexture; entry <= LastTexture; entry++)
        {
            textures.Add(Entry(archive, entry, "the texture", data => ToTexture(GraphicFile.ReadRaw(data), palette)));
        }

        return new TerrainGraphics { Masks = masks, Textures = textures };
    }

    internal static T Entry<T>(Archive archive, int number, string what, Func<ReadOnlyMemory<byte>, T> read)
    {
        try
        {
            var entry = archive[number];
            if (entry.State == EntryState.Absent)
            {
                throw new InvalidDataException("the archive has no such entry");
            }

            return read(archive.Data(entry));
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentOutOfRangeException)
        {
            throw new InvalidDataException($"{what} (entry {number}) of {ArchiveFile} is unreadable: {e.Message}", e);
        }
    }

    internal static MaskImage ToLayer(Mask mask)
    {
        if (mask.Width > LayerWidth || mask.Height > MaskLayerHeight)
        {
            throw new InvalidDataException(
                $"the mask is {mask.Width} x {mask.Height}, larger than the {LayerWidth} x {MaskLayerHeight} the drawing takes"
            );
        }

        var layer = new byte[LayerWidth * MaskLayerHeight];
        for (var y = 0; y < mask.Height; y++)
        {
            for (var x = 0; x < mask.Width; x++)
            {
                layer[y * LayerWidth + x] = mask.Covered[y * mask.Width + x] ? (byte)255 : (byte)0;
            }
        }

        return new MaskImage(mask.Width, mask.Height, mask.Header.OffsetX, mask.Header.OffsetY, layer);
    }

    internal static byte[] ToTexture(RawImage image, Palette palette)
    {
        if (image.Header.Width != LayerWidth || image.Header.Height != TextureHeight)
        {
            throw new InvalidDataException(
                $"the texture is {image.Header.Width} x {image.Header.Height}, not the {LayerWidth} x {TextureHeight} the drawing takes"
            );
        }

        var rgba = new byte[image.Pixels.Length * 4];
        for (var i = 0; i < image.Pixels.Length; i++)
        {
            palette.Rgba(image.Pixels.Span[i]).CopyTo(rgba.AsSpan(i * 4, 4));
        }

        return rgba;
    }
}
