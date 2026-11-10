using System;
using System.Collections.Generic;
using System.IO;
using OpenSettlers.Assets;
using OpenSettlers.Domain;

namespace OpenSettlers.MapView;

/// <summary>
/// The graphics source of the History Edition install (ADR 0014). It keeps the original's encoding inside itself: the
/// slope tables of <see cref="TerrainRules"/> and the texture picks of <see cref="RoadRules"/> choose a mask and a
/// texture entry of <c>loca/SPAD.PA</c>, and the source bakes the two into one RGBA image. The textured fill repeats the
/// texture down every <see cref="TerrainGraphics.TextureHeight"/> rows from the top of the mask
/// (<c>docs/rules/graphics-images.md</c>, "Textured fill"). A term the original has no mask for draws nothing. For the
/// objects it holds the entry picks and the phase folding of <see cref="ObjectSprites"/>, the shadow mask of each object
/// sprite, and the surface color of the water ripple; an object code with no sprite draws nothing.
/// </summary>
public sealed class HistoryEditionSource : IGraphicsSource
{
    readonly TerrainGraphics _terrain;
    readonly RoadGraphics? _roads;
    readonly Dictionary<(int Mask, int Texture), ObjectImage?> _terrainImages = [];
    readonly Dictionary<(int Mask, int Texture), ObjectImage?> _roadImages = [];

    readonly ObjectGraphics? _objects;
    readonly Dictionary<int, ObjectDrawing?> _drawings = [];
    readonly Dictionary<int, RippleImage?> _ripples = [];

    HistoryEditionSource(
        TerrainGraphics terrain,
        RoadGraphics? roads,
        string? roadsProblem,
        ObjectGraphics? objects,
        string? objectsProblem
    )
    {
        _terrain = terrain;
        _roads = roads;
        RoadsProblem = roadsProblem;
        _objects = objects;
        ObjectsProblem = objectsProblem;
    }

    /// <summary>Why the road graphics did not load, or null. The source draws no road then and still draws the terrain.</summary>
    public string? RoadsProblem { get; }

    /// <summary>Why the object graphics did not load, or null. The source draws no object then and still draws the terrain.</summary>
    public string? ObjectsProblem { get; }

    /// <summary>
    /// Reads the graphics from the install. Throws <see cref="FileNotFoundException"/> for a missing archive and
    /// <see cref="InvalidDataException"/>, naming the entry and the reason, where the terrain graphics are absent or
    /// unreadable. Unreadable road graphics set <see cref="RoadsProblem"/>, and unreadable object graphics
    /// <see cref="ObjectsProblem"/>, instead.
    /// </summary>
    public static HistoryEditionSource Load(Install install)
    {
        var terrain = TerrainGraphics.Load(install);
        RoadGraphics? roads = null;
        string? roadsProblem = null;
        try
        {
            roads = RoadGraphics.Load(install);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            roadsProblem = e.Message;
        }

        ObjectGraphics? objects = null;
        string? objectsProblem = null;
        try
        {
            objects = ObjectGraphics.Load(install);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            objectsProblem = e.Message;
        }

        return new HistoryEditionSource(terrain, roads, roadsProblem, objects, objectsProblem);
    }

    /// <inheritdoc />
    public int ObjectHeadroom => _objects?.Headroom ?? 0;

    /// <inheritdoc />
    public ObjectDrawing? Draw(MapObjectDraw draw) =>
        ObjectSprites.Entry(draw.Object, draw.Step) is { } entry ? Drawing(entry) : null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(BuildingDraw draw) =>
        draw.Type == BuildingType.Castle ? Drawing(ObjectSprites.CastleEntry)
        : draw.Type == BuildingType.Woodcutter ? Drawing(ObjectSprites.WoodcutterEntry)
        : null;

    /// <inheritdoc />
    public ObjectDrawing? Draw(GoodAtFlag draw) => Drawing(ObjectSprites.GoodEntry(draw.Good));

