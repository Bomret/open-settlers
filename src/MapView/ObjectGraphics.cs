using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenSettlers.Assets;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// The object sprites of a History Edition install, by archive entry (ADR 0014): the sprites of codes 8 to 127 (entries
/// 1250 to 1369), the castle's, the flag's with its further frames, the hut's, the goods', and the water's, and the
/// shadow masks of those that have one. Only the History Edition source reads it.
/// </summary>
public sealed class ObjectGraphics
{
    /// <summary>
    /// The color of palette index <see cref="SurfaceIndex"/> as four bytes (red, green, blue, alpha): the surface the
    /// water sprites draw over (<c>docs/rules/graphics-images.md</c>, "Sprites drawn over color 8").
    /// </summary>
    public byte[] SurfaceRgba { get; init; } = new byte[4];

    /// <summary>The palette index of the water surface.</summary>
    public const int SurfaceIndex = 8;

    /// <summary>The sprite of each entry the archive holds, at its own size.</summary>
    public required IReadOnlyDictionary<int, ObjectImage> Sprites { get; init; }

    /// <summary>The shadow of each sprite entry that has a mask in the archive. The goods and the water draw no shadow (<c>0x44de9a</c>, <c>0x44ff59</c> draw only the sprite).</summary>
    public IReadOnlyDictionary<int, ObjectImage> Shadows { get; init; } = new Dictionary<int, ObjectImage>();

    /// <summary>The height of the tallest sprite.</summary>
    public required int Headroom { get; init; }

    /// <summary>
    /// The number <c>Gfx_DrawObjectSprite</c> (<c>0x44da4d</c>) adds to an object sprite entry for its shadow mask: it
    /// draws the mask of the number plus 1500 through <c>Gfx_DrawShadow</c> (<c>0x4061a0</c>), then the sprite of the
    /// number plus 1250.
    /// </summary>
    public const int ShadowOffset = 1500 - ObjectSprites.FirstEntry;

    /// <summary>
    /// Reads the sprites from the install. Throws <see cref="FileNotFoundException"/> for a missing archive and
    /// <see cref="InvalidDataException"/>, naming the entry and the reason, for a sprite that is unreadable and for an
    /// archive that holds no object sprite. An entry the archive leaves out has no sprite.
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
        var sprites = new Dictionary<int, ObjectImage>();

        bool Holds(int entry) => entry < archive.RowCount && archive[entry].State != EntryState.Absent;

        void AddSprites(IEnumerable<int> entries, string what)
        {
            foreach (var entry in entries.Where(Holds))
            {
                sprites[entry] = Read(archive, entry, what, data => ObjectImage.From(SpriteFile.Read(data), palette));
            }
        }

        AddSprites(Enumerable.Range(ObjectSprites.FirstEntry, ObjectSprites.EntryCount), "the sprite");
        AddSprites([ObjectSprites.CastleEntry], "the castle sprite");
        AddSprites([ObjectSprites.FlagEntry], "the flag sprite");
        AddSprites([ObjectSprites.WoodcutterEntry], "the woodcutter's hut sprite");
        AddSprites(
            [
                ObjectSprites.StonecutterEntry,
                ObjectSprites.ForesterEntry,
                ObjectSprites.SawmillEntry,
                ObjectSprites.SiteLargeEntry,
                ObjectSprites.SiteSmallEntry,
            ],
            "the building's sprite"
        );
        AddSprites(Enumerable.Range(ObjectSprites.GoodsFirstEntry, ObjectSprites.GoodsCount), "the good's sprite");
        AddSprites(Enumerable.Range(ObjectSprites.WaterFirstEntry, ObjectSprites.WaterCount), "the water sprite");
        AddSprites(Enumerable.Range(ObjectSprites.FlagEntry + 1, ObjectSprites.FlagFrames - 1), "the flag sprite");

        var shadows = new Dictionary<int, ObjectImage>();
        var shadowed = Enumerable
            .Range(ObjectSprites.FirstEntry, ObjectSprites.EntryCount)
            .Append(ObjectSprites.CastleEntry)
            .Append(ObjectSprites.FlagEntry)
            .Append(ObjectSprites.WoodcutterEntry)
            .Append(ObjectSprites.StonecutterEntry)
            .Append(ObjectSprites.ForesterEntry)
            .Append(ObjectSprites.SawmillEntry)
            .Append(ObjectSprites.SiteLargeEntry)
            .Append(ObjectSprites.SiteSmallEntry)
            .Concat(Enumerable.Range(ObjectSprites.FlagEntry + 1, ObjectSprites.FlagFrames - 1));
        foreach (var entry in shadowed.Where(entry => Holds(entry + ShadowOffset)))
        {
            shadows[entry] = Read(
                archive,
                entry + ShadowOffset,
                "the shadow mask",
                data => ObjectImage.Shadow(MaskFile.Read(data), palette)
            );
        }

        if (
            sprites.Values.Select(image => image.Width).DefaultIfEmpty().Max() == 0
            || sprites.Values.Select(image => image.Height).DefaultIfEmpty().Max() == 0
        )
        {
            throw new InvalidDataException(
                $"{TerrainGraphics.ArchiveFile} holds no object sprite: entries {ObjectSprites.FirstEntry} to {ObjectSprites.FirstEntry + ObjectSprites.EntryCount - 1} are all absent"
            );
        }

        return new ObjectGraphics
        {
            SurfaceRgba = palette.Rgba(SurfaceIndex).ToArray(),
            Sprites = sprites,
            Shadows = shadows,
            Headroom = sprites.Values.Max(image => image.Height),
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
