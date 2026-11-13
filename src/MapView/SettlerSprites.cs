using System;
using System.Collections.Generic;
using System.IO;
using OpenSettlers.Assets;
using OpenSettlers.Domain;
using OpenSettlers.Formats;

namespace OpenSettlers.MapView;

/// <summary>
/// The History Edition's settler images (ADR 0014): the figure tables pick a sprite entry for each settler term
/// (<see cref="SettlerFigureTables"/>) and this class reads it from the archive when first asked, fills a colour overlay with
/// the player's colour, anchors a second figure by the header of its figure and a waiting carrier by its road directions, and
/// adds the settler's shadow to a figure. Equal picks answer with one image instance. A sprite the archive does not hold, or
/// cannot read, draws nothing. Only the History Edition source reads it.
/// </summary>
public sealed class SettlerSprites
{
    /// <summary>
    /// The mask entry of a settler's shadow: <c>Gfx_DrawSettlerRecords</c> stores 4 at <c>0x45006a</c> and draws it
    /// through <c>Gfx_DrawShadow</c> at <c>0x45007d</c>, at the record's position, before the figure.
    /// </summary>
    public const int ShadowEntry = 4;

    private readonly Archive _archive;
    private readonly Palette _palette;
    private readonly SettlerFigureTables _tables;
    private readonly ObjectImage? _shadow;
    private readonly Dictionary<int, (ObjectImage Image, int First, int Second)?> _sprites = [];
    private readonly Dictionary<(int Entry, int Colour), ObjectImage?> _filled = [];
    private readonly Dictionary<(int Entry, int Colour, int X, int Y), ObjectImage?> _anchored = [];
    private readonly Dictionary<(int X, int Y), ObjectImage?> _shadows = [];
    private readonly Dictionary<(SettlerPick Pick, bool Shadowed), ObjectDrawing?> _drawings = [];

    private SettlerSprites(
        Archive archive,
        Palette palette,
        SettlerFigureTables tables,
        ObjectImage? shadow,
        StepOffsets steps
    )
    {
        Steps = steps;
        _archive = archive;
        _palette = palette;
        _tables = tables;
        _shadow = shadow;
    }

    /// <summary>
    /// Reads the palette, the walk sequences, and the shadow mask from the install; the sprites are read when first asked
    /// for. Throws <see cref="FileNotFoundException"/> for a missing archive and <see cref="InvalidDataException"/>, naming
    /// the entry and the reason, for the palette, the sequences, or the shadow mask that is absent or unreadable. An absent
    /// shadow mask gives no shadow.
    /// </summary>
    public static SettlerSprites Load(Install install, RuleTables tables)
    {
        if (!install.Exists(TerrainGraphics.ArchiveFile))
        {
            throw new FileNotFoundException(
                $"the archive {TerrainGraphics.ArchiveFile} is missing: the install must hold it"
            );
        }

        var archive = ArchiveFile.Read(install.ReadAll(TerrainGraphics.ArchiveFile));
        var palette = TerrainGraphics.Entry(archive, 3, "the palette", data => PaletteFile.Read(data.Span));
        var steps = StepOffsets.From(archive);
        ObjectImage? shadow = null;
        if (archive.RowCount > ShadowEntry && archive[ShadowEntry].State != EntryState.Absent)
        {
            shadow = TerrainGraphics.Entry(
                archive,
                ShadowEntry,
                "the shadow mask",
                data => ObjectImage.Shadow(MaskFile.Read(data), palette)
            );
        }

        return new SettlerSprites(archive, palette, SettlerFigureTables.From(tables, steps.Sequences), shadow, steps);
    }

    /// <summary>The step offsets of the walk sequences the tables read, parsed once for the view's settler placement.</summary>
    public StepOffsets Steps { get; }