    /// <inheritdoc />
    public RippleImage? Draw(WaterRipple ripple)
    {
        var entry = ObjectSprites.WaterEntry(ripple.Tile, ripple.Step);
        if (_objects is null)
        {
            return null;
        }

        if (!_ripples.TryGetValue(entry, out var cached))
        {
            cached = Usable(_objects.Sprites.GetValueOrDefault(entry)) is { } image
                ? new RippleImage(image, _objects.SurfaceRgba)
                : null;
            _ripples[entry] = cached;
        }

        return cached;
    }

    // What the archive draws for an object sprite entry, made once so that equal entries answer with one instance: the
    // sprite and the shadow mask at its entry plus 250, or null where it has neither.
    ObjectDrawing? Drawing(int entry)
    {
        if (_objects is null)
        {
            return null;
        }

        if (!_drawings.TryGetValue(entry, out var cached))
        {
            var image = Usable(_objects.Sprites.GetValueOrDefault(entry));
            var shadow = Usable(_objects.Shadows.GetValueOrDefault(entry));
            cached = image is null && shadow is null ? null : new ObjectDrawing(image, shadow);
            _drawings[entry] = cached;
        }

        return cached;
    }

    // A sprite with no pixel draws nothing.
    static ObjectImage? Usable(ObjectImage? image) => image is { Width: > 0, Height: > 0 } ? image : null;

    /// <inheritdoc />
    public ObjectImage? Draw(TerrainTriangle triangle)
    {
        var pick = triangle.Upper
            ? TerrainRules.Upper(triangle.Apex, triangle.Left, triangle.Right, triangle.Terrain)
            : TerrainRules.Lower(triangle.Apex, triangle.Left, triangle.Right, triangle.Terrain);

        return pick is { } draw
            ? Image(
                _terrainImages,
                draw.Mask,
                draw.Texture,
                _terrain.Masks[TerrainGraphics.MaskLayer(draw.Mask)],
                _terrain.Textures[TerrainGraphics.TextureLayer(draw.Texture)],
                -TerrainGraphics.LayerWidth / 2
            )
            : null;
    }

    /// <inheritdoc />
    public ObjectImage? Draw(RoadStrip strip) =>
        _roads is not null && RoadRules.Of(strip) is { } draw
            ? Image(
                _roadImages,
                draw.Mask,
                draw.Texture,
                _roads.Masks[RoadGraphics.MaskLayer(draw.Mask)],
                _roads.Textures[RoadGraphics.TextureLayer(draw.Texture)],
                0
            )
            : null;

    // The image of a mask and a texture entry, made once: the texture's color where the mask covers, nothing elsewhere.
    // A mask with no pixel draws nothing. A terrain mask hangs from its anchor centered, so the caller passes the shift.
    static ObjectImage? Image(
        Dictionary<(int Mask, int Texture), ObjectImage?> cache,
        int maskEntry,
        int textureEntry,
        MaskImage mask,
        byte[] texture,
        int shiftX
    )
    {
        if (cache.TryGetValue((maskEntry, textureEntry), out var cached))
        {
            return cached;
        }

        ObjectImage? image = null;
        if (mask.Width > 0 && mask.Height > 0)
        {
            var rgba = new byte[mask.Width * mask.Height * 4];
            for (var y = 0; y < mask.Height; y++)
            {
                for (var x = 0; x < mask.Width; x++)
                {
                    if (mask.Layer[y * TerrainGraphics.LayerWidth + x] != 0)
                    {
                        Array.Copy(
                            texture,
                            ((y % TerrainGraphics.TextureHeight) * TerrainGraphics.LayerWidth + x) * 4,
                            rgba,
                            (y * mask.Width + x) * 4,
                            4
                        );
                    }
                }
            }

            image = new ObjectImage(mask.Width, mask.Height, mask.OffsetX + shiftX, mask.OffsetY, rgba);
        }

        cache[(maskEntry, textureEntry)] = image;

        return image;
    }
}
