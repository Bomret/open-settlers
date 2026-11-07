using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// The object sprites of an install as the GPU takes them: the game palette as a lookup texture and each sprite
/// (entries 1250 to 1369, the sprites of codes 8 to 127) as an <see cref="ObjectImage"/>, padded to one
/// <see cref="LayerWidth"/> x <see cref="LayerHeight"/> layer so they fit one array texture.
/// </summary>
public sealed class ObjectGraphics
{
    /// <summary>
    /// The number of object sprite entries: those of codes 8 to 127. The castle's sprite follows them, then the flag's, the hut's, and the 32 goods'.
    /// </summary>
    public const int EntryCount = ObjectSprites.LastCode - ObjectSprites.FirstCode + 1;

    /// <summary>The palette of entry 3 as <see cref="PaletteTexture.Rgba"/>.</summary>
    public required byte[] PaletteRgba { get; init; }

    /// <summary>The widest sprite; the width of every layer.</summary>
    public required int LayerWidth { get; init; }

    /// <summary>The tallest sprite; the height of every layer.</summary>
    public required int LayerHeight { get; init; }

    /// <summary>
    /// One image per entry from <see cref="ObjectSprites.FirstEntry"/> on, then the castle's
    /// (<see cref="ObjectSprites.CastleEntry"/>), the flag's (<see cref="ObjectSprites.FlagEntry"/>), the hut's, and the
    /// goods' (<see cref="ObjectSprites.GoodsCount"/> from <see cref="ObjectSprites.GoodsFirstEntry"/>), and the water's
    /// (<see cref="ObjectSprites.WaterCount"/> from <see cref="ObjectSprites.WaterFirstEntry"/>), each padded to
    /// the layer size in its pixel arrays, and last the flag's further frames (<see cref="ObjectSprites.FlagFrames"/>).
    /// </summary>
    public required IReadOnlyList<ObjectImage> Images { get; init; }

    /// <summary>The widest shadow mask, at least 1; the width of every shadow layer.</summary>
    public int ShadowLayerWidth { get; init; }

    /// <summary>The tallest shadow mask, at least 1; the height of every shadow layer.</summary>
    public int ShadowLayerHeight { get; init; }

    /// <summary>
    /// The shadow mask of each sprite layer at the same index as its sprite (<see cref="ShadowLayer"/>), padded to
    /// <see cref="ShadowLayerWidth"/> x <see cref="ShadowLayerHeight"/>; empty where the sprite draws none. Empty for
    /// graphics that draw no object shadow.
    /// </summary>
    public IReadOnlyList<ObjectImage> ShadowImages { get; init; } = [];

    /// <summary>The number of sprite layers, and of shadow layers.</summary>
    public const int SpriteLayerCount =
        EntryCount + 3 + ObjectSprites.GoodsCount + ObjectSprites.WaterCount + ObjectSprites.FlagFrames - 1;

    /// <summary>
    /// The number <c>Gfx_DrawObjectSprite</c> (<c>0x44da4d</c>) adds to an object sprite entry for its shadow mask: it
    /// draws the mask of the number plus 1500 through <c>Gfx_DrawShadow</c> (<c>0x4061a0</c>), then the sprite of the
    /// number plus 1250.
    /// </summary>
    public const int ShadowOffset = 1500 - ObjectSprites.FirstEntry;

    /// <summary>
    /// The layer of the shadow of a sprite entry in <see cref="ShadowImages"/>: its sprite's layer. The goods and the
    /// water draw no shadow (<c>0x44de9a</c>, <c>0x44ff59</c> draw only the sprite), so their shadow layers are empty.
    /// </summary>
    public static int ShadowLayer(int entry) => Layer(entry);

    /// <summary>The layer of a sprite entry: the object entries in order, then the castle's, the flag's, the hut's, the goods', the water's, and the flag's further frames.</summary>
    public static int Layer(int entry) =>
        entry is > ObjectSprites.FlagEntry and < ObjectSprites.FlagEntry + ObjectSprites.FlagFrames
            ? EntryCount + 3 + ObjectSprites.GoodsCount + ObjectSprites.WaterCount + entry - ObjectSprites.FlagEntry - 1
        : entry == ObjectSprites.CastleEntry ? EntryCount
        : entry == ObjectSprites.FlagEntry ? EntryCount + 1
        : entry == ObjectSprites.WoodcutterEntry ? EntryCount + 2
        : entry is >= ObjectSprites.WaterFirstEntry and < ObjectSprites.WaterFirstEntry + ObjectSprites.WaterCount
            ? EntryCount + 3 + ObjectSprites.GoodsCount + entry - ObjectSprites.WaterFirstEntry
        : entry is >= ObjectSprites.GoodsFirstEntry and < ObjectSprites.GoodsFirstEntry + ObjectSprites.GoodsCount
            ? EntryCount + 3 + entry - ObjectSprites.GoodsFirstEntry
        : entry - ObjectSprites.FirstEntry;