    /// <summary>
    /// What the tables pick for <paramref name="draw"/>: the sprite, with the settler's shadow before a figure; null where
    /// the tables pick none or the archive has no sprite for the pick.
    /// </summary>
    public ObjectDrawing? Draw(SettlerDraw draw)
    {
        if (_tables.Pick(draw) is not { } pick)
        {
            return null;
        }

        var key = (pick, draw.Part == SettlerPart.Figure);
        if (!_drawings.TryGetValue(key, out var cached))
        {
            cached = Make(pick, key.Item2);
            _drawings[key] = cached;
        }

        return cached;
    }

    private ObjectDrawing? Make(SettlerPick pick, bool shadowed)
    {
        // A second figure moves by the header bytes 0 and 1 of its figure, read as signed bytes (0x4066d2, 0x4066e4); a figure
        // that does not read shifts nothing.
        var (shiftX, shiftY) =
            pick.ShiftedBy >= 0 && Loaded(pick.ShiftedBy) is { } figure ? (figure.First, figure.Second) : (0, 0);
        if (Anchored(pick.Entry, pick.Colour, pick.Dx + shiftX, pick.Dy + shiftY) is not { } image)
        {
            return null;
        }

        return new ObjectDrawing(image, shadowed ? Shadow(pick.Dx, pick.Dy) : null);
    }

    // The image of an entry filled with a colour (-1 for none), hung moved by x and y; one instance for each, so that the
    // picks that land on the same image share one layer of the view's array texture (ImageLayers keys an image by reference).
    private ObjectImage? Anchored(int entry, int colour, int x, int y)
    {
        if (!_anchored.TryGetValue((entry, colour, x, y), out var cached))
        {
            cached = Filled(entry, colour) is { } image ? Moved(image, x, y) : null;
            _anchored[(entry, colour, x, y)] = cached;
        }

        return cached;
    }

    // The shadow mask hung moved by x and y; one instance for each, for the same reason.
    private ObjectImage? Shadow(int x, int y)
    {
        if (_shadow is null)
        {
            return null;
        }

        if (!_shadows.TryGetValue((x, y), out var cached))
        {
            cached = Moved(_shadow, x, y);
            _shadows[(x, y)] = cached;
        }

        return cached;
    }

    private static ObjectImage Moved(ObjectImage image, int x, int y) =>
        x == 0 && y == 0 ? image : image with { OffsetX = image.OffsetX + x, OffsetY = image.OffsetY + y };

    private ObjectImage? Filled(int entry, int colour)
    {
        if (colour < 0)
        {
            return Loaded(entry)?.Image;
        }

        if (!_filled.TryGetValue((entry, colour), out var cached))
        {
            cached = Loaded(entry)?.Image is { } image ? Fill(image, _palette, colour) : null;
            _filled[(entry, colour)] = cached;
        }

        return cached;
    }

    // The sprite of an entry with its header bytes 0 and 1, or null where the archive holds none or it does not read. A
    // sequence names figures for every job, and some of the entries it reaches are no sprites.
    private (ObjectImage Image, int First, int Second)? Loaded(int entry)
    {
        if (!_sprites.TryGetValue(entry, out var cached))
        {
            cached = null;
            if (entry >= 1 && entry < _archive.RowCount && _archive[entry].State != EntryState.Absent)
            {
                try
                {
                    var sprite = SpriteFile.Read(_archive.Data(_archive[entry]));
                    cached = (ObjectImage.From(sprite, _palette), sprite.Header.First, sprite.Header.Second);
                }
                catch (InvalidDataException)
                {
                    // An entry that is no sprite draws nothing.
                }
            }

            _sprites[entry] = cached;
        }

        return cached;
    }

    /// <summary>
    /// A colour overlay drawn in one colour: every covered pixel takes the colour of palette index
    /// <paramref name="colour"/>, and the transparent pixels stay transparent (0x405880 skips them).
    /// </summary>
    public static ObjectImage Fill(ObjectImage overlay, Palette palette, int colour)
    {
        var rgba = (byte[])overlay.Rgba.Clone();
        for (var i = 0; i < rgba.Length; i += 4)
        {
            if (rgba[i + 3] != 0)
            {
                palette.Rgba(colour).CopyTo(rgba.AsSpan(i, 4));
            }
        }

        return overlay with
        {
            Rgba = rgba,
        };
    }
}