    /// <summary>
    /// Reads the sprites from the install. Throws <see cref="FileNotFoundException"/> for a missing archive and
    /// <see cref="InvalidDataException"/>, naming the entry and the reason, for a sprite that is unreadable and for an
    /// archive with no object sprite at all. An entry the archive leaves out is an empty image: the objects the
    /// generation places do not reach every code.
    /// </summary>
    public static ObjectGraphics Load(Install install)
    {
        if (!install.Exists(TerrainGraphics.ArchiveFile))
        {
            throw new FileNotFoundException(
                $"the archive {TerrainGraphics.ArchiveFile} is missing: the install must hold it"
            );
        }

        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var palette = Read(archive, 3, "the palette", data => PaletteFile.Read(data.Span));
        var images = new List<ObjectImage>();
        for (var entry = ObjectSprites.FirstEntry; entry < ObjectSprites.FirstEntry + EntryCount; entry++)
        {
            images.Add(
                archive[entry].State == EntryState.Absent
                    ? ObjectImage.Empty
                    : Read(archive, entry, "the sprite", data => ObjectImage.From(SpriteFile.Read(data)))
            );
        }

        images.Add(
            archive[ObjectSprites.CastleEntry].State == EntryState.Absent
                ? ObjectImage.Empty
                : Read(
                    archive,
                    ObjectSprites.CastleEntry,
                    "the castle sprite",
                    data => ObjectImage.From(SpriteFile.Read(data))
                )
        );

        images.Add(
            archive[ObjectSprites.FlagEntry].State == EntryState.Absent
                ? ObjectImage.Empty
                : Read(
                    archive,
                    ObjectSprites.FlagEntry,
                    "the flag sprite",
                    data => ObjectImage.From(SpriteFile.Read(data))
                )
        );

        images.Add(
            archive[ObjectSprites.WoodcutterEntry].State == EntryState.Absent
                ? ObjectImage.Empty
                : Read(
                    archive,
                    ObjectSprites.WoodcutterEntry,
                    "the woodcutter's hut sprite",
                    data => ObjectImage.From(SpriteFile.Read(data))
                )
        );

        for (
            var entry = ObjectSprites.GoodsFirstEntry;
            entry < ObjectSprites.GoodsFirstEntry + ObjectSprites.GoodsCount;
            entry++
        )
        {
            images.Add(
                archive[entry].State == EntryState.Absent
                    ? ObjectImage.Empty
                    : Read(archive, entry, "the good's sprite", data => ObjectImage.From(SpriteFile.Read(data)))
            );
        }

        for (
            var entry = ObjectSprites.WaterFirstEntry;
            entry < ObjectSprites.WaterFirstEntry + ObjectSprites.WaterCount;
            entry++
        )
        {
            images.Add(
                archive[entry].State == EntryState.Absent
                    ? ObjectImage.Empty
                    : Read(archive, entry, "the water sprite", data => ObjectImage.From(SpriteFile.Read(data)))
            );
        }

        for (
            var entry = ObjectSprites.FlagEntry + 1;
            entry < ObjectSprites.FlagEntry + ObjectSprites.FlagFrames;
            entry++
        )
        {
            images.Add(
                archive[entry].State == EntryState.Absent
                    ? ObjectImage.Empty
                    : Read(archive, entry, "the flag sprite", data => ObjectImage.From(SpriteFile.Read(data)))
            );
        }

        // The shadow of each sprite in the order of the sprite layers: the goods and the water draw none.
        ObjectImage ShadowOf(int entry)
        {
            var shadow = entry + ShadowOffset;
            return shadow >= archive.RowCount || archive[shadow].State == EntryState.Absent
                ? ObjectImage.Empty
                : Read(archive, shadow, "the shadow mask", data => ObjectImage.Shadow(MaskFile.Read(data)));
        }

        var shadows = Enumerable
            .Range(ObjectSprites.FirstEntry, EntryCount)
            .Append(ObjectSprites.CastleEntry)
            .Append(ObjectSprites.FlagEntry)
            .Append(ObjectSprites.WoodcutterEntry)
            .Select(ShadowOf)
            .Concat(Enumerable.Repeat(ObjectImage.Empty, ObjectSprites.GoodsCount + ObjectSprites.WaterCount))
            .Concat(Enumerable.Range(ObjectSprites.FlagEntry + 1, ObjectSprites.FlagFrames - 1).Select(ShadowOf))
            .ToList();

        var width = images.Max(image => image.Width);
        var height = images.Max(image => image.Height);
        if (width == 0 || height == 0)
        {
            throw new InvalidDataException(
                $"{TerrainGraphics.ArchiveFile} holds no object sprite: entries {ObjectSprites.FirstEntry} to {ObjectSprites.FirstEntry + EntryCount - 1} are all absent"
            );
        }

        var padded = images.Select(image => Pad(image, width, height)).ToList();
        var shadowWidth = Math.Max(shadows.Max(image => image.Width), 1);
        var shadowHeight = Math.Max(shadows.Max(image => image.Height), 1);

        return new ObjectGraphics
        {
            PaletteRgba = PaletteTexture.Rgba(palette),
            LayerWidth = width,
            LayerHeight = height,
            Images = padded,
            ShadowLayerWidth = shadowWidth,
            ShadowLayerHeight = shadowHeight,
            ShadowImages = [.. shadows.Select(image => Pad(image, shadowWidth, shadowHeight))],
        };
    }

    // The pixel arrays grow to the layer size at the top left; Width and Height stay the sprite's own.
    internal static ObjectImage Pad(ObjectImage image, int width, int height)
    {
        var indices = new byte[width * height];
        var coverage = new byte[width * height];
        for (var y = 0; y < image.Height; y++)
        {
            Array.Copy(image.Indices, y * image.Width, indices, y * width, image.Width);
            Array.Copy(image.Coverage, y * image.Width, coverage, y * width, image.Width);
        }

        return image with
        {
            Indices = indices,
            Coverage = coverage,
        };
    }

    /// <summary>Reads entry <paramref name="number"/> with <paramref name="read"/>; an absent or unreadable entry fails naming <paramref name="what"/> and the entry.</summary>
    internal static T Read<T>(Archive archive, int number, string what, Func<ReadOnlyMemory<byte>, T> read)
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
            throw new InvalidDataException(
                $"{what} (entry {number}) of {TerrainGraphics.ArchiveFile} is unreadable: {e.Message}",
                e
            );
        }
    }
}
